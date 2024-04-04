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
        Assert.Equal(DataSynchronizerStatus.Starting, synchronizer.Status);

        var startTask = synchronizer.StartAsync();
        await startTask.WaitAsync(options.StartWaitTime);

        Assert.True(store.Populated);
        Assert.True(synchronizer.Initialized);
        Assert.Equal(DataSynchronizerStatus.Stable, synchronizer.Status);

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
        Assert.Equal(DataSynchronizerStatus.Starting, synchronizer.Status);

        var startTask = synchronizer.StartAsync();
        await startTask.WaitAsync(options.StartWaitTime);

        Assert.True(synchronizer.Initialized);
        Assert.Equal(DataSynchronizerStatus.Stable, synchronizer.Status);

        var flag = store.Get<FeatureFlag>("ff_returns-true");
        Assert.NotNull(flag);
    }

    [Fact]
    public async Task ServerRejectConnection()
    {
        var options = new FbOptionsBuilder().Build();
        var store = new DefaultMemoryStore();

        var synchronizer =
            new WebSocketDataSynchronizer(options, store, _ => _app.CreateFbWebSocket("close-with-4003"));
        Assert.Equal(DataSynchronizerStatus.Starting, synchronizer.Status);

        _ = synchronizer.StartAsync();

        var tcs = new TaskCompletionSource();
        var onStatusChangedTask = tcs.Task;
        synchronizer.StatusChanged += _ =>
        {
            Assert.False(synchronizer.Initialized);
            Assert.Equal(DataSynchronizerStatus.Stopped, synchronizer.Status);
            tcs.SetResult();
        };
        await onStatusChangedTask.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ServerDisconnectedAfterStable()
    {
        var options = new FbOptionsBuilder()
            .Build();
        var store = new DefaultMemoryStore();

        var webSocketUri = new Uri("ws://localhost/streaming?type=server&token=close-after-first-datasync");
        var synchronizer =
            new WebSocketDataSynchronizer(options, store, op => _app.CreateFbWebSocket(op, webSocketUri));
        Assert.Equal(DataSynchronizerStatus.Starting, synchronizer.Status);

        var startTask = synchronizer.StartAsync();
        await startTask.WaitAsync(options.StartWaitTime);

        Assert.True(synchronizer.Initialized);
        Assert.Equal(DataSynchronizerStatus.Stable, synchronizer.Status);

        var tcs = new TaskCompletionSource();
        var onStatusChangedTask = tcs.Task;
        synchronizer.StatusChanged += _ =>
        {
            Assert.Equal(DataSynchronizerStatus.Interrupted, synchronizer.Status);
            tcs.SetResult();
        };
        await onStatusChangedTask.WaitAsync(TimeSpan.FromSeconds(1));
    }
}