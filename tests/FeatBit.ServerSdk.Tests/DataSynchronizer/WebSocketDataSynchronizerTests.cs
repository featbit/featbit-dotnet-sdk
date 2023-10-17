using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Options;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server.DataSynchronizer;

[UsesVerify]
[Collection(nameof(TestApp))]
public class WebSocketDataSynchronizerTests
{
    private readonly TestApp _app;

    public WebSocketDataSynchronizerTests(TestApp app)
    {
        _app = app;
    }

    [Fact]
    public async Task StartWithEmptyStoreAsync()
    {
        var options = new FbOptionsBuilder("qJHQTVfsZUOu1Q54RLMuIQ-JtrIvNK-k-bARYicOTNQA")
            .Streaming(new Uri("ws://localhost/"))
            .Build();

        var store = new DefaultMemoryStore();
        var synchronizer = new WebSocketDataSynchronizer(options, store, op => _app.CreateFbWebSocket(op));

        var startTask = synchronizer.StartAsync();
        await startTask.WaitAsync(options.StartWaitTime);

        Assert.True(store.Populated);
        Assert.True(synchronizer.Initialized);

        var flag = store.Get<FeatureFlag>("ff_returns-true");
        Assert.NotNull(flag);

        var segment = store.Get<Segment>("segment_0779d76b-afc6-4886-ab65-af8c004273ad");
        Assert.NotNull(segment);
    }

    [Fact]
    public async Task StartWithPopulatedStoreAsync()
    {
        var options = new FbOptionsBuilder("qJHQTVfsZUOu1Q54RLMuIQ-JtrIvNK-k-bARYicOTNQA")
            .Streaming(new Uri("ws://localhost/"))
            .Build();

        var store = new DefaultMemoryStore();
        var hello = new FeatureFlagBuilder().Key("hello-world").Version(1).Build();
        store.Populate(new[] { hello });

        var synchronizer = new WebSocketDataSynchronizer(options, store, op => _app.CreateFbWebSocket(op));

        var startTask = synchronizer.StartAsync();
        await startTask.WaitAsync(options.StartWaitTime);

        Assert.True(synchronizer.Initialized);

        var flag = store.Get<FeatureFlag>("ff_returns-true");
        Assert.NotNull(flag);
    }
}