using FeatBit.Sdk.Server.Events;
using FeatBit.Sdk.Server.Model;

namespace FeatBit.Sdk.Server.Evaluation
{
    internal interface IEvaluator
    {
        (EvalResult evalResult, EvalEvent evalEvent) Evaluate(EvaluationContext context);

        EvalDetail<string>[] EvaluateAll(FbUser user);
    }
}