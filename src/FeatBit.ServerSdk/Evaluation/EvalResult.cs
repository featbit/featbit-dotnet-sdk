namespace FeatBit.Sdk.Server.Evaluation
{
    internal class EvalResult
    {
        public ReasonKind Kind { get; set; }

        public string Reason { get; set; }

        public string Value { get; set; }
        public string ValueId { get; set; }

        private EvalResult(ReasonKind kind, string reason, string value, string valueId)
        {
            Kind = kind;
            Reason = reason;
            Value = value;
            ValueId = valueId;
        }

        // Indicates that the caller provided a flag key that did not match any known flag.
        public static readonly EvalResult FlagNotFound =
            new EvalResult(ReasonKind.Error, "flag not found", string.Empty, string.Empty);

        // Indicates that there was an internal inconsistency in the flag data, e.g. a rule specified a nonexistent
        // variation. 
        public static readonly EvalResult MalformedFlag =
            new EvalResult(ReasonKind.Error, "malformed flag", string.Empty, string.Empty);

        public static EvalResult FlagOff(string value, string valueId)
        {
            return new EvalResult(ReasonKind.Off, "flag off", value, valueId);
        }

        public static EvalResult Targeted(string value, string valueId)
        {
            return new EvalResult(ReasonKind.TargetMatch, "target match", value, valueId);
        }

        public static EvalResult RuleMatched(string value, string ruleName, string valueId)
        {
            return new EvalResult(ReasonKind.RuleMatch, $"match rule {ruleName}", value, valueId);
        }

        public static EvalResult Fallthrough(string value, string valueId)
        {
            return new EvalResult(ReasonKind.Fallthrough, "fall through targets and rules", value, valueId);
        }
    }

    public enum ReasonKind
    {
        /// <summary>
        /// Indicates that the caller tried to evaluate a flag before the client had successfully initialized.
        /// </summary>
        ClientNotReady,

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
        /// Indicates that the result value was not of the requested type, e.g. you requested a <see langword="bool"/>
        /// but the value was an <see langword="int"/>.
        /// </summary>
        WrongType,

        /// <summary>
        /// Indicates that the flag could not be evaluated, e.g. because it does not exist or due to an unexpected
        /// error. In this case the result value will be the default value that the caller passed to the client.
        /// </summary>
        Error
    }
}