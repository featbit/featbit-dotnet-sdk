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

        // canceled when a data-sync reply is applied; if it fires first, the connection is replaced
        private CancellationTokenSource _dataSyncTimeoutCts;

        // DataChanged subscribers run here, in order, never on the WebSocket receive loop
        private readonly SerialDispatcher<DataChangeEventArgs> _dataChangedDispatcher;

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
            _dataChangedDispatcher = new SerialDispatcher<DataChangeEventArgs>(
                change => DataChanged?.Invoke(this, change),
                _logger,
                // notifications only say "something changed": merge any that pile up behind a slow subscriber
                MergeDataChanges
            );
        }

        internal static DataChangeEventArgs MergeDataChanges(DataChangeEventArgs pending, DataChangeEventArgs next) =>
            new(
                pending.Kind == DataChangeKind.Full || next.Kind == DataChangeKind.Full
                    ? DataChangeKind.Full
                    : DataChangeKind.Patch,
                pending.FeatureFlagsChanged || next.FeatureFlagsChanged,
                pending.SegmentsChanged || next.SegmentsChanged
            );

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
                // do data-sync once the connection is established, and make sure we hear back
                StartDataSyncTimeout();
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
            CancelDataSyncTimeout();
            _statusManager.CompareAndSet(DataSynchronizerStatus.Stable, DataSynchronizerStatus.Interrupted);
            return Task.CompletedTask;
        }

        private Task OnClosed(
            Exception ex,
            WebSocketCloseStatus? closeStatus,
            string closeStatusDescription,
            string closeMessage)
        {
            CancelDataSyncTimeout();
            _statusManager.SetStatus(DataSynchronizerStatus.Stopped);
            return Task.CompletedTask;
        }

        private void StartDataSyncTimeout()
        {
            var cts = new CancellationTokenSource();
            Interlocked.Exchange(ref _dataSyncTimeoutCts, cts)?.Cancel();

            // OnConnected runs after the connection is established, so this is the connection the request goes to
            _ = WatchDataSyncReplyAsync(_webSocket.ConnectionId, cts.Token);
        }

        private void CancelDataSyncTimeout() => Interlocked.Exchange(ref _dataSyncTimeoutCts, null)?.Cancel();

        private async Task WatchDataSyncReplyAsync(int connectionId, CancellationToken token)
        {
            var timeout = _options.DataSyncTimeout;
            try
            {
                await Task.Delay(timeout, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // reply arrived (or the connection went away) in time
                return;
            }

            // The reply may have been applied between the delay completing and this continuation running,
            // or the connection may already have been replaced: neither should be aborted (or logged).
            if (token.IsCancellationRequested || connectionId != _webSocket.ConnectionId)
            {
                return;
            }

            // The server accepted the request but never replied (e.g. its data-sync handler failed and it
            // logged the error without responding). Pings still succeed, so nothing else would notice.
            // Scoped to the connection the request was sent on: a later connection is never aborted by this.
            _logger.LogWarning(
                "No data-sync response received within {Timeout} seconds; replacing the connection.",
                timeout.TotalSeconds
            );
            _webSocket.AbortConnection(new TimeoutException(
                $"No data-sync response received within {timeout.TotalSeconds:F0} seconds."
            ), connectionId);
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
            CancelDataSyncTimeout();

            await _webSocket.CloseAsync();

            _statusManager.SetStatus(DataSynchronizerStatus.Stopped);
        }

        private void ProcessDataSyncMessage(JsonElement data)
        {
            var dataSet = DataSet.FromJsonElement(data, _logger);
            if (dataSet.SkippedCount > 0)
            {
                _logger.LogError(
                    "{SkippedCount} malformed entities were skipped in a {EventType} data-sync message; " +
                    "the remaining entities were applied.",
                    dataSet.SkippedCount, dataSet.EventType
                );
            }

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

            // the server answered: the connection is useful, stop waiting and reset the reconnect backoff
            CancelDataSyncTimeout();
            _webSocket.MarkConnectionHealthy();

            _statusManager.SetStatus(DataSynchronizerStatus.Stable);
            if (dataSet.EventType == DataSet.Full && _initialized.CompareAndSet(false, true))
            {
                _initTcs.TrySetResult(true);
            }

            // Notify subscribers of data changes if there are any. Subscribers run on a background dispatcher
            // so a slow handler cannot delay the processing of later messages (flag updates, pongs).
            if (result.Change != null)
            {
                _dataChangedDispatcher.Post(result.Change);
            }
        }
    }
}
