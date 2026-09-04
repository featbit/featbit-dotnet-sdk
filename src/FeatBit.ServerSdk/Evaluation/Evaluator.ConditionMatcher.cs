using FeatBit.Sdk.Server.Model;

namespace FeatBit.Sdk.Server.Evaluation
{
    internal partial class Evaluator
    {
        internal static bool IsMatchCondition(Condition condition, FbUser user)
        {
            if (string.IsNullOrWhiteSpace(condition.Property))
            {
                return false;
            }

            var hasProperty = user.TryGetValue(condition.Property, out var userValue);
            if (!hasProperty)
            {
                return false;
            }

            var theOperator = Operator.Get(condition.Op);
            return theOperator.IsMatch(userValue, condition.Value);
        }
    }
}
