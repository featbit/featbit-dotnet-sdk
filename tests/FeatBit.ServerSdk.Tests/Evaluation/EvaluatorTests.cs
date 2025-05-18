using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server.Evaluation;

public class EvaluatorTests
{
    private static readonly Variation TrueVariation = new() { Id = "trueId", Value = "true" };
    private static readonly Variation FalseVariation = new() { Id = "falseId", Value = "false" };

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
        Assert.Equal("flag not found", evalResult.Reason);
        Assert.Equivalent(Variation.Empty, evalResult.Variation);

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
            .Variations(TrueVariation)
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
        Assert.Equal("malformed flag", evalResult.Reason);
        Assert.Equivalent(Variation.Empty, evalResult.Variation);

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
            .Variations(TrueVariation)
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
        Assert.Equal("flag off", evalResult.Reason);
        Assert.Equivalent(TrueVariation, evalResult.Variation);

        // flag is off
        Assert.False(evalEvent.SendToExperiment);
    }

    [Fact]
    public void EvaluateTargetedResult()
    {
        var store = new DefaultMemoryStore();

        var variations = new[] { TrueVariation, FalseVariation };
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
        Assert.Equal("target match", evalResult.Reason);
        Assert.Equivalent(FalseVariation, evalResult.Variation);

        // ExptIncludeAllTargets is true by default
        Assert.True(evalEvent.SendToExperiment);
    }

    [Fact]
    public void EvaluatorRuleMatchedResult()
    {
        var store = new DefaultMemoryStore();

        var variations = new[] { TrueVariation, FalseVariation };
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
        Assert.Equal($"match rule {customRule.Name}", evalResult.Reason);
        Assert.Equivalent(TrueVariation, evalResult.Variation);

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
        Assert.Equal("fall through targets and rules", evalResult.Reason);
        Assert.Equivalent(FalseVariation, evalResult.Variation);

        Assert.True(evalEvent.SendToExperiment);
    }
}