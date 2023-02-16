using FeatBit.Sdk.Server.Model;

namespace FeatBit.Sdk.Server.Evaluation;

public class ConditionMatcherTests
{
    // note: 
    // uv = userValue
    // op = operation
    // rv = ruleValue

    [Theory]
    [InlineData("10", OperatorTypes.BiggerThan, "9", true)]
    [InlineData("10", OperatorTypes.BiggerThan, "11", false)]
    [InlineData("10", OperatorTypes.BiggerEqualThan, "10", true)]
    [InlineData("10", OperatorTypes.BiggerEqualThan, "11", false)]
    [InlineData("10", OperatorTypes.LessThan, "11", true)]
    [InlineData("10", OperatorTypes.LessThan, "9", false)]
    [InlineData("10", OperatorTypes.LessEqualThan, "10", true)]
    [InlineData("10", OperatorTypes.LessEqualThan, "9", false)]
    public void MatchNumeric(string uv, string op, string rv, bool expected)
    {
        CheckMatch(uv, op, rv, expected);
    }

    [Theory]
    [InlineData("v1.0.0", OperatorTypes.Equal, "v1.0.0", true)]
    [InlineData("v1.1.0", OperatorTypes.Equal, "v1.0.0", false)]
    [InlineData("v1.1.0", OperatorTypes.NotEqual, "v1.1.0", false)]
    [InlineData("v1.1.0", OperatorTypes.NotEqual, "v1.0.0", true)]
    public void MatchEquality(string uv, string op, string rv, bool expected)
    {
        CheckMatch(uv, op, rv, expected);
    }

    [Theory]
    [InlineData("vvip", OperatorTypes.Contains, "vip", true)]
    [InlineData("vvip", OperatorTypes.Contains, "sv", false)]
    [InlineData("svip", OperatorTypes.NotContain, "vv", true)]
    [InlineData("svip", OperatorTypes.NotContain, "vip", false)]
    public void MatchContainsOrNot(string uv, string op, string rv, bool expected)
    {
        CheckMatch(uv, op, rv, expected);
    }

    [Theory]
    [InlineData("abc", OperatorTypes.StartsWith, "ab", true)]
    [InlineData("abc", OperatorTypes.StartsWith, "b", false)]
    [InlineData("abc", OperatorTypes.EndsWith, "bc", true)]
    [InlineData("abc", OperatorTypes.EndsWith, "cd", false)]
    public void MatchStartsOrEndsWith(string uv, string op, string rv, bool expected)
    {
        CheckMatch(uv, op, rv, expected);
    }

    [Theory]
    [InlineData("color", OperatorTypes.MatchRegex, "colou?r", true)]
    [InlineData("colour", OperatorTypes.MatchRegex, "colorr?", false)]
    [InlineData("colouur", OperatorTypes.NotMatchRegex, "colou?r", true)]
    [InlineData("color", OperatorTypes.NotMatchRegex, "colou?r", false)]
    public void MatchRegexOrNot(string uv, string op, string rv, bool expected)
    {
        CheckMatch(uv, op, rv, expected);
    }

    [Theory]
    [InlineData("a", OperatorTypes.IsOneOf, "[\"a\", \"b\"]", true)]
    [InlineData("c", OperatorTypes.IsOneOf, "[\"a\", \"b\"]", false)]
    [InlineData("c", OperatorTypes.NotOneOf, "[\"a\", \"b\"]", true)]
    [InlineData("a", OperatorTypes.NotOneOf, "[\"a\", \"b\"]", false)]
    public void MatchIsOneOf(string uv, string op, string rv, bool expected)
    {
        CheckMatch(uv, op, rv, expected);
    }

    [Theory]
    [InlineData("true", OperatorTypes.IsTrue, "", true)]
    [InlineData("TRue", OperatorTypes.IsTrue, "", true)]
    [InlineData("false", OperatorTypes.IsFalse, "", true)]
    [InlineData("falSE", OperatorTypes.IsFalse, "", true)]
    [InlineData("not-true-string", OperatorTypes.IsTrue, "", false)]
    [InlineData("not-false-string", OperatorTypes.IsFalse, "", false)]
    public void MatchTrueFalse(string uv, string op, string rv, bool expected)
    {
        CheckMatch(uv, op, rv, expected);
    }

    private static void CheckMatch(string uv, string op, string rv, bool expected)
    {
        var condition = new Condition
        {
            Property = "prop",
            Op = op,
            Value = rv
        };

        var user = FbUser.Builder("nope")
            .Custom("prop", uv)
            .Build();

        var isMatch = Evaluator.IsMatchCondition(condition, user);
        Assert.Equal(expected, isMatch);
    }
}