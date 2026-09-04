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

    [Theory]
    [InlineData(OperatorTypes.Equal)]
    [InlineData(OperatorTypes.NotEqual)]
    [InlineData(OperatorTypes.Contains)]
    [InlineData(OperatorTypes.NotContain)]
    [InlineData(OperatorTypes.MatchRegex)]
    [InlineData(OperatorTypes.NotMatchRegex)]
    [InlineData(OperatorTypes.IsOneOf)]
    [InlineData(OperatorTypes.NotOneOf)]
    [InlineData("UnknownOperator")]
    public void MissingAttributeDoesNotMatchRegularOperator(string op)
    {
        var condition = new Condition
        {
            Property = "missing",
            Op = op,
            Value = op is OperatorTypes.IsOneOf or OperatorTypes.NotOneOf ? "[\"\"]" : string.Empty
        };

        var user = FbUser.Builder("user-key").Build();

        Assert.False(Evaluator.IsMatchCondition(condition, user));
    }

    [Theory]
    [InlineData(OperatorTypes.Equal, true)]
    [InlineData(OperatorTypes.NotEqual, false)]
    [InlineData(OperatorTypes.Contains, true)]
    [InlineData(OperatorTypes.NotContain, false)]
    public void EmptyAttributeValueIsComparedNormally(string op, bool expected)
    {
        CheckMatch(string.Empty, op, string.Empty, expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespacePropertyNeverMatches(string property)
    {
        var condition = new Condition
        {
            Property = property,
            Op = OperatorTypes.NotEqual,
            Value = string.Empty
        };

        var user = FbUser.Builder("user-key").Build();

        Assert.False(Evaluator.IsMatchCondition(condition, user));
    }

    [Theory]
    [InlineData(FbUser.KeyIdAttribute, "built-in-key")]
    [InlineData(FbUser.NameAttribute, "Built-in Name")]
    public void BuiltInAttributeTakesPriorityOverCustomAttribute(string property, string expected)
    {
        var condition = new Condition
        {
            Property = property,
            Op = OperatorTypes.Equal,
            Value = expected
        };

        var user = FbUser.Builder("built-in-key")
            .Name("Built-in Name")
            .Custom(property, "custom-value")
            .Build();

        Assert.True(Evaluator.IsMatchCondition(condition, user));
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
