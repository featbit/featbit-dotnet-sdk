using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FeatBit.Sdk.Server.Concurrent;
using FeatBit.Sdk.Server.Options;
using FeatBit.Sdk.Server.Store;
using FeatBit.Sdk.Server.Transport;
using Microsoft.Extensions.Logging;

namespace FeatBit.Sdk.Server.DataSynchronizer
{
    internal sealed class WebSocketDataSynchronizer : IDataSynchronizer
    {
        private readonly StatusManager<DataSynchronizerStatus> _statusManager;
        private readonly AtomicBoolean _initialized;

        public bool Initialized => _initialized.Value;
        public DataSynchronizerStatus Status => _statusManager.Status;
        public event Action<DataSynchronizerStatus> StatusChanged;

        private readonly IMemoryStore _store;
        private readonly FbOptions _options;
        private readonly FbWebSocket _webSocket;
        private readonly TaskCompletionSource<bool> _initTcs;
        private readonly ILogger<WebSocketDataSynchronizer> _logger;

        private static readonly byte[] FullDataSync =
            Encoding.UTF8.GetBytes("{\"messageType\":\"data-sync\",\"data\":{\"timestamp\":0}}");

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
        }

        private static FbWebSocket DefaultFbWebSocketFactory(FbOptions options)
        {
            return new FbWebSocket(options);
        }

        public Task<bool> StartAsync()
        {
            Task.Run(() =>
            {
                var cts = new CancellationTokenSource(_options.ConnectTimeout);
                return _webSocket.ConnectAsync(cts.Token);
            });

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
                _logger.LogDebug(ex, "Exception occurred when performing data synchronization request");
            }
        }

        private Task OnReceived(ReadOnlySequence<byte> bytes)
        {
            try
            {
                HandleMessage(bytes);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Exception occurred when handling server message");
            }

            return Task.CompletedTask;
        }

        private Task OnReconnecting(Exception ex)
        {
            _statusManager.SetStatus(DataSynchronizerStatus.Interrupted);
            return Task.CompletedTask;
        }

        private Task OnClosed(Exception ex, WebSocketCloseStatus? closeStatus, string closeStatusDescription)
        {
            _statusManager.SetStatus(DataSynchronizerStatus.Stopped);
            return Task.CompletedTask;
        }

        private void OnStatusChanged(DataSynchronizerStatus status) => StatusChanged?.Invoke(status);

        private async Task DoDataSyncAsync()
        {
            byte[] request;

            var version = _store.Version();
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

        private void HandleMessage(ReadOnlySequence<byte> sequence)
        {
            var bytes = sequence.IsSingleSegment
                ? sequence.First // this should be the hot path
                : sequence.ToArray();

            using (var jsonDocument = JsonDocument.Parse(bytes))
            {
                var root = jsonDocument.RootElement;
                var messageType = root.GetProperty("messageType").GetString();

                // handle 'data-sync' message
                if (messageType == "data-sync")
                {
                    HandleDataSyncMessage(root);
                }
            }

            return;

            void HandleDataSyncMessage(JsonElement root)
            {
                var dataSet = DataSet.FromJsonElement(root.GetProperty("data"));
                _logger.LogDebug("Received {Type} data-sync message", dataSet.EventType);
                var objects = dataSet.GetStorableObjects();
                // populate data store
                if (dataSet.EventType == DataSet.Full)
                {
                    _store.Populate(objects);
                }
                // upsert objects
                else if (dataSet.EventType == DataSet.Patch)
                {
                    foreach (var storableObject in objects)
                    {
                        _store.Upsert(storableObject);
                    }
                }

                _statusManager.SetStatus(DataSynchronizerStatus.Stable);
                if (_initialized.CompareAndSet(false, true))
                {
                    _initTcs.TrySetResult(true);
                }
            }
        }

        public async Task StopAsync()
        {
            await _webSocket.CloseAsync();

            _webSocket.OnConnected -= OnConnected;
            _webSocket.OnReceived -= OnReceived;
            _webSocket.OnReconnecting -= OnReconnecting;
            _webSocket.OnClosed -= OnClosed;
        }
    }
}