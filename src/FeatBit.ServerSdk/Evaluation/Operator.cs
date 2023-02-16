using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FeatBit.Sdk.Server.Evaluation
{
    public class Operator
    {
        private string Operation { get; }
        private Func<string, string, bool> Func { get; }

        private Operator(string operation, Func<string, string, bool> func)
        {
            Operation = operation;
            Func = func;
        }

        public bool IsMatch(string userValue, string conditionValue)
        {
            if (userValue == null || conditionValue == null)
            {
                return false;
            }

            return Func(userValue, conditionValue);
        }

        #region numeric (rule value is a number)

        public static readonly Operator LessThan =
            new Operator(OperatorTypes.LessThan, NumericOperator(-1, -1));

        public static readonly Operator LessEqualThan =
            new Operator(OperatorTypes.LessEqualThan, NumericOperator(-1, 0));

        public static readonly Operator BiggerThan =
            new Operator(OperatorTypes.BiggerThan, NumericOperator(1, 1));

        public static readonly Operator BiggerEqualThan =
            new Operator(OperatorTypes.BiggerEqualThan, NumericOperator(1, 0));

        private static Func<string, string, bool> NumericOperator(
            int desiredComparisonResult,
            int otherDesiredComparisonResult
        ) => (userValue, ruleValue) =>
        {
            // not a number, return false
            if (!double.TryParse(userValue, out var userDoubleValue) ||
                !double.TryParse(ruleValue, out var ruleDoubleValue))
            {
                return false;
            }

            // is NaN, return false
            if (double.IsNaN(userDoubleValue) || double.IsNaN(ruleDoubleValue))
            {
                return false;
            }

            var result = userDoubleValue.CompareTo(ruleDoubleValue);
            return result == desiredComparisonResult || result == otherDesiredComparisonResult;
        };

        #endregion

        #region string compare (rule value is a string)

        public static readonly Operator Equal = new Operator(
            OperatorTypes.Equal,
            (userValue, ruleValue) => string.Equals(userValue, ruleValue, StringComparison.Ordinal)
        );

        public static readonly Operator NotEqual = new Operator(
            OperatorTypes.NotEqual,
            (userValue, ruleValue) => !string.Equals(userValue, ruleValue, StringComparison.Ordinal)
        );

        #endregion

        #region string contains/not contains (rule value is a string)

        public static readonly Operator Contains =
            new Operator(OperatorTypes.Contains, (userValue, ruleValue) => userValue.Contains(ruleValue));

        public static readonly Operator NotContains =
            new Operator(OperatorTypes.NotContain, (userValue, ruleValue) => !userValue.Contains(ruleValue));

        #endregion

        #region string starts with/ends with (rule value is a string)

        public static readonly Operator StartsWith =
            new Operator(OperatorTypes.StartsWith, (userValue, ruleValue) => userValue.StartsWith(ruleValue));

        public static readonly Operator EndsWith =
            new Operator(OperatorTypes.EndsWith, (userValue, ruleValue) => userValue.EndsWith(ruleValue));

        #endregion

        #region string match regex (rule value is a regex)

        public static readonly Operator MatchRegex = new Operator(OperatorTypes.MatchRegex, Regex.IsMatch);

        public static readonly Operator NotMatchRegex =
            new Operator(OperatorTypes.NotMatchRegex, (userValue, ruleValue) => !Regex.IsMatch(userValue, ruleValue));

        #endregion

        #region is one of/ not one of (rule value is a list of strings)

        public static readonly Operator IsOneOf =
            new Operator(OperatorTypes.IsOneOf, (userValue, ruleValue) =>
            {
                var ruleValues = JsonSerializer.Deserialize<List<string>>(ruleValue);

                return ruleValues?.Contains(userValue) ?? false;
            });

        public static readonly Operator NotOneOf =
            new Operator(OperatorTypes.NotOneOf, (userValue, ruleValue) =>
            {
                var ruleValues = JsonSerializer.Deserialize<List<string>>(ruleValue);

                return !ruleValues?.Contains(userValue) ?? true;
            });

        #endregion

        #region is true/ is false (no rule value)

        public static readonly Operator IsTrue = new Operator(
            OperatorTypes.IsTrue,
            (userValue, _) => userValue.Equals("TRUE", StringComparison.OrdinalIgnoreCase)
        );

        public static readonly Operator IsFalse = new Operator(
            OperatorTypes.IsFalse,
            (userValue, _) => userValue.Equals("FALSE", StringComparison.OrdinalIgnoreCase)
        );

        #endregion

        public static IEnumerable<Operator> All => new[]
        {
            // numeric
            LessThan, LessEqualThan, BiggerThan, BiggerEqualThan,

            // string compare
            Equal, NotEqual,

            // string contains/not contains
            Contains, NotContains,

            // string starts with/ends with
            StartsWith, EndsWith,

            // string match regex/not match regex
            MatchRegex, NotMatchRegex,

            // is one of/ not one of
            IsOneOf, NotOneOf,

            // is true/ is false
            IsTrue, IsFalse
        };

        public static Operator Get(string operation)
        {
            var theOperator = All.FirstOrDefault(x => x.Operation == operation);

            // unrecognized operators are treated as non-matches, not errors
            return theOperator ?? new Operator(operation, (uv, rv) => false);
        }
    }
}