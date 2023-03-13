using System.Linq;
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
                return Targeted(targetedVariation);
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

                    var ruleMatchedVariation = flag.GetVariation(rolloutVariation.Id);
                    return RuleMatched(ruleMatchedVariation, rule.Name);
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

            var defaultRuleVariation = flag.GetVariation(defaultVariation.Id);
            return Fallthrough(defaultRuleVariation);

            (EvalResult EvalResult, EvalEvent evalEvent) MalformedFlag() => (EvalResult.MalformedFlag, null);

            (EvalResult EvalResult, EvalEvent evalEvent) FlagOff(Variation variation) =>
                (EvalResult.FlagOff(variation.Value), new EvalEvent(user, flagKey, variation));

            (EvalResult EvalResult, EvalEvent evalEvent) Targeted(Variation variation) =>
                (EvalResult.Targeted(variation.Value), new EvalEvent(user, flagKey, variation));

            (EvalResult EvalResult, EvalEvent evalEvent) RuleMatched(Variation variation, string ruleName) =>
                (EvalResult.RuleMatched(variation.Value, ruleName), new EvalEvent(user, flagKey, variation));

            (EvalResult EvalResult, EvalEvent evalEvent) Fallthrough(Variation variation) =>
                (EvalResult.Fallthrough(variation.Value), new EvalEvent(user, flagKey, variation));
        }
    }
}