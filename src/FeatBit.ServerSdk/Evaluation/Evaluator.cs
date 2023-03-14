using System.Linq;
using System.Runtime.CompilerServices;
using FeatBit.Sdk.Server.Events;
using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server.Evaluation
{
    internal partial class Evaluator : IEvaluator
    {
        private readonly IMemoryStore _store;

        public Evaluator(IMemoryStore store)
        {
            _store = store;
        }

        public (EvalResult evalResult, EvalEvent evalEvent) Evaluate(EvaluationContext context)
        {
            var storeKey = StoreKeys.ForFeatureFlag(context.FlagKey);

            var flag = _store.Get<FeatureFlag>(storeKey);
            if (flag == null)
            {
                return FlagNotFound();
            }

            return Evaluate(flag, context.FbUser);

            (EvalResult EvalResult, EvalEvent evalEvent) FlagNotFound() => (EvalResult.FlagNotFound, null);
        }

        public (EvalResult evalResult, EvalEvent evalEvent) Evaluate(FeatureFlag flag, FbUser user)
        {
            var flagKey = flag.Key;

            // if flag is disabled
            if (!flag.IsEnabled)
            {
                var disabledVariation = flag.GetVariation(flag.DisabledVariationId);
                if (disabledVariation == null)
                {
                    return MalformedFlag();
                }

                return FlagOff(disabledVariation);
            }

            // if user is targeted
            var targetUser = flag.TargetUsers.FirstOrDefault(x => x.KeyIds.Contains(user.Key));
            if (targetUser != null)
            {
                var targetedVariation = flag.GetVariation(targetUser.VariationId);
                return Targeted(targetedVariation, flag.ExptIncludeAllTargets);
            }

            string dispatchKey;

            // if user is rule matched
            foreach (var rule in flag.Rules)
            {
                if (IsMatchRule(rule, user))
                {
                    var ruleDispatchKey = rule.DispatchKey;
                    dispatchKey = string.IsNullOrWhiteSpace(ruleDispatchKey)
                        ? $"{flagKey}{user.Key}"
                        : $"{flagKey}{user.ValueOf(ruleDispatchKey)}";

                    var rolloutVariation = rule.Variations.FirstOrDefault(x => x.IsInRollout(dispatchKey));
                    if (rolloutVariation == null)
                    {
                        return MalformedFlag();
                    }

                    return RuleMatched(rule, rolloutVariation);
                }
            }

            // match default rule
            var fallthroughDispatchKey = flag.Fallthrough.DispatchKey;
            dispatchKey = string.IsNullOrWhiteSpace(fallthroughDispatchKey)
                ? $"{flagKey}{user.Key}"
                : $"{flagKey}{user.ValueOf(fallthroughDispatchKey)}";

            var defaultVariation =
                flag.Fallthrough.Variations.FirstOrDefault(x => x.IsInRollout(dispatchKey));
            if (defaultVariation == null)
            {
                return MalformedFlag();
            }

            return Fallthrough();

            (EvalResult EvalResult, EvalEvent evalEvent) MalformedFlag() => (EvalResult.MalformedFlag, null);

            (EvalResult EvalResult, EvalEvent evalEvent) FlagOff(Variation variation) =>
                (EvalResult.FlagOff(variation.Value), new EvalEvent(user, flagKey, variation, false));

            (EvalResult EvalResult, EvalEvent evalEvent) Targeted(Variation variation, bool exptIncludeAllTargets) =>
                (EvalResult.Targeted(variation.Value), new EvalEvent(user, flagKey, variation, exptIncludeAllTargets));

            (EvalResult EvalResult, EvalEvent evalEvent) RuleMatched(TargetRule rule, RolloutVariation rolloutVariation)
            {
                var variation = flag.GetVariation(rolloutVariation.Id);

                var evalResult = EvalResult.RuleMatched(variation.Value, rule.Name);

                var sendToExperiment = IsSendToExperiment(
                    flag.ExptIncludeAllTargets,
                    rule.IncludedInExpt,
                    dispatchKey,
                    rolloutVariation
                );
                var evalEvent = new EvalEvent(user, flagKey, variation, sendToExperiment);

                return (evalResult, evalEvent);
            }

            (EvalResult EvalResult, EvalEvent evalEvent) Fallthrough()
            {
                var variation = flag.GetVariation(defaultVariation.Id);

                var evalResult = EvalResult.Fallthrough(variation.Value);

                var sendToExperiment = IsSendToExperiment(
                    flag.ExptIncludeAllTargets,
                    flag.Fallthrough.IncludedInExpt,
                    dispatchKey,
                    defaultVariation
                );
                var evalEvent = new EvalEvent(user, flagKey, variation, sendToExperiment);

                return (evalResult, evalEvent);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSendToExperiment(
            bool exptIncludeAllTargets,
            bool thisRuleIncludeInExpt,
            string dispatchKey,
            RolloutVariation rolloutVariation)
        {
            if (exptIncludeAllTargets)
            {
                return true;
            }

            if (!thisRuleIncludeInExpt)
            {
                return false;
            }

            // create a new key to calculate the experiment dispatch percentage
            const string exptDispatchKeyPrefix = "expt";
            var sendToExptKey = $"{exptDispatchKeyPrefix}{dispatchKey}";

            var exptRollout = rolloutVariation.ExptRollout;
            var dispatchRollout = rolloutVariation.DispatchRollout();
            if (exptRollout == 0.0 || dispatchRollout == 0.0)
            {
                return false;
            }

            var upperBound = exptRollout / dispatchRollout;
            if (upperBound > 1.0)
            {
                upperBound = 1.0;
            }

            return DispatchAlgorithm.IsInRollout(sendToExptKey, new[] { 0.0, upperBound });
        }
    }
}