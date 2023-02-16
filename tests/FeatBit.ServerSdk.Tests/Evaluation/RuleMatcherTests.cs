using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server.Evaluation;

public class RuleMatcherTests
{
    [Fact]
    public void MatchRule()
    {
        var segmentId = Guid.NewGuid();
        var segment = new SegmentBuilder()
            .Id(segmentId)
            .Included("u1")
            .Excluded("u2", "u3")
            .Build();

        var store = new DefaultMemoryStore();
        store.Populate(new[] { segment });

        var evaluator = new Evaluator(store);

        var rule = new TargetRule
        {
            Name = "test",
            Conditions = new List<Condition>
            {
                new()
                {
                    Property = Evaluator.IsNotInSegmentProperty,
                    Op = string.Empty,
                    Value = $"[\"{segmentId}\"]"
                },
                new()
                {
                    Property = "age",
                    Op = OperatorTypes.Equal,
                    Value = "10"
                }
            }
        };

        var u1 = new FbUserBuilder("u1").Build();
        var u2 = new FbUserBuilder("u2")
            .Custom("age", "11")
            .Build();
        var u3 = new FbUserBuilder("u3")
            .Custom("age", "10")
            .Build();

        Assert.False(evaluator.IsMatchRule(rule, u1));
        Assert.False(evaluator.IsMatchRule(rule, u2));
        Assert.True(evaluator.IsMatchRule(rule, u3));
    }
}