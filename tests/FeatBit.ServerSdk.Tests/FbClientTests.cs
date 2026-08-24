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
            .ConnectTimeout(TimeSpan.FromMilliseconds(50))
            .StartWaitTime(TimeSpan.FromMilliseconds(100))
            .Build();
        var client = new FbClient(options);
        Assert.False(client.Initialized);

        await client.CloseAsync();
    }

    [Fact]
    public void GetVariation()
    {
        var eventProcessorMock = new Mock<IEventProcessor>();
        var client = CreateTestFbClient(eventProcessorMock.Object);

        var user = FbUser.Builder("u1").Build();
        var variation = client.BoolVariation("returns-true", user);
        Assert.True(variation);

        eventProcessorMock.Verify(x => x.Record(It.IsAny<IEvent>()), Times.Once);
    }

    [Fact]
    public void GetVariationDetail()
    {
        var eventProcessorMock = new Mock<IEventProcessor>();
        var client = CreateTestFbClient(eventProcessorMock.Object);

        var user = FbUser.Builder("u1").Build();
        var variationDetail = client.BoolVariationDetail("returns-true", user);
        Assert.Equal("returns-true", variationDetail.Key);
        Assert.True(variationDetail.Value);
        Assert.Equal("3da96792-debf-4878-905a-c9b5f9178cd0", variationDetail.ValueId);
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
        Assert.Equal("returns-true", result0.Key);
        Assert.Equal("true", result0.Value);
        Assert.Equal("3da96792-debf-4878-905a-c9b5f9178cd0", result0.ValueId);
        Assert.Equal(ReasonKind.Fallthrough, result0.Kind);
        Assert.Equal("fall through targets and rules", result0.Reason);
    }

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

    private FbClient CreateTestFbClient(IEventProcessor processor = null) =>
        CreateTestFbClient(null, processor);

    private FbClient CreateTestFbClient(
        IDataSynchronizer synchronizer,
        IEventProcessor processor = null)
    {
        var options = new FbOptionsBuilder("qJHQTVfsZUOu1Q54RLMuIQ-JtrIvNK-k-bARYicOTNQA")
            .Streaming(new Uri("ws://localhost/"))
            .Build();

        var store = new DefaultMemoryStore();
        synchronizer ??=
            new WebSocketDataSynchronizer(options, store, op => _app.CreateFbWebSocket(op));
        var eventProcessor = processor ?? new DefaultEventProcessor(options);
        var client = new FbClient(options, store, synchronizer, eventProcessor);
        return client;
    }

    private sealed class ManualDataSynchronizer : IDataSynchronizer
    {
        public DataChangeEventArgs DataChangeOnStart { get; init; }

        public bool Initialized => true;

        public DataSynchronizerStatus Status => DataSynchronizerStatus.Stable;

        public event Action<DataSynchronizerStatus> StatusChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<DataChangeEventArgs> DataChanged;

        public Task<bool> StartAsync()
        {
            if (DataChangeOnStart != null)
            {
                DataChanged?.Invoke(this, DataChangeOnStart);
            }

            return Task.FromResult(true);
        }

        public Task StopAsync() => Task.CompletedTask;

        public void RaiseDataChanged(
            DataChangeKind kind,
            bool featureFlagsChanged,
            bool segmentsChanged)
        {
            DataChanged?.Invoke(
                this,
                new DataChangeEventArgs(kind, featureFlagsChanged, segmentsChanged));
        }
    }
}
