using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server.Evaluation;

public class EvaluatorTests
{
    [Fact]
    public void EvaluateFlagNotFound()
    {
        var store = new DefaultMemoryStore();
        var evaluator = new Evaluator(store);
        var context = new EvaluationContext
        {
            FlagKey = "hello",
            FbUser = new FbUserBuilder("u1").Build()
        };

        var (result, _) = evaluator.Evaluate(context);

        Assert.Equal(ReasonKind.Error, result.Kind);
        Assert.Equal(string.Empty, result.Value);
        Assert.Equal("flag not found", result.Reason);
    }

    [Fact]
    public void EvaluateMalformedFlag()
    {
        var store = new DefaultMemoryStore();
        var malformedFlag = new FeatureFlagBuilder()
            .Key("hello")
            .IsEnabled(false)
            .DisabledVariationId("not-exist-variation-id")
            .Variations(new Variation { Id = "trueId", Value = "true" })
            .Build();
        store.Populate(new[] { malformedFlag });

        var evaluator = new Evaluator(store);
        var context = new EvaluationContext
        {
            FlagKey = "hello",
            FbUser = new FbUserBuilder("u1").Build()
        };

        var (result, _) = evaluator.Evaluate(context);

        Assert.Equal(ReasonKind.Error, result.Kind);
        Assert.Equal(string.Empty, result.Value);
        Assert.Equal("malformed flag", result.Reason);
    }

    [Fact]
    public void EvaluateFlagOffResult()
    {
        var store = new DefaultMemoryStore();
        var flag = new FeatureFlagBuilder()
            .Key("hello")
            .IsEnabled(false)
            .DisabledVariationId("trueId")
            .Variations(new Variation { Id = "trueId", Value = "true" })
            .Build();
        store.Populate(new[] { flag });

        var evaluator = new Evaluator(store);
        var context = new EvaluationContext
        {
            FlagKey = "hello",
            FbUser = new FbUserBuilder("u1").Build()
        };

        var (result, _) = evaluator.Evaluate(context);

        Assert.Equal(ReasonKind.Off, result.Kind);
        Assert.Equal("true", result.Value);
        Assert.Equal("flag off", result.Reason);
    }

    [Fact]
    public void EvaluateTargetedResult()
    {
        var store = new DefaultMemoryStore();

        var variations = new Variation[]
        {
            new() { Id = "trueId", Value = "true" },
            new() { Id = "falseId", Value = "false" }
        };
        var targetUser = new TargetUser
        {
            VariationId = "falseId", KeyIds = new List<string> { "u1" }
        };

        var flag = new FeatureFlagBuilder()
            .Key("hello")
            .IsEnabled(true)
            .TargetUsers(targetUser)
            .Variations(variations)
            .Build();
        store.Populate(new[] { flag });

        var evaluator = new Evaluator(store);
        var context = new EvaluationContext
        {
            FlagKey = "hello",
            FbUser = new FbUserBuilder("u1").Build()
        };

        var (result, _) = evaluator.Evaluate(context);

        Assert.Equal(ReasonKind.TargetMatch, result.Kind);
        Assert.Equal("false", result.Value);
        Assert.Equal("target match", result.Reason);
    }

    [Fact]
    public void EvaluatorRuleMatchedResult()
    {
        var store = new DefaultMemoryStore();

        var variations = new Variation[]
        {
            new() { Id = "trueId", Value = "true" },
            new() { Id = "falseId", Value = "false" }
        };
        var customRule = new TargetRule
        {
            Name = "open for vip & svip",
            Conditions = new List<Condition>
            {
                new()
                {
                    Property = "level",
                    Op = OperatorTypes.IsOneOf,
                    Value = "[\"vip\",\"svip\"]"
                }
            },
            DispatchKey = "keyId",
            IncludedInExpt = true,
            Variations = new List<RolloutVariation>
            {
                new()
                {
                    Id = "trueId",
                    ExptRollout = 1,
                    Rollout = new double[] { 0, 1 }
                }
            }
        };

        var flag = new FeatureFlagBuilder()
            .Key("hello")
            .IsEnabled(true)
            .Rules(customRule)
            .Variations(variations)
            .Build();
        store.Populate(new[] { flag });

        var evaluator = new Evaluator(store);
        var context = new EvaluationContext
        {
            FlagKey = "hello",
            FbUser = new FbUserBuilder("u1")
                .Custom("level", "svip")
                .Build()
        };

        var (result, _) = evaluator.Evaluate(context);

        Assert.Equal(ReasonKind.RuleMatch, result.Kind);
        Assert.Equal("true", result.Value);
        Assert.Equal($"match rule {customRule.Name}", result.Reason);
    }

    [Fact]
    public void EvaluateFallthroughResult()
    {
        var store = new DefaultMemoryStore();

        var variations = new Variation[]
        {
            new() { Id = "trueId", Value = "true" },
            new() { Id = "falseId", Value = "false" }
        };

        var fallthrough = new Fallthrough
        {
            DispatchKey = "keyId",
            IncludedInExpt = true,
            Variations = new List<RolloutVariation>
            {
                new()
                {
                    Id = "falseId",
                    ExptRollout = 1,
                    Rollout = new double[] { 0, 1 }
                }
            }
        };

        var flag = new FeatureFlagBuilder()
            .Key("hello")
            .IsEnabled(true)
            .Fallthrough(fallthrough)
            .Variations(variations)
            .Build();
        store.Populate(new[] { flag });

        var evaluator = new Evaluator(store);
        var context = new EvaluationContext
        {
            FlagKey = "hello",
            FbUser = new FbUserBuilder("u1").Build()
        };

        var (result, _) = evaluator.Evaluate(context);

        Assert.Equal(ReasonKind.Fallthrough, result.Kind);
        Assert.Equal("false", result.Value);
        Assert.Equal("fall through targets and rules", result.Reason);
    }
}