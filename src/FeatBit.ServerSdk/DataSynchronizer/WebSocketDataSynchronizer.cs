using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FeatBit.Sdk.Server.Concurrent;
using FeatBit.Sdk.Server.Options;
using FeatBit.Sdk.Server.Store;
using FeatBit.Sdk.Server.Transport;
using FeatBit.Sdk.Server.Utils;
using Microsoft.Extensions.Logging;

namespace FeatBit.Sdk.Server.DataSynchronizer
{
    internal sealed class WebSocketDataSynchronizer : IDataSynchronizer
    {
        private static readonly byte[] FullDataSync =
            Encoding.UTF8.GetBytes("{\"messageType\":\"data-sync\",\"data\":{\"timestamp\":0}}");

        private readonly StatusManager<DataSynchronizerStatus> _statusManager;
        private readonly AtomicBoolean _initialized;

        public bool Initialized => _initialized.Value;
        public DataSynchronizerStatus Status => _statusManager.Status;
        public event Action<DataSynchronizerStatus> StatusChanged;
        public event EventHandler<DataChangeEventArgs> DataChanged;

        private readonly IMemoryStore _store;
        private readonly FbOptions _options;
        private readonly FbWebSocket _webSocket;
        private readonly TaskCompletionSource<bool> _initTcs;
        private readonly ILogger<WebSocketDataSynchronizer> _logger;

        private readonly DataSyncProcessor _dataSyncProcessor;

        public WebSocketDataSynchronizer(
            FbOptions options,
            IMemoryStore store,
            Func<FbOptions, FbWebSocket> fbWebSocketFactory = null)
        {
            _store = store;
            _statusManager = new StatusManager<DataSynchronizerStatus>(
                DataSynchronizerStatus.Starting,
                OnStatusChanged
            );

            // Shallow copy so we don't mutate the user-defined options object.
            var shallowCopiedOptions = options.ShallowCopy();
            _options = shallowCopiedOptions;

            var factory = fbWebSocketFactory ?? DefaultFbWebSocketFactory;
            _webSocket = factory(shallowCopiedOptions);

            _webSocket.OnConnected += OnConnected;
            _webSocket.OnReceived += OnReceived;
            _webSocket.OnReconnecting += OnReconnecting;
            _webSocket.OnClosed += OnClosed;

            _initTcs = new TaskCompletionSource<bool>();
            _initialized = new AtomicBoolean();

            _logger = options.LoggerFactory.CreateLogger<WebSocketDataSynchronizer>();
            _dataSyncProcessor = new DataSyncProcessor(store, _logger);
        }

        private static FbWebSocket DefaultFbWebSocketFactory(FbOptions options)
        {
            return new FbWebSocket(options);
        }

        public Task<bool> StartAsync()
        {
            _webSocket.ConnectAsync().Forget();

            return _initTcs.Task;
        }

        private async Task OnConnected()
        {
            try
            {
                // do data-sync once the connection is established
                await DoDataSyncAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception occurred when performing data synchronization request");
            }
        }

        private Task OnReceived(ReadOnlySequence<byte> sequence)
        {
            var bytes = sequence.IsSingleSegment
                ? sequence.First // this should be the hot path
                : sequence.ToArray();

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Received server message. Length: {Length} bytes", bytes.Length);
            }

            try
            {
                using var jsonDocument = JsonDocument.Parse(bytes);
                var root = jsonDocument.RootElement;
                var messageType = root.GetProperty("messageType").GetString();
                if (messageType == "data-sync")
                {
                    ProcessDataSyncMessage(root.GetProperty("data"));
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse or deserialize server message");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception occurred when handling server message.");
            }

            return Task.CompletedTask;
        }

        private Task OnReconnecting(Exception ex)
        {
            _statusManager.CompareAndSet(DataSynchronizerStatus.Stable, DataSynchronizerStatus.Interrupted);
            return Task.CompletedTask;
        }

        private Task OnClosed(
            Exception ex, 
            WebSocketCloseStatus? closeStatus, 
            string closeStatusDescription, 
            string closeMessage)
        {
            _statusManager.SetStatus(DataSynchronizerStatus.Stopped);
            return Task.CompletedTask;
        }

        private void OnStatusChanged(DataSynchronizerStatus status) => StatusChanged?.Invoke(status);

        private async Task DoDataSyncAsync()
        {
            byte[] request;

            var version = Initialized ? _store.Version() : 0;
            if (version == 0)
            {
                // this should be the hot path
                request = FullDataSync;
            }
            else
            {
                object patchDataSync = new
                {
                    messageType = "data-sync",
                    data = new
                    {
                        timestamp = version
                    }
                };

                request = JsonSerializer.SerializeToUtf8Bytes(patchDataSync);
            }

            _logger.LogDebug("Do data-sync with version: {Version}", version);
            await _webSocket.SendAsync(request);
        }

        public async Task StopAsync()
        {
            _webSocket.OnConnected -= OnConnected;
            _webSocket.OnReceived -= OnReceived;
            _webSocket.OnReconnecting -= OnReconnecting;
            _webSocket.OnClosed -= OnClosed;

            await _webSocket.CloseAsync();

            _statusManager.SetStatus(DataSynchronizerStatus.Stopped);
        }

        private void ProcessDataSyncMessage(JsonElement data)
        {
            var dataSet = DataSet.FromJsonElement(data);
            if (dataSet.EventType == DataSet.Patch && !Initialized)
            {
                // Patch data-sync message received before full synchronization has initialized the client, ignore it
                _logger.LogWarning(
                    "Ignoring patch data-sync message before full synchronization has initialized the client"
                );

                return;
            }

            var result = _dataSyncProcessor.Process(dataSet);
            if (!result.Applied)
            {
                // Ignore if the data-sync message is not applied, e.g. unknown event type
                return;
            }

            _statusManager.SetStatus(DataSynchronizerStatus.Stable);
            if (dataSet.EventType == DataSet.Full && _initialized.CompareAndSet(false, true))
            {
                _initTcs.TrySetResult(true);
            }

            // Notify subscribers of data changes if there are any
            if (result.Change != null)
            {
                DataChanged?.Invoke(this, result.Change);
            }
        }
    }
}
