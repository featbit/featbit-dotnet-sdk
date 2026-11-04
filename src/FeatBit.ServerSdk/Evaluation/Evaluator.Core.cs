using System;
using System.Linq;
using FeatBit.Sdk.Server.Model;

namespace FeatBit.Sdk.Server.Evaluation;

internal partial class Evaluator
{
    internal EvalResult Evaluate(FeatureFlag flag, FbUser user)
    {
        try
        {
            var evalResult = EvaluateCore();
            if (evalResult.Kind == ReasonKind.Error)
            {
                return evalResult;
            }

            if (evalResult.Variation?.Value == null)
            {
                return EvalResult.MalformedFlag;
            }

            return evalResult;
        }
        catch (Exception)
        {
            return EvalResult.MalformedFlag;
        }

        EvalResult EvaluateCore()
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

            EvalResult MalformedFlag() => EvalResult.MalformedFlag;

            EvalResult FlagOff(Variation variation) =>
                EvalResult.FlagOff(variation, flag.InsightsEnabled);

            EvalResult Targeted(Variation variation) =>
                EvalResult.Targeted(variation, flag.InsightsEnabled);

            EvalResult RuleMatched(TargetRule rule, RolloutVariation rolloutVariation)
            {
                var variation = flag.GetVariation(rolloutVariation.Id);

                return EvalResult.RuleMatched(rule.Name, variation, flag.InsightsEnabled);
            }

            EvalResult Fallthrough()
            {
                var variation = flag.GetVariation(defaultVariation.Id);

                return EvalResult.Fallthrough(variation, flag.InsightsEnabled);
            }
        }
    }
}
