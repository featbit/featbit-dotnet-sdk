using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server.Evaluation;

public class EvaluationErrorTests
{
    private static readonly FbUser User = FbUser.Builder("user").Custom("country", "fr").Build();

    [Theory]
    [ClassData(typeof(MalformedFlagData))]
    public void EvaluatorReturnsErrorWithoutEventForMalformedFlag(object flag)
    {
        var broken = Assert.IsType<FeatureFlag>(flag);
        var store = new DefaultMemoryStore();
        store.Populate(new[] { broken });
        var evaluator = new Evaluator(store);

        var (result, evalEvent) = evaluator.Evaluate(
            new EvaluationContext { FlagKey = broken.Key, FbUser = User });

        Assert.Equal(ReasonKind.Error, result.Kind);
        Assert.Equal(string.Empty, result.Variation.Id);
        Assert.Null(evalEvent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void InvalidKeysReturnsInvalidInput(string key)
    {
        AssertRejectedWithoutStoreAccess(new EvaluationContext { FlagKey = key, FbUser = User });
        AssertRejectedWithoutStoreAccess(new EvaluationContext
        {
            FlagKey = "healthy",
            FbUser = FbUser.Builder(key).Build()
        });
    }

    [Fact]
    public void NullContextOrUserReturnsInvalidInput()
    {
        AssertRejectedWithoutStoreAccess(null);
        AssertRejectedWithoutStoreAccess(new EvaluationContext { FlagKey = "healthy", FbUser = null });
    }

    private static void AssertRejectedWithoutStoreAccess(EvaluationContext context)
    {
        var store = new Mock<IMemoryStore>(MockBehavior.Strict);
        var evaluator = new Evaluator(store.Object);

        var (result, evalEvent) = evaluator.Evaluate(context);

        Assert.Equal(ReasonKind.Error, result.Kind);
        Assert.Equal("invalid input", result.Reason);
        Assert.Equal(string.Empty, result.Variation.Id);
        Assert.Null(evalEvent);
        store.VerifyNoOtherCalls();
    }
}

public class MalformedFlagData : TheoryData<object>
{
    public MalformedFlagData()
    {
        // Invalid regex in a negative condition.
        var invalidRegex = new FeatureFlagBuilder()
            .Key("invalidRegex")
            .Rules(new TargetRule
            {
                Conditions = new[]
                {
                    new Condition { Property = "country", Op = "NotMatchRegex", Value = "[" }
                }
            })
            .Build();
        Add(invalidRegex);

        // Invalid JSON in a list condition.
        var invalidList = new FeatureFlagBuilder()
            .Key("invalidList")
            .Rules(new TargetRule
            {
                Conditions = new[]
                {
                    new Condition { Property = "country", Op = "NotOneOf", Value = "[" }
                }
            })
            .Build();
        Add(invalidList);

        // Negative segment condition references a missing segment.
        var missingSegment = new FeatureFlagBuilder()
            .Key("missingSegment")
            .Rules(new TargetRule
            {
                Conditions = new[]
                {
                    new Condition { Property = "User is not in segment", Op = "", Value = "[\"missing\"]" }
                }
            })
            .Build();
        Add(missingSegment);

        // Individual target references a missing variation.
        var missingTargetVariation = new FeatureFlagBuilder()
            .Key("missing-target-variation")
            .TargetUsers(new TargetUser { KeyIds = new[] { "user" }, VariationId = "missing" })
            .Build();
        Add(missingTargetVariation);

        // Matched rule references a missing variation.
        var missingRuleVariation = new FeatureFlagBuilder()
            .Key("missing-rule-variation")
            .Rules(new TargetRule
            {
                Conditions = Array.Empty<Condition>(),
                Variations = new[] { new RolloutVariation { Id = "missing", Rollout = new[] { 0d, 1d } } }
            })
            .Build();
        Add(missingRuleVariation);

        // Fallthrough references a missing variation.
        var missingFallthroughVariation = new FeatureFlagBuilder()
            .Key("missing-fallthrough-variation")
            .Fallthrough(new Fallthrough
            {
                Variations = new[] { new RolloutVariation { Id = "missing", Rollout = new[] { 0d, 1d } } }
            })
            .Build();
        Add(missingFallthroughVariation);

        // Selected variation has a null value.
        var nullVariationValue = new FeatureFlagBuilder()
            .Key("null-variation-value")
            .IsEnabled(false)
            .DisabledVariationId("v")
            .Variations(new Variation { Id = "v", Value = null })
            .Build();
        Add(nullVariationValue);

        // Enabled flag has a null target list.
        var nullTargets = new FeatureFlagBuilder()
            .Key("null-targets")
            .TargetUsers((List<TargetUser>)null)
            .Build();
        Add(nullTargets);
    }
}
