using FeatBit.Sdk.Server.Model;

namespace FeatBit.Sdk.Server.Evaluation
{
    internal class EvalResult
    {
        public ReasonKind Kind { get; set; }

        public string Reason { get; set; }

        public Variation Variation { get; set; }

        private EvalResult(ReasonKind kind, string reason, Variation variation)
        {
            Kind = kind;
            Reason = reason;
            Variation = variation;
        }

        // Indicates that the caller provided a flag key that did not match any known flag.
        public static readonly EvalResult FlagNotFound = new(ReasonKind.Error, "flag not found", Variation.Empty);

        // Indicates that there was an internal inconsistency in the flag data, e.g. a rule specified a nonexistent
        // variation. 
        public static readonly EvalResult MalformedFlag = new(ReasonKind.Error, "malformed flag", Variation.Empty);

        public static EvalResult FlagOff(Variation variation)
        {
            return new EvalResult(ReasonKind.Off, "flag off", variation);
        }

        public static EvalResult Targeted(Variation variation)
        {
            return new EvalResult(ReasonKind.TargetMatch, "target match", variation);
        }

        public static EvalResult RuleMatched(string ruleName, Variation value)
        {
            return new EvalResult(ReasonKind.RuleMatch, $"match rule {ruleName}", value);
        }

        public static EvalResult Fallthrough(Variation variation)
        {
            return new EvalResult(ReasonKind.Fallthrough, "fall through targets and rules", variation);
        }

        public EvalDetail<string> AsEvalDetail(string key)
        {
            return new EvalDetail<string>(key, Kind, Reason, Variation.Value, Variation.Id);
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