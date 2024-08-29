using FeatBit.Sdk.Server.DataSynchronizer;
using FeatBit.Sdk.Server.Evaluation;
using FeatBit.Sdk.Server.Events;
using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Options;

namespace FeatBit.Sdk.Server;

public class FbClientOfflineTests
{
    [Fact]
    public async Task CreateAndClose()
    {
        var options = new FbOptionsBuilder()
            .Offline(true)
            .Build();

        var client = new FbClient(options);
        await client.CloseAsync();
    }

    [Fact]
    public void UseNullDataSource()
    {
        var options = new FbOptionsBuilder()
            .Offline(true)
            .Build();

        var client = new FbClient(options);

        Assert.IsType<NullDataSynchronizer>(client._dataSynchronizer);
    }

    [Fact]
    public void UseNullEventProcessor()
    {
        var options = new FbOptionsBuilder()
            .Offline(true)
            .Build();

        var client = new FbClient(options);

        Assert.IsType<NullEventProcessor>(client._eventProcessor);
    }

    [Fact]
    public void UseNullEventProcessorWhenEventsAreDisabled()
    {
        var options = new FbOptionsBuilder()
            .Offline(false)
            .DisableEvents(true)
            .Build();

        var client = new FbClient(options);

        Assert.IsType<NullEventProcessor>(client._eventProcessor);
    }

    [Fact]
    public void ClientIsInitialized()
    {
        var options = new FbOptionsBuilder()
            .Offline(true)
            .Build();

        var client = new FbClient(options);

        Assert.True(client.Initialized);
    }

    [Fact]
    public void EvaluationReturnsDefaultValue()
    {
        var options = new FbOptionsBuilder()
            .Offline(true)
            .Build();

        var client = new FbClient(options);

        var user = FbUser.Builder("tester").Build();

        var variationDetail = client.StringVariationDetail("hello", user, "fallback-value");
        Assert.Equal("fallback-value", variationDetail.Value);
        Assert.Equal(ReasonKind.Error, variationDetail.Kind);
        Assert.Equal("flag not found", variationDetail.Reason);
    }

    [Fact]
    public void WithJsonBootstrapProvider()
    {
        var options = new FbOptionsBuilder()
            .Offline(true)
            .UseJsonBootstrapProvider(TestData.BootstrapJson)
            .Build();

        var client = new FbClient(options);

        var user = FbUser.Builder("true-1").Build();
        var variationDetail = client.BoolVariationDetail("example-flag", user);

        Assert.True(variationDetail.Value);
        Assert.Equal("target match", variationDetail.Reason);
        Assert.Equal(ReasonKind.TargetMatch, variationDetail.Kind);
    }
}