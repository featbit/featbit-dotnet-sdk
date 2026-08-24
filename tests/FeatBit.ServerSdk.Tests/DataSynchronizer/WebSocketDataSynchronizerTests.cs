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
    public async Task NotifiesAfterFullDataSyncIsApplied()
    {
        var options = new FbOptionsBuilder("qJHQTVfsZUOu1Q54RLMuIQ-JtrIvNK-k-bARYicOTNQA")
            .Streaming(new Uri("ws://localhost/"))
            .Build();

        var store = new DefaultMemoryStore();
        var synchronizer = new WebSocketDataSynchronizer(options, store, op => _app.CreateFbWebSocket(op));
        var dataChanged = new TaskCompletionSource<DataChangeEventArgs>();

        synchronizer.DataChanged += (_, eventArgs) => dataChanged.TrySetResult(eventArgs);

        await synchronizer.StartAsync().WaitAsync(options.StartWaitTime);

        var change = await dataChanged.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(store.Populated);
        Assert.True(synchronizer.Initialized);
        Assert.NotNull(store.Get<FeatureFlag>("ff_returns-true"));
        Assert.Equal(DataChangeKind.Full, change.Kind);
        Assert.True(change.FeatureFlagsChanged);
        Assert.True(change.SegmentsChanged);
    }

    [Fact]
    public async Task FullDataSyncInvalidatesBothCategoriesWhenItRemovesStoredData()
    {
        var options = new FbOptionsBuilder("qJHQTVfsZUOu1Q54RLMuIQ-JtrIvNK-k-bARYicOTNQA")
            .Streaming(new Uri("ws://localhost/"))
            .Build();

        var store = new DefaultMemoryStore();
        store.Populate(new StorableObject[]
        {
            new FeatureFlagBuilder().Key("hello-world").Version(1).Build(),
            new SegmentBuilder().Id(Guid.Parse("3e2a29b9-1f58-4e5d-8f0f-0248b806d75c")).Version(1).Build(),
        });

        var webSocketUri = new Uri("ws://localhost/streaming?type=server&token=empty-full");
        var synchronizer = new WebSocketDataSynchronizer(
            options,
            store,
            op => _app.CreateFbWebSocket(op, webSocketUri));
        var dataChanged = new TaskCompletionSource<DataChangeEventArgs>();
        synchronizer.DataChanged += (_, eventArgs) => dataChanged.TrySetResult(eventArgs);

        await synchronizer.StartAsync().WaitAsync(options.StartWaitTime);

        var change = await dataChanged.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Null(store.Get<FeatureFlag>("hello-world"));
        Assert.Null(store.Get<Segment>("segment_3e2a29b9-1f58-4e5d-8f0f-0248b806d75c"));
        Assert.Equal(DataChangeKind.Full, change.Kind);
        Assert.True(change.FeatureFlagsChanged);
        Assert.True(change.SegmentsChanged);
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
    public async Task NotifiesAfterEffectivePatchDataSync()
    {
        var options = new FbOptionsBuilder("qJHQTVfsZUOu1Q54RLMuIQ-JtrIvNK-k-bARYicOTNQA")
            .Streaming(new Uri("ws://localhost/"))
            .Build();

        var store = new DefaultMemoryStore();
        store.Populate(new[] { new FeatureFlagBuilder().Key("hello-world").Version(1).Build() });

        var synchronizer = new WebSocketDataSynchronizer(options, store, op => _app.CreateFbWebSocket(op));
        var dataChanged = new TaskCompletionSource<DataChangeEventArgs>();
        synchronizer.DataChanged += (_, eventArgs) => dataChanged.TrySetResult(eventArgs);

        await synchronizer.StartAsync().WaitAsync(options.StartWaitTime);

        var change = await dataChanged.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var flag = store.Get<FeatureFlag>("ff_returns-true");
        Assert.NotNull(flag);
        Assert.True(synchronizer.Initialized);
        Assert.Equal("returns-true", flag.Key);
        Assert.Equal(DataChangeKind.Patch, change.Kind);
        Assert.True(change.FeatureFlagsChanged);
        Assert.False(change.SegmentsChanged);
    }

    [Fact]
    public async Task NotifiesAfterEffectiveSegmentPatchDataSync()
    {
        var options = new FbOptionsBuilder("qJHQTVfsZUOu1Q54RLMuIQ-JtrIvNK-k-bARYicOTNQA")
            .Streaming(new Uri("ws://localhost/"))
            .Build();

        var store = new DefaultMemoryStore();
        store.Populate(new[] { new FeatureFlagBuilder().Key("hello-world").Version(1).Build() });

        var webSocketUri = new Uri("ws://localhost/streaming?type=server&token=segment-patch");
        var synchronizer = new WebSocketDataSynchronizer(
            options,
            store,
            op => _app.CreateFbWebSocket(op, webSocketUri));
        var dataChanged = new TaskCompletionSource<DataChangeEventArgs>();
        synchronizer.DataChanged += (_, eventArgs) => dataChanged.TrySetResult(eventArgs);

        await synchronizer.StartAsync().WaitAsync(options.StartWaitTime);

        var change = await dataChanged.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(synchronizer.Initialized);
        Assert.NotNull(store.Get<Segment>("segment_3e2a29b9-1f58-4e5d-8f0f-0248b806d75c"));
        Assert.Equal(DataChangeKind.Patch, change.Kind);
        Assert.False(change.FeatureFlagsChanged);
        Assert.True(change.SegmentsChanged);
    }

    [Fact]
    public async Task DoesNotNotifyWhenPatchDoesNotChangeStore()
    {
        var options = new FbOptionsBuilder("qJHQTVfsZUOu1Q54RLMuIQ-JtrIvNK-k-bARYicOTNQA")
            .Streaming(new Uri("ws://localhost/"))
            .Build();

        var store = new DefaultMemoryStore();
        store.Populate(new[] { new FeatureFlagBuilder().Key("returns-true").Version(long.MaxValue).Build() });

        var synchronizer = new WebSocketDataSynchronizer(options, store, op => _app.CreateFbWebSocket(op));
        var notified = false;
        synchronizer.DataChanged += (_, _) => notified = true;

        await synchronizer.StartAsync().WaitAsync(options.StartWaitTime);

        Assert.False(notified);
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
            .ReconnectRetryDelays(new[] { TimeSpan.FromMilliseconds(200) })
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
