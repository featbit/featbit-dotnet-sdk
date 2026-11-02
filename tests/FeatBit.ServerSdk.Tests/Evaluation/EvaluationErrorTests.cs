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
        var invalidRegex = CreateValidFlag("invalid-regex");
        var regexRule = invalidRegex.Rules.Single();
        regexRule.Conditions = new[]
        {
            new Condition
            {
                Property = "country",
                Op = "NotMatchRegex",
                Value = "["
            }
        };
        Add(invalidRegex);

        // Invalid JSON in a list condition.
        var invalidList = CreateValidFlag("invalid-list");
        var listRule = invalidList.Rules.Single();
        listRule.Conditions = new[]
        {
            new Condition
            {
                Property = "country",
                Op = "NotOneOf",
                Value = "["
            }
        };
        Add(invalidList);

        // Negative segment condition references a missing segment.
        var missingSegment = CreateValidFlag("missing-segment");
        var segmentRule = missingSegment.Rules.Single();
        segmentRule.Conditions = new[]
        {
            new Condition
            {
                Property = "User is not in segment",
                Op = "Equal",
                Value = "[\"missing\"]"
            }
        };
        Add(missingSegment);

        // Individual target references a missing variation.
        var missingTargetVariation = CreateValidFlag("missing-target-variation");
        var target = missingTargetVariation.TargetUsers.Single();
        target.KeyIds = new[] { "user" };
        target.VariationId = "missing";
        Add(missingTargetVariation);

        // Matched rule references a missing variation.
        var missingRuleVariation = CreateValidFlag("missing-rule-variation");
        var matchedRule = missingRuleVariation.Rules.Single();
        matchedRule.Variations.Single().Id = "missing";
        Add(missingRuleVariation);

        // Fallthrough references a missing variation; the valid rule does not match.
        var missingFallthroughVariation = CreateValidFlag("missing-fallthrough-variation");
        var nonMatchingRule = missingFallthroughVariation.Rules.Single();
        nonMatchingRule.Conditions = new[]
        {
            new Condition { Property = "country", Op = "Equal", Value = "de" }
        };
        missingFallthroughVariation.Fallthrough.Variations.Single().Id = "missing";
        Add(missingFallthroughVariation);

        // Selected variation has a null value.
        var nullVariationValue = CreateValidFlag("null-variation-value");
        nullVariationValue.Variations.Single(x => x.Id == "rule").Value = null;
        Add(nullVariationValue);

        // Enabled flag has a null target list.
        var nullTargets = CreateValidFlag("null-targets");
        nullTargets.TargetUsers = null;
        Add(nullTargets);
    }

    private static FeatureFlag CreateValidFlag(string key) => new FeatureFlagBuilder()
        .Key(key)
        .DisabledVariationId("fallthrough")
        .Variations(
            new Variation { Id = "rule", Value = "true" },
            new Variation { Id = "fallthrough", Value = "false" })
        .TargetUsers(new TargetUser { KeyIds = new[] { "target-user" }, VariationId = "rule" })
        .Rules(new TargetRule
        {
            Conditions = [new Condition { Property = "country", Op = "Equal", Value = "fr" }],
            Variations = [new RolloutVariation { Id = "rule", Rollout = new[] { 0d, 1d } }]
        })
        .Fallthrough(new Fallthrough
        {
            Variations = [new RolloutVariation { Id = "fallthrough", Rollout = new[] { 0d, 1d } }]
        })
        .Build();
}
