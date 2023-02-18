namespace FeatBit.Sdk.Server.Evaluation
{
    public class EvalDetail<TValue>
    {
        /// <summary>
        /// An enum indicating the category of the reason.
        /// </summary>
        public ReasonKind Kind { get; set; }

        /// <summary>
        /// A string describing the main factor that influenced the flag evaluation value.
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// The result of the flag evaluation. This will be either one of the flag's variations or the default
        /// value that was specified when the flag was evaluated.
        /// </summary>
        public TValue Value { get; set; }

        /// <summary>
        /// Constructs a new EvalDetail instance.
        /// </summary>
        /// <param name="kind">the reason kind</param>
        /// <param name="reason">the evaluation reason</param>
        /// <param name="value">the flag value</param>
        public EvalDetail(ReasonKind kind, string reason, TValue value)
        {
            Kind = kind;
            Reason = reason;
            Value = value;
        }
    }
}