using FeatBit.Sdk.Server.Model;

namespace FeatBit.Sdk.Server.Evaluation
{
    internal partial class Evaluator
    {
        internal const string IsInSegmentProperty = "User is in segment";

        internal const string IsNotInSegmentProperty = "User is not in segment";

        internal bool IsMatchRule(TargetRule rule, FbUser user)
        {
            foreach (var condition in rule.Conditions)
            {
                // in segment condition
                if (condition.Property is IsInSegmentProperty)
                {
                    if (!IsMatchAnySegment(condition, user))
                    {
                        return false;
                    }
                }
                // not in segment condition
                else if (condition.Property is IsNotInSegmentProperty)
                {
                    if (IsMatchAnySegment(condition, user))
                    {
                        return false;
                    }
                }
                // common condition
                else if (!IsMatchCondition(condition, user))
                {
                    return false;
                }
            }

            return true;
        }
    }
}