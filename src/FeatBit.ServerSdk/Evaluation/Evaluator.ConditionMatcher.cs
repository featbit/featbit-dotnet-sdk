using FeatBit.Sdk.Server.Model;

namespace FeatBit.Sdk.Server.Evaluation
{
    internal partial class Evaluator
    {
        internal static bool IsMatchCondition(Condition condition, FbUser user)
        {
            var userValue = user.ValueOf(condition.Property);

            var theOperator = Operator.Get(condition.Op);
            return theOperator.IsMatch(userValue, condition.Value);
        }
    }
}