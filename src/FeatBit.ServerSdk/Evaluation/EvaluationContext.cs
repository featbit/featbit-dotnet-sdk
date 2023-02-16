using FeatBit.Sdk.Server.Model;

namespace FeatBit.Sdk.Server.Evaluation
{
    internal class EvaluationContext
    {
        public string FlagKey { get; set; }

        public FbUser FbUser { get; set; }
    }
}