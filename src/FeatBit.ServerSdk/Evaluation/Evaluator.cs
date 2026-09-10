using System;
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
            if (context?.IsValid() != true)
            {
                return (EvalResult.InvalidInput, null);
            }

            var storeKey = StoreKeys.ForFeatureFlag(context.FlagKey);

            var flag = _store.Get<FeatureFlag>(storeKey);
            if (flag == null)
            {
                return (EvalResult.FlagNotFound, null);
            }

            return Evaluate(flag, context.FbUser);
        }

        public EvalDetail<string>[] EvaluateAll(FbUser user)
        {
            if (string.IsNullOrWhiteSpace(user?.Key))
            {
                return Array.Empty<EvalDetail<string>>();
            }

            var results = _store
                .Find<FeatureFlag>(x => x.StoreKey.StartsWith(StoreKeys.FlagPrefix))
                .Select(flag =>
                {
                    var (evalResult, _) = Evaluate(flag, user);
                    return evalResult.AsEvalDetail(flag.Key);
                })
                .ToArray();

            return results;
        }
    }
}
