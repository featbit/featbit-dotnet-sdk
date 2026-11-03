using FeatBit.Sdk.Server.Evaluation;
using FeatBit.Sdk.Server.Events;
using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Options;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server;

public partial class FbClientTests
{
    [Fact]
    public void GetVariation()
    {
        var eventProcessorMock = new Mock<IEventProcessor>();
        var client = CreateTestFbClient(eventProcessorMock.Object);

        var user = FbUser.Builder("u1").Build();
        var variation = client.BoolVariation("returns-true", user);
        Assert.True(variation);

        eventProcessorMock.Verify(x => x.Record(It.IsAny<IEvent>()), Times.Once);
    }

    [Fact]
    public void GetVariationDetail()
    {
        var eventProcessorMock = new Mock<IEventProcessor>();
        var client = CreateTestFbClient(eventProcessorMock.Object);

        var user = FbUser.Builder("u1").Build();
        var variationDetail = client.BoolVariationDetail("returns-true", user);
        Assert.Equal("returns-true", variationDetail.Key);
        Assert.True(variationDetail.Value);
        Assert.Equal("3da96792-debf-4878-905a-c9b5f9178cd0", variationDetail.ValueId);
        Assert.Equal(ReasonKind.Fallthrough, variationDetail.Kind);
        Assert.Equal("fall through targets and rules", variationDetail.Reason);

        eventProcessorMock.Verify(x => x.Record(It.IsAny<IEvent>()), Times.Once);
    }

    [Fact]
    public void GetAllVariations()
    {
        var client = CreateTestFbClient();
        var user = FbUser.Builder("u1").Build();

        var results = client.GetAllVariations(user);
        Assert.Single(results);

        var result0 = results[0];
        Assert.Equal("returns-true", result0.Key);
        Assert.Equal("true", result0.Value);
        Assert.Equal("3da96792-debf-4878-905a-c9b5f9178cd0", result0.ValueId);
        Assert.Equal(ReasonKind.Fallthrough, result0.Kind);
        Assert.Equal("fall through targets and rules", result0.Reason);
    }

    [Fact]
    public async Task MalformedFlagReturnsFallbackWithoutRecordingEvent()
    {
        var events = new Mock<IEventProcessor>();
        var store = new DefaultMemoryStore();
        store.Populate(new[] { CreateEvaluationFlag("broken", "[") });
        var client = new FbClient(FbOptions.Default("unused"), store,
            new ManualDataSynchronizer(), events.Object);
        var user = FbUser.Builder("user").Custom("country", "fr").Build();
        var fallback = new string('x', 3);

        try
        {
            var detail = client.StringVariationDetail("broken", user, fallback);

            Assert.Same(fallback, detail.Value);
            Assert.Equal(ReasonKind.Error, detail.Kind);
            Assert.Equal(string.Empty, detail.ValueId);
            events.Verify(x => x.Record(It.IsAny<IEvent>()), Times.Never);
        }
        finally
        {
            await client.CloseAsync();
        }
    }

    [Fact]
    public async Task GetAllVariationsIsolatesMalformedFlagWithoutRecordingEvents()
    {
        var events = new Mock<IEventProcessor>();
        var store = new DefaultMemoryStore();
        store.Populate(new[]
        {
            CreateEvaluationFlag("broken", "["),
            CreateEvaluationFlag("healthy", "fr")
        });
        var client = new FbClient(FbOptions.Default("unused"), store,
            new ManualDataSynchronizer(), events.Object);
        var user = FbUser.Builder("user").Custom("country", "fr").Build();

        try
        {
            var results = client.GetAllVariations(user);

            Assert.Equal(2, results.Length);
            var error = Assert.Single(results, x => x.Key == "broken");
            Assert.Equal(ReasonKind.Error, error.Kind);
            Assert.Equal(string.Empty, error.Value);
            Assert.Equal(string.Empty, error.ValueId);
            var healthy = Assert.Single(results, x => x.Key == "healthy");
            Assert.Equal(ReasonKind.RuleMatch, healthy.Kind);
            Assert.Equal("true", healthy.Value);
            Assert.Equal("on", healthy.ValueId);
            events.Verify(x => x.Record(It.IsAny<IEvent>()), Times.Never);
        }
        finally
        {
            await client.CloseAsync();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InsightsEnabledControlsEvaluationEvents(bool insightsEnabled)
    {
        var events = new Mock<IEventProcessor>();
        var flag = CreateEvaluationFlag("flag", "fr");
        flag.InsightsEnabled = insightsEnabled;
        var store = new DefaultMemoryStore();
        store.Populate(new[] { flag });
        var client = new FbClient(FbOptions.Default("unused"), store,
            new ManualDataSynchronizer(), events.Object);

        try
        {
            client.BoolVariation("flag", FbUser.Builder("user").Build());

            events.Verify(x => x.Record(It.IsAny<EvalEvent>()), insightsEnabled ? Times.Once() : Times.Never());
        }
        finally
        {
            await client.CloseAsync();
        }
    }

    private static FeatureFlag CreateEvaluationFlag(string key, string pattern) => new FeatureFlagBuilder()
        .Key(key)
        .Variations(new Variation { Id = "on", Value = "true" },
            new Variation { Id = "off", Value = "false" })
        .Rules(new TargetRule
        {
            Conditions = new[] { new Condition { Property = "country", Op = "MatchRegex", Value = pattern } },
            Variations = new[] { new RolloutVariation { Id = "on", Rollout = new[] { 0d, 1d } } }
        })
        .Fallthrough(new Fallthrough
        {
            Variations = new[] { new RolloutVariation { Id = "off", Rollout = new[] { 0d, 1d } } }
        })
        .Build();
}
