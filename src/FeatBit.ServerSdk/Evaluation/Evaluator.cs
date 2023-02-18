using System.Linq;
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

        public EvalResult Evaluate(EvaluationContext context)
        {
            var storeKey = StoreKeys.ForFeatureFlag(context.FlagKey);

            var flag = _store.Get<FeatureFlag>(storeKey);
            if (flag == null)
            {
                return EvalResult.FlagNotFound;
            }

            return Evaluate(flag, context.FbUser);
        }

        public EvalResult Evaluate(FeatureFlag flag, FbUser user)
        {
            // if flag is disabled
            if (!flag.IsEnabled)
            {
                var disabledVariation = flag.GetVariation(flag.DisabledVariationId);
                if (disabledVariation == null)
                {
                    return EvalResult.MalformedFlag;
                }

                return EvalResult.FlagOff(disabledVariation.Value);
            }

            // if user is targeted
            var targetUser = flag.TargetUsers.FirstOrDefault(x => x.KeyIds.Contains(user.Key));
            if (targetUser != null)
            {
                var targetedVariation = flag.GetVariation(targetUser.VariationId);
                return EvalResult.Targeted(targetedVariation.Value);
            }

            var flagKey = flag.Key;
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
                        return EvalResult.MalformedFlag;
                    }

                    var ruleMatchedVariation = flag.GetVariation(rolloutVariation.Id);
                    return EvalResult.RuleMatched(ruleMatchedVariation.Value, rule.Name);
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
                return EvalResult.MalformedFlag;
            }

            var defaultRuleVariation = flag.GetVariation(defaultVariation.Id);
            return EvalResult.Fallthrough(defaultRuleVariation.Value);
        }
    }
}