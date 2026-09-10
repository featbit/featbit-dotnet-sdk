using FeatBit.Sdk.Server.DataSynchronizer;
using FeatBit.Sdk.Server.Events;
using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Options;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server.Evaluation;

public class EvaluationErrorTests
{
    private static readonly FbUser User = FbUser.Builder("user").Custom("country", "fr").Build();

    [Theory]
    [InlineData("regex")]
    [InlineData("list")]
    [InlineData("segment")]
    [InlineData("variation")]
    [InlineData("rule-variation")]
    [InlineData("fallthrough-variation")]
    [InlineData("null-value")]
    [InlineData("targets")]
    public void MalformedFlagReturnsExactFallbackAndDoesNotBreakBulk(string failure)
    {
        var broken = Flag("broken");
        switch (failure)
        {
            case "variation":
                broken.IsEnabled = true;
                broken.TargetUsers = new[]
                {
                    new TargetUser { KeyIds = new[] { User.Key }, VariationId = "missing" }
                };
                break;
            case "rule-variation":
                broken.IsEnabled = true;
                broken.Rules = new[]
                {
                    new TargetRule
                    {
                        Conditions = Array.Empty<Condition>(),
                        Variations = new[] { new RolloutVariation { Id = "missing", Rollout = new[] { 0d, 1d } } }
                    }
                };
                break;
            case "fallthrough-variation":
                broken.IsEnabled = true;
                broken.Fallthrough = new Fallthrough
                {
                    Variations = new[] { new RolloutVariation { Id = "missing", Rollout = new[] { 0d, 1d } } }
                };
                break;
            case "null-value":
                broken.Variations.Single().Value = null;
                break;
            case "targets":
                broken.IsEnabled = true;
                broken.TargetUsers = null;
                break;
            default:
                broken.IsEnabled = true;
                broken.Rules = new[]
                {
                    new TargetRule
                    {
                        Conditions = new[]
                        {
                            new Condition
                            {
                                Property = failure == "segment" ? "User is not in segment" : "country",
                                Op = failure == "regex" ? "NotMatchRegex" : "NotOneOf",
                                Value = failure == "segment" ? "[\"missing\"]" : "["
                            }
                        }
                    }
                };
                break;
        }

        var events = new Mock<IEventProcessor>();
        var store = new DefaultMemoryStore();
        store.Populate(new[] { broken, Flag("healthy") });
        var evaluator = new Evaluator(store);
        var direct = evaluator.Evaluate(broken, User);
        Assert.Equal(ReasonKind.Error, direct.evalResult.Kind);
        Assert.Equal(string.Empty, direct.evalResult.Variation.Id);
        Assert.Null(direct.evalEvent);
        var lookup = evaluator.Evaluate(new EvaluationContext { FlagKey = "broken", FbUser = User });
        Assert.Equal(ReasonKind.Error, lookup.evalResult.Kind);
        Assert.Null(lookup.evalEvent);
        var bulk = evaluator.EvaluateAll(User);
        Assert.Equal(2, bulk.Length);
        Assert.Equal("true", Assert.Single(bulk, x => x.Key == "healthy").Value);
        var client = Client(store, events.Object);
        var fallback = new string('x', 3);

        var detail = client.StringVariationDetail("broken", User, fallback);
        Assert.Same(fallback, detail.Value);
        Assert.Equal(ReasonKind.Error, detail.Kind);
        Assert.Equal(string.Empty, detail.ValueId);
        Assert.False(client.BoolVariation("broken", User, false));
        events.Verify(x => x.Record(It.IsAny<IEvent>()), Times.Never);

        var results = client.GetAllVariations(User);
        Assert.Equal(2, results.Length);
        var error = Assert.Single(results, x => x.Key == "broken");
        Assert.Equal(ReasonKind.Error, error.Kind);
        Assert.Equal(string.Empty, error.Value);
        Assert.Equal(string.Empty, error.ValueId);
        Assert.Equal("true", Assert.Single(results, x => x.Key == "healthy").Value);
        events.Verify(x => x.Record(It.IsAny<IEvent>()), Times.Never);
        Assert.True(client.BoolVariation("healthy", User));
        Assert.True(client.Initialized);
        Assert.Equal(FbClientStatus.Ready, client.Status);
        Assert.Same(broken, store.Get<FeatureFlag>(StoreKeys.ForFeatureFlag("broken")));
    }

    [Fact]
    public void WrongTypeAndNotReadyKeepTheirReasonsAndDoNotRecordEvents()
    {
        var events = new Mock<IEventProcessor>();
        var store = new DefaultMemoryStore();
        store.Populate(new[] { Flag("healthy") });
        var client = Client(store, events.Object);
        var wrongType = client.IntVariationDetail("healthy", User, 42);
        Assert.Equal(42, wrongType.Value);
        Assert.Equal(ReasonKind.WrongType, wrongType.Kind);
        Assert.Equal(string.Empty, wrongType.ValueId);

        var synchronizer = new Mock<IDataSynchronizer>();
        synchronizer.Setup(x => x.StartAsync()).Returns(Task.FromResult(false));
        var unready = new FbClient(FbOptions.Default("unused"), store, synchronizer.Object, events.Object);
        var result = unready.IntVariationDetail("healthy", User, 42);
        Assert.Equal(42, result.Value);
        Assert.Equal(ReasonKind.ClientNotReady, result.Kind);
        Assert.Equal(string.Empty, result.ValueId);
        Assert.Equal("true", Assert.Single(unready.GetAllVariations(User)).Value);
        events.Verify(x => x.Record(It.IsAny<IEvent>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EvaluatorRejectsInvalidKeysWithoutAccessingStore(string key)
    {
        var store = new Mock<IMemoryStore>(MockBehavior.Strict);
        var evaluator = new Evaluator(store.Object);
        var invalidUser = FbUser.Builder(key).Build();

        AssertInvalid(evaluator.Evaluate(new EvaluationContext { FlagKey = key, FbUser = User }));
        AssertInvalid(evaluator.Evaluate(new EvaluationContext { FlagKey = "healthy", FbUser = invalidUser }));

        var client = Client(store.Object, new NullEventProcessor());
        Assert.Empty(client.GetAllVariations(invalidUser));
        var detail = client.IntVariationDetail("healthy", invalidUser, 42);
        Assert.Equal(42, detail.Value);
        Assert.Equal("invalid input", detail.Reason);
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public void EvaluatorRejectsNullContextAndUser()
    {
        var evaluator = new Evaluator(new Mock<IMemoryStore>(MockBehavior.Strict).Object);
        AssertInvalid(evaluator.Evaluate(null));
        AssertInvalid(evaluator.Evaluate(new EvaluationContext { FlagKey = "healthy", FbUser = null }));
    }

    private static void AssertInvalid((EvalResult evalResult, EvalEvent evalEvent) result)
    {
        Assert.Equal(ReasonKind.Error, result.evalResult.Kind);
        Assert.Equal("invalid input", result.evalResult.Reason);
        Assert.Equal(string.Empty, result.evalResult.Variation.Id);
        Assert.Null(result.evalEvent);
    }

    private static FeatureFlag Flag(string key) => new FeatureFlagBuilder()
        .Key(key).IsEnabled(false).DisabledVariationId("v")
        .Variations(new Variation { Id = "v", Value = "true" }).Build();

    private static FbClient Client(IMemoryStore store, IEventProcessor events) =>
        new(FbOptions.Default("unused"), store, new NullDataSynchronizer(), events);
}
