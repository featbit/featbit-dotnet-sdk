using FeatBit.Sdk.Server.DataSynchronizer;
using FeatBit.Sdk.Server.Events;
using FeatBit.Sdk.Server.Options;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server;

public partial class FbClientTests
{
    [Fact]
    public void PublishesDataChangesFromSynchronizer()
    {
        var synchronizer = new ManualDataSynchronizer();
        var client = CreateTestFbClient(synchronizer);
        DataChangeEventArgs received = null;
        client.DataChanged += (_, eventArgs) => received = eventArgs;

        synchronizer.RaiseDataChanged(
            DataChangeKind.Patch,
            featureFlagsChanged: true,
            segmentsChanged: false);

        Assert.NotNull(received);
        Assert.Equal(DataChangeKind.Patch, received.Kind);
        Assert.True(received.FeatureFlagsChanged);
        Assert.False(received.SegmentsChanged);
    }

    [Fact]
    public void UsesExplicitInitialRefreshWhenFullDataChangeOccursDuringStartup()
    {
        var synchronizer = new ManualDataSynchronizer
        {
            DataChangeOnStart = new DataChangeEventArgs(
                DataChangeKind.Full,
                featureFlagsChanged: true,
                segmentsChanged: true)
        };

        var client = CreateTestFbClient(synchronizer);
        var refreshCount = 0;

        client.DataChanged += (_, _) => Refresh();

        Assert.Equal(0, refreshCount);

        Refresh();
        Assert.Equal(1, refreshCount);

        synchronizer.RaiseDataChanged(
            DataChangeKind.Patch,
            featureFlagsChanged: true,
            segmentsChanged: false);

        Assert.Equal(2, refreshCount);

        void Refresh() => refreshCount++;
    }

    [Fact]
    public void IsolatesExceptionsThrownByDataChangeSubscribers()
    {
        var synchronizer = new ManualDataSynchronizer();
        var client = CreateTestFbClient(synchronizer);
        var secondSubscriberCalled = false;
        client.DataChanged += (_, _) => throw new InvalidOperationException("test subscriber failure");
        client.DataChanged += (_, _) => secondSubscriberCalled = true;

        synchronizer.RaiseDataChanged(
            DataChangeKind.Full,
            featureFlagsChanged: true,
            segmentsChanged: true);

        Assert.True(secondSubscriberCalled);
    }

    [Fact]
    public async Task DoesNotBlockReceiveLoopWhenSubscriberAwaitsClientShutdown()
    {
        var options = new FbOptionsBuilder("qJHQTVfsZUOu1Q54RLMuIQ-JtrIvNK-k-bARYicOTNQA")
            .Streaming(new Uri("ws://localhost/"))
            .ConnectTimeout(TimeSpan.FromMilliseconds(10))
            .StartWaitTime(TimeSpan.FromMilliseconds(20))
            .Build();
        var store = new DefaultMemoryStore();
        var webSocketUri = new Uri("ws://localhost/streaming?type=server&token=delayed-full");
        var synchronizer = new WebSocketDataSynchronizer(
            options,
            store,
            op => _app.CreateFbWebSocket(op, webSocketUri));
        var eventProcessor = new Mock<IEventProcessor>();
        var client = new FbClient(options, store, synchronizer, eventProcessor.Object);
        var handlerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task closeTask = null;

        client.DataChanged += async (_, _) =>
        {
            closeTask = client.CloseAsync();
            handlerStarted.TrySetResult(true);
            await closeTask;
        };

        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.NotNull(closeTask);
        await closeTask.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task StopsForwardingDataChangesAfterClientIsClosed()
    {
        var synchronizer = new ManualDataSynchronizer();
        var eventProcessor = new Mock<IEventProcessor>();
        var client = CreateTestFbClient(synchronizer, eventProcessor.Object);
        var notified = false;
        client.DataChanged += (_, _) => notified = true;

        await client.CloseAsync();
        synchronizer.RaiseDataChanged(
            DataChangeKind.Full,
            featureFlagsChanged: true,
            segmentsChanged: true);

        Assert.False(notified);
    }
}
