namespace FeatBit.Sdk.Server.Evaluation
{
    public class EvalResult
    {
        public ReasonKind Kind { get; set; }

        public string Reason { get; set; }

        public string Value { get; set; }

        private EvalResult(ReasonKind kind, string reason, string value)
        {
            Kind = kind;
            Reason = reason;
            Value = value;
        }

        // Indicates that the caller provided a flag key that did not match any known flag.
        public static readonly EvalResult FlagNotFound =
            new EvalResult(ReasonKind.Error, "flag not found", string.Empty);

        // Indicates that there was an internal inconsistency in the flag data, e.g. a rule specified a nonexistent
        // variation. 
        public static readonly EvalResult MalformedFlag =
            new EvalResult(ReasonKind.Error, "malformed flag", string.Empty);

        public static EvalResult FlagOff(string value)
        {
            return new EvalResult(ReasonKind.Off, "flag off", value);
        }

        public static EvalResult Targeted(string value)
        {
            return new EvalResult(ReasonKind.TargetMatch, "target match", value);
        }

        public static EvalResult RuleMatched(string value, string ruleName)
        {
            return new EvalResult(ReasonKind.RuleMatch, $"match rule {ruleName}", value);
        }

        public static EvalResult Fallthrough(string value)
        {
            return new EvalResult(ReasonKind.Fallthrough, "fall through targets and rules", value);
        }
    }

    public enum ReasonKind
    {
        /// <summary>
        /// Indicates that the flag was off and therefore returned its configured off value.
        /// </summary>
        Off,

        /// <summary>
        /// Indicates that the flag was on but the user did not match any targets or rules.
        /// </summary>
        Fallthrough,

        /// <summary>
        /// Indicates that the user key was specifically targeted for this flag.
        /// </summary>
        TargetMatch,

        /// <summary>
        /// Indicates that the user matched one of the flag's rules.
        /// </summary>
        RuleMatch,

        /// <summary>
        /// Indicates that the flag could not be evaluated, e.g. because it does not exist or due to an unexpected
        /// error. In this case the result value will be the default value that the caller passed to the client.
        /// </summary>
        Error
    }
}