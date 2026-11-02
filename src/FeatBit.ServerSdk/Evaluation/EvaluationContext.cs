using FeatBit.Sdk.Server.Model;

namespace FeatBit.Sdk.Server.Evaluation
{
    internal class EvaluationContext
    {
        public string FlagKey { get; set; }

        public FbUser FbUser { get; set; }

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(FlagKey) && !string.IsNullOrWhiteSpace(FbUser?.Key);
        }
    }
}
