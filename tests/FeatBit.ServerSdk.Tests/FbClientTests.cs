using FeatBit.Sdk.Server.DataSynchronizer;
using FeatBit.Sdk.Server.Evaluation;
using FeatBit.Sdk.Server.Events;
using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Options;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server;

[Collection(nameof(TestApp))]
public class FbClientTests
{
    private readonly TestApp _app;

    public FbClientTests(TestApp app)
    {
        _app = app;
    }

    [Fact]
    public async Task CloseInitializedFbClient()
    {
        var client = CreateTestFbClient();
        Assert.True(client.Initialized);

        await client.CloseAsync();
    }

    [Fact]
    public async Task CloseUninitializedFbClient()
    {
        var options = new FbOptionsBuilder("fake-secret")
            .StartWaitTime(TimeSpan.FromMilliseconds(50))
            .Build();
        var client = new FbClient(options);
        Assert.False(client.Initialized);

        await client.CloseAsync();
    }

    [Fact]
    public void GetBoolVariation()
    {
        var eventProcessorMock = new Mock<IEventProcessor>();
        var client = CreateTestFbClient(eventProcessorMock.Object);

        var user = FbUser.Builder("u1").Build();
        var variation = client.BoolVariation("returns-true", user);
        Assert.True(variation);

        eventProcessorMock.Verify(x => x.Record(It.IsAny<IEvent>()), Times.Once);
    }

    [Fact]
    public void GetBoolVariationDetail()
    {
        var eventProcessorMock = new Mock<IEventProcessor>();
        var client = CreateTestFbClient(eventProcessorMock.Object);

        var user = FbUser.Builder("u1").Build();
        var variationDetail = client.BoolVariationDetail("returns-true", user);
        Assert.True(variationDetail.Value);
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
        Assert.Equal("true", result0.Value);
        Assert.Equal(ReasonKind.Fallthrough, result0.Kind);
        Assert.Equal("fall through targets and rules", result0.Reason);
    }

    private FbClient CreateTestFbClient(IEventProcessor processor = null)
    {
        var options = new FbOptionsBuilder("qJHQTVfsZUOu1Q54RLMuIQ-JtrIvNK-k-bARYicOTNQA")
            .Steaming(new Uri("ws://localhost/"))
            .Build();

        var store = new DefaultMemoryStore();
        var synchronizer =
            new WebSocketDataSynchronizer(options, store, op => _app.CreateFbWebSocket(op));
        var eventProcessor = processor ?? new DefaultEventProcessor(options);
        var client = new FbClient(options, store, synchronizer, eventProcessor);
        return client;
    }
}