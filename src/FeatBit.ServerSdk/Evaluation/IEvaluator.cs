using FeatBit.Sdk.Server.Model;

namespace FeatBit.Sdk.Server.Evaluation
{
    internal interface IEvaluator
    {
        EvalResult Evaluate(EvaluationContext context);

        EvalResult Evaluate(FeatureFlag flag, FbUser user);
    }
}