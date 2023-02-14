using System;
using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FeatBit.Sdk.Server.Json;
using FeatBit.Sdk.Server.Options;
using FeatBit.Sdk.Server.Store;
using FeatBit.Sdk.Server.Transport;

namespace FeatBit.Sdk.Server.DataSynchronizer
{
    internal sealed class WebSocketDataSynchronizer : IDataSynchronizer
    {
        internal bool Initialized { get; private set; }

        private readonly IMemoryStore _store;
        private readonly FbOptions _options;
        private readonly FbWebSocket _webSocket;
        private readonly TaskCompletionSource<bool> _initTcs;

        private static readonly byte[] FullDataSync =
            Encoding.UTF8.GetBytes("{\"messageType\":\"data-sync\",\"data\":{\"timestamp\":0}}");

        public WebSocketDataSynchronizer(
            FbOptions options,
            IMemoryStore store,
            Func<FbOptions, FbWebSocket> fbWebSocketFactory = null)
        {
            _store = store;
            // Shallow copy before setting the StreamingUri property so we don't mutate the user-defined options object.
            var shallowCopiedOptions = options.ShallowCopy();
            shallowCopiedOptions.StreamingUri = options.ResolveWebSocketUri();

            _options = shallowCopiedOptions;

            var factory = fbWebSocketFactory ?? DefaultFbWebSocketFactory;
            _webSocket = factory(shallowCopiedOptions);

            _webSocket.OnConnected += OnConnected;
            _webSocket.OnReceived += OnReceived;

            _initTcs = new TaskCompletionSource<bool>();
            Initialized = false;
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
                return _webSocket.StartAsync(cts.Token);
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
            catch
            {
                // exception occured when data-sync
            }
        }

        private Task OnReceived(ReadOnlySequence<byte> bytes)
        {
            try
            {
                HandleMessage(bytes);
            }
            catch
            {
                // exception occured when handling server's message
            }

            return Task.CompletedTask;
        }

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
                    var dataSet = root.GetProperty("data").Deserialize<DataSet>(ReusableJsonSerializerOptions.Web);

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

                    if (!Initialized)
                    {
                        CompleteInitialize();
                    }
                }
            }
        }

        private void CompleteInitialize()
        {
            Initialized = true;
            _initTcs.TrySetResult(true);
        }

        public async Task StopAsync()
        {
            _webSocket.OnConnected -= OnConnected;
            _webSocket.OnReceived -= OnReceived;

            await _webSocket.StopAsync();
        }
    }
}