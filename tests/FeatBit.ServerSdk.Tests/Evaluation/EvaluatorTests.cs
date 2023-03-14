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

        var (evalResult, evalEvent) = evaluator.Evaluate(context);

        Assert.Equal(ReasonKind.Error, evalResult.Kind);
        Assert.Equal(string.Empty, evalResult.Value);
        Assert.Equal("flag not found", evalResult.Reason);

        Assert.Null(evalEvent);
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

        var (evalResult, evalEvent) = evaluator.Evaluate(context);

        Assert.Equal(ReasonKind.Error, evalResult.Kind);
        Assert.Equal(string.Empty, evalResult.Value);
        Assert.Equal("malformed flag", evalResult.Reason);

        Assert.Null(evalEvent);
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

        var (evalResult, evalEvent) = evaluator.Evaluate(context);

        Assert.Equal(ReasonKind.Off, evalResult.Kind);
        Assert.Equal("true", evalResult.Value);
        Assert.Equal("flag off", evalResult.Reason);

        // flag is off
        Assert.False(evalEvent.SendToExperiment);
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

        var (evalResult, evalEvent) = evaluator.Evaluate(context);

        Assert.Equal(ReasonKind.TargetMatch, evalResult.Kind);
        Assert.Equal("false", evalResult.Value);
        Assert.Equal("target match", evalResult.Reason);

        // ExptIncludeAllTargets is true by default
        Assert.True(evalEvent.SendToExperiment);
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
            IncludedInExpt = false,
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
            .ExptIncludeAllTargets(false)
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

        var (evalResult, evalEvent) = evaluator.Evaluate(context);

        Assert.Equal(ReasonKind.RuleMatch, evalResult.Kind);
        Assert.Equal("true", evalResult.Value);
        Assert.Equal($"match rule {customRule.Name}", evalResult.Reason);

        // customRule.IncludedInExpt is false
        Assert.False(evalEvent.SendToExperiment);
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
            .ExptIncludeAllTargets(false)
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

        var (evalResult, evalEvent) = evaluator.Evaluate(context);

        Assert.Equal(ReasonKind.Fallthrough, evalResult.Kind);
        Assert.Equal("false", evalResult.Value);
        Assert.Equal("fall through targets and rules", evalResult.Reason);

        Assert.True(evalEvent.SendToExperiment);
    }
}