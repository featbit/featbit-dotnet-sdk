namespace FeatBit.Sdk.Server.Evaluation
{
    internal interface IEvaluator
    {
        EvalResult Evaluate(EvaluationContext context);
    }
}