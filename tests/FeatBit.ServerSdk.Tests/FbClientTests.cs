using FeatBit.Sdk.Server.DataSynchronizer;
using FeatBit.Sdk.Server.Evaluation;
using FeatBit.Sdk.Server.Events;
using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Options;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server;

[Collection(nameof(TestApp))]
public partial class FbClientTests
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
