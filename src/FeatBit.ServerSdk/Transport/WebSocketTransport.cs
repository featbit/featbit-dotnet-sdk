using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FeatBit.Sdk.Server.Http;
using FeatBit.Sdk.Server.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FeatBit.Sdk.Server.Transport
{
    // ref: https://github.com/dotnet/aspnetcore/blob/main/src/SignalR/clients/csharp/Http.Connections.Client/src/Internal/WebSocketsTransport.cs
    internal sealed partial class WebSocketTransport : IDuplexPipe
    {
        public PipeReader Input => _transport.Input;
        public PipeWriter Output => _transport.Output;

        public WebSocketState State => _webSocket?.State ?? WebSocketState.None;
        public WebSocketCloseStatus? CloseStatus => _webSocket?.CloseStatus;
        public string CloseDescription => _webSocket?.CloseStatusDescription;

        private readonly FbOptions _options;
        private readonly Func<Uri, CancellationToken, Task<WebSocket>> _webSocketFactory;
        private WebSocket _webSocket;

        private IDuplexPipe _transport;
        private IDuplexPipe _application;

        private TimeSpan _closeTimeout;
        private readonly CancellationTokenSource _stopCts = new CancellationTokenSource();
        private volatile bool _aborted;
        private Task Running { get; set; } = Task.CompletedTask;
        private readonly ILogger<WebSocketTransport> _logger;

        private static readonly TimeSpan DefaultCloseTimeout = TimeSpan.FromSeconds(5);

        // 1MB
        private const long DefaultBufferSize = 1024 * 1024;

        // 1KB
        private const int DefaultMessageBufferSize = 1024;

        private static PipeOptions DefaultPipeOptions => new PipeOptions(
            pauseWriterThreshold: DefaultBufferSize,
            resumeWriterThreshold: DefaultBufferSize / 2,
            readerScheduler: PipeScheduler.ThreadPool,
            useSynchronizationContext: false
        );

        public WebSocketTransport(
            FbOptions options,
            ILoggerFactory loggerFactory = null,
            Func<Uri, CancellationToken, Task<WebSocket>> webSocketFactory = null)
        {
            _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<WebSocketTransport>();
            _options = options;
            _webSocketFactory = webSocketFactory;
        }

        public async Task StartAsync(
            Uri uri,
            TimeSpan? closeTimeout = null,
            CancellationToken cancellationToken = default)
        {
            _closeTimeout = closeTimeout ?? DefaultCloseTimeout;

            // Create the pipe pair (Application's writer is connected to Transport's reader, and vice versa)
            var pair = DuplexPipe.CreateConnectionPair(DefaultPipeOptions, DefaultPipeOptions);

            // The transport duplex pipe is used by the caller to
            // - subscribe to incoming websocket messages
            // - push messages to the websocket
            _transport = pair.Transport;

            // The application duplex pipe is used here to
            // - subscribe to incoming messages from the caller
            // - proxy incoming data from the websocket back to the subscriber
            _application = pair.Application;

            var factory = _webSocketFactory ?? DefaultWebSocketFactory;
            _webSocket = await factory(uri, cancellationToken);
            if (_webSocket == null)
            {
                throw new InvalidOperationException("Configured WebSocketFactory did not return a value.");
            }

            Running = ProcessSocketAsync(_webSocket);

            Log.StartedTransport(_logger);
        }

        private async Task<WebSocket> DefaultWebSocketFactory(Uri uri, CancellationToken cancellationToken)
        {
            var webSocket = new ClientWebSocket();

            // reference: https://github.com/dotnet/aspnetcore/blob/main/src/SignalR/clients/csharp/Http.Connections.Client/src/Internal/WebSocketsTransport.cs#L98-L105
            // Full Framework will throw when trying to set the User-Agent header
            // So avoid setting it in netstandard2.0 and only set it in netstandard2.1 and higher
#if !NETSTANDARD2_0 && !NETFRAMEWORK
            webSocket.Options.SetRequestHeader("User-Agent", HttpConstants.UserAgent);
#else
            // Set an alternative user agent header on Full framework
            webSocket.Options.SetRequestHeader("X-FeatBit-User-Agent", HttpConstants.UserAgent);
#endif

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_options.ConnectTimeout);
                await webSocket.ConnectAsync(uri, cts.Token);
            }
            catch
            {
                webSocket.Dispose();
                throw;
            }

            return webSocket;
        }

        private async Task ProcessSocketAsync(WebSocket socket)
        {
            Debug.Assert(_application != null);

            using (socket)
            {
                // Begin sending and receiving.
                var receiving = StartReceiving(socket);
                var sending = StartSending(socket);

                // Wait for send or receive to complete
                var trigger = await Task.WhenAny(receiving, sending).ConfigureAwait(false);

                _stopCts.CancelAfter(_closeTimeout);

                if (trigger == receiving)
                {
                    // We're waiting for the application to finish and there are 2 things it could be doing
                    // 1. Waiting for application data
                    // 2. Waiting for a websocket send to complete

                    // Cancel the application so that ReadAsync yields
                    _application.Input.CancelPendingRead();

                    var resultTask = await Task.WhenAny(sending, Task.Delay(_closeTimeout, _stopCts.Token))
                        .ConfigureAwait(false);
                    if (resultTask != sending)
                    {
                        _aborted = true;

                        // Abort the websocket if we're stuck in a pending send to the client
                        socket.Abort();
                    }
                }
                else
                {
                    // We're waiting on the websocket to close and there are 2 things it could be doing
                    // 1. Waiting for websocket data
                    // 2. Waiting on a flush to complete (backpressure being applied)

                    _aborted = true;

                    // Abort the websocket if we're stuck in a pending receive from the client
                    socket.Abort();

                    // Cancel any pending flush so that we can quit
                    _application.Output.CancelPendingFlush();
                }
            }
        }

        private async Task StartReceiving(WebSocket socket)
        {
            Debug.Assert(_application != null);

            try
            {
                while (true)
                {
#if NETSTANDARD2_1 || NETCOREAPP
                    // Do a 0 byte read so that idle connections don't allocate a buffer when waiting for a read
                    var result = await socket.ReceiveAsync(Memory<byte>.Empty, _stopCts.Token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Log.WebSocketClosed(_logger, socket.CloseStatus);
                        return;
                    }

                    ValueWebSocketReceiveResult receiveResult;
#else
                    WebSocketReceiveResult receiveResult;
#endif
                    do
                    {
                        var memory = _application.Output.GetMemory(DefaultMessageBufferSize);

#if NETSTANDARD2_1 || NETCOREAPP
                        receiveResult = await socket.ReceiveAsync(memory, _stopCts.Token).ConfigureAwait(false);
#elif NETSTANDARD2_0 || NETFRAMEWORK
                        var isArray = MemoryMarshal.TryGetArray<byte>(memory, out var arraySegment);
                        Debug.Assert(isArray);

                        // Exceptions are handled above where the send and receive tasks are being run.
                        receiveResult = await socket.ReceiveAsync(arraySegment, _stopCts.Token).ConfigureAwait(false);
#else
#error TFMs need to be updated
#endif
                        _application.Output.Advance(receiveResult.Count);
                    } while (receiveResult.MessageType != WebSocketMessageType.Close && !receiveResult.EndOfMessage);

                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        Log.WebSocketClosed(_logger, _webSocket.CloseStatus);
                        return;
                    }

                    Log.MessageReceived(_logger, receiveResult.MessageType, receiveResult.Count);

                    // write record separator
                    TextMessageFormatter.WriteRecordSeparator(_application.Output);

                    // Message received. Type: {MessageType}, size: {Count}, EndOfMessage: True.
                    var flushResult = await _application.Output.FlushAsync().ConfigureAwait(false);

                    // We canceled in the middle of applying back pressure
                    // or if the consumer is done
                    if (flushResult.IsCanceled || flushResult.IsCompleted)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log.ReceiveCanceled(_logger);
            }
            catch (Exception ex)
            {
                if (!_aborted)
                {
                    _application.Output.Complete(ex);
                }
            }
            finally
            {
                // We're done writing
                _application.Output.Complete();

                Log.ReceiveStopped(_logger);
            }
        }

        private async Task StartSending(WebSocket socket)
        {
            Debug.Assert(_application != null);

            Exception error = null;

            try
            {
                while (true)
                {
                    var result = await _application.Input.ReadAsync().ConfigureAwait(false);
                    var buffer = result.Buffer;

                    // Get a frame from the application

                    try
                    {
                        if (result.IsCanceled)
                        {
                            break;
                        }

                        if (!buffer.IsEmpty)
                        {
                            try
                            {
                                Log.ReceivedFromApp(_logger, buffer.Length);
                                if (WebSocketCanSend(socket))
                                {
                                    await socket.SendAsync(buffer, WebSocketMessageType.Text, _stopCts.Token)
                                        .ConfigureAwait(false);
                                }
                                else
                                {
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                if (!_aborted)
                                {
                                    Log.ErrorSendingMessage(_logger, ex);
                                }

                                break;
                            }
                        }
                        else if (result.IsCompleted)
                        {
                            break;
                        }
                    }
                    finally
                    {
                        _application.Input.AdvanceTo(buffer.End);
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                if (WebSocketCanSend(socket))
                {
                    try
                    {
                        await socket.CloseAsync(
                            error != null
                                ? WebSocketCloseStatus.InternalServerError
                                : WebSocketCloseStatus.NormalClosure,
                            string.Empty,
                            _stopCts.Token
                        ).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log.ClosingWebSocketFailed(_logger, ex);
                    }
                }

                _application.Input.Complete();

                Log.SendStopped(_logger);
            }
        }

        private static bool WebSocketCanSend(WebSocket ws)
        {
            return !(ws.State == WebSocketState.Aborted ||
                     ws.State == WebSocketState.Closed ||
                     ws.State == WebSocketState.CloseSent);
        }

        public async Task StopAsync()
        {
            // Transport is stopping.

            if (_application == null)
            {
                // We never started
                return;
            }

            _transport.Output.Complete();
            _transport.Input.Complete();

            // Cancel any pending reads from the application, this should start the entire shutdown process
            _application.Input.CancelPendingRead();

            // Start ungraceful close timer
            _stopCts.CancelAfter(_closeTimeout);

            try
            {
                await Running.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // exceptions have been handled in the Running task continuation by closing the channel with the exception
                Log.TransportStopped(_logger, ex);
            }
            finally
            {
                _webSocket?.Dispose();
                _stopCts.Dispose();
            }

            // Transport stopped.
        }
    }
}