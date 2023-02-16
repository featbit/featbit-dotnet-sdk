using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server.Evaluation;

public class SegmentMatcherTests
{
    [Fact]
    public void MatchSegment()
    {
        var rule = new MatchRule
        {
            Conditions = new List<Condition>
            {
                new()
                {
                    Property = "age",
                    Op = OperatorTypes.Equal,
                    Value = "10"
                },
                new()
                {
                    Property = "country",
                    Op = OperatorTypes.Equal,
                    Value = "us"
                },
            }
        };

        var segment = new SegmentBuilder()
            .Included("u1")
            .Excluded("u2")
            .Rules(rule)
            .Build();

        var u1 = FbUser.Builder("u1").Build();
        var u2 = FbUser.Builder("u2").Build();

        var u3 = FbUser.Builder("u3")
            .Custom("age", "10")
            .Custom("country", "us")
            .Build();

        var u4 = FbUser.Builder("u4")
            .Custom("age", "10")
            .Custom("country", "eu")
            .Build();

        Assert.True(Evaluator.IsMatchSegment(segment, u1));
        Assert.False(Evaluator.IsMatchSegment(segment, u2));
        Assert.True(Evaluator.IsMatchSegment(segment, u3));
        Assert.False(Evaluator.IsMatchSegment(segment, u4));
    }

    [Fact]
    public void MatchAnySegment()
    {
        var store = new DefaultMemoryStore();

        var segmentId = Guid.NewGuid();
        var segment = new SegmentBuilder()
            .Id(segmentId)
            .Included("u1")
            .Build();
        store.Populate(new[] { segment });

        var evaluator = new Evaluator(store);

        var segmentCondition = new Condition
        {
            Property = Evaluator.IsInSegmentProperty,
            Op = string.Empty,
            Value = $"[\"{segmentId}\"]",
        };
        var user = FbUser.Builder("u1").Build();

        Assert.True(evaluator.IsMatchAnySegment(segmentCondition, user));
    }
}