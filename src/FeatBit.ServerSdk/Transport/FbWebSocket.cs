using System;
using System.Buffers;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FeatBit.Sdk.Server.Options;
using FeatBit.Sdk.Server.Retry;
using Microsoft.Extensions.Logging;

namespace FeatBit.Sdk.Server.Transport
{
    internal sealed partial class FbWebSocket
    {
        public event Func<Task> OnConnected;
        public event Func<Task> OnKeepAlive;
        public event Func<Exception, Task> OnReconnecting;
        public event Func<Task> OnReconnected;
        public event Func<Exception, WebSocketCloseStatus?, string, Task> OnClosed;
        public event Func<ReadOnlySequence<byte>, Task> OnReceived;

        private static readonly ReadOnlyMemory<byte> PingMessage =
            new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("{\"messageType\":\"ping\",\"data\":{}}"));

        private readonly FbOptions _options;
        private readonly Func<FbOptions, WebSocketTransport> _transportFactory;
        private readonly Func<FbOptions, Uri> _webSocketUriResolver;
        private WebSocketTransport _transport;
        private Task _receiveTask;
        private readonly TimeSpan _keepAliveInterval;
        private Timer _keepAliveTimer;
        private Exception _closeException;
        private readonly IRetryPolicy _retryPolicy;
        private CancellationTokenSource _stopCts = new CancellationTokenSource();
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<FbWebSocket> _logger;

        internal FbWebSocket(
            FbOptions options,
            Func<FbOptions, WebSocketTransport> transportFactory = null,
            Func<FbOptions, Uri> webSocketUriResolver = null)
        {
            _options = options;
            _transportFactory = transportFactory;
            _webSocketUriResolver = webSocketUriResolver;
            _keepAliveInterval = options.KeepAliveInterval;
            _retryPolicy = options.ReconnectRetryDelays?.Length > 0
                ? new DefaultRetryPolicy(options.ReconnectRetryDelays)
                : new DefaultRetryPolicy();
            _loggerFactory = options.LoggerFactory;
            _logger = _loggerFactory.CreateLogger<FbWebSocket>();
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default, bool isReconnecting = false)
        {
            Log.Starting(_logger);

            var webSocketUriResolver = _webSocketUriResolver ?? DefaultWebSocketUriResolver;
            var webSocketUri = webSocketUriResolver(_options);
            if (webSocketUri == null)
            {
                throw new InvalidOperationException("Configured WebSocketUriResolver did not return a value.");
            }

            var transportFactory = _transportFactory ?? DefaultWebSocketTransportFactory;
            var transport = transportFactory(_options);
            if (transport == null)
            {
                throw new InvalidOperationException("Configured WebSocketTransportFactory did not return a value.");
            }

            _transport = transport;
            try
            {
                // starts the transport
                Log.StartingTransport(_logger, "WebSockets", webSocketUri);
                await transport.StartAsync(webSocketUri, _options.CloseTimeout, cancellationToken);
            }
            catch (Exception ex)
            {
                // don't log starting transport error when reconnecting
                if (!isReconnecting)
                {
                    Log.ErrorStartingTransport(_logger, ex);

                    // reconnect if we failed to start the transport
                    _ = ReconnectAsync();
                }

                throw;
            }

            Log.StartingReceiveLoop(_logger);
            _receiveTask = ReceiveLoop();

            Log.StartingKeepAliveTimer(_logger);
            _keepAliveTimer = new Timer(
                state => _ = KeepAliveAsync(),
                null,
                _keepAliveInterval,
                _keepAliveInterval
            );

            Log.InvokingEventHandler(_logger, nameof(OnConnected));
            _ = OnConnected?.Invoke();

            Log.Started(_logger);
        }

        private WebSocketTransport DefaultWebSocketTransportFactory(FbOptions options)
        {
            return new WebSocketTransport(options, _loggerFactory);
        }

        private static Uri DefaultWebSocketUriResolver(FbOptions options)
        {
            var token = ConnectionToken.New(options.EnvSecret);
            var websocketUri = new Uri(options.StreamingUri, $"streaming?type=server&token={token}");
            return websocketUri;
        }

        private async Task ReceiveLoop()
        {
            // Receive loop starting.

            var input = _transport.Input;
            try
            {
                while (true)
                {
                    var result = await input.ReadAsync().ConfigureAwait(false);
                    var buffer = result.Buffer;

                    try
                    {
                        if (result.IsCanceled)
                        {
                            Log.ReceiveLoopCanceled(_logger);
                            break;
                        }
                        else if (!buffer.IsEmpty)
                        {
                            Log.ProcessingMessage(_logger, buffer.Length);
                            while (TextMessageParser.TryParseMessage(ref buffer, out var payload))
                            {
                                Log.InvokingEventHandler(_logger, nameof(OnReceived));
                                _ = OnReceived?.Invoke(payload);
                            }
                        }

                        if (result.IsCompleted)
                        {
                            if (!buffer.IsEmpty)
                            {
                                throw new InvalidDataException("Connection terminated while reading a message.");
                            }

                            break;
                        }
                    }
                    finally
                    {
                        // The buffer was sliced up to where it was consumed, so we can just advance to the start.
                        // We mark examined as `buffer.End` so that if we didn't receive a full frame, we'll wait for more data
                        // before yielding the read again.
                        input.AdvanceTo(buffer.Start, buffer.End);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ServerDisconnectedWithError(_logger, ex);
                _closeException = ex;
            }
            finally
            {
                await HandleConnectionCloseAsync();
                Log.ReceiveLoopEnded(_logger);
            }
        }

        private async Task HandleConnectionCloseAsync()
        {
            try
            {
                Log.StoppingTransport(_logger);
                await _transport.StopAsync();
            }
            catch (Exception ex)
            {
                Log.ErrorStoppingTransport(_logger, ex);
            }

            Log.StoppingKeepAliveTimer(_logger);
            _keepAliveTimer?.Dispose();

            if (ShouldReconnect())
            {
                _ = ReconnectAsync();
            }
            else
            {
                CompleteClose(_closeException);
            }
        }

        private bool ShouldReconnect()
        {
            var closeStatus = _transport.CloseStatus;
            return closeStatus != WebSocketCloseStatus.NormalClosure && closeStatus != (WebSocketCloseStatus)4003;
        }

        private async Task ReconnectAsync()
        {
            var reconnectStartTime = DateTime.UtcNow;
            if (_closeException != null)
            {
                Log.ReconnectingWithError(_logger, _closeException);
            }
            else
            {
                Log.Reconnecting(_logger);
            }

            Log.InvokingEventHandler(_logger, nameof(OnReconnecting));
            _ = OnReconnecting?.Invoke(_closeException).ConfigureAwait(false);

            var retryTimes = 0;
            while (true)
            {
                if (!ShouldReconnect())
                {
                    Log.GiveUpReconnect(_logger, _transport.CloseStatus);
                    CompleteClose(_closeException);
                    return;
                }

                var nextRetryDelay = _retryPolicy.NextRetryDelay(new RetryContext { RetryAttempt = retryTimes });
                try
                {
                    Log.AwaitingReconnectRetryDelay(_logger, retryTimes, nextRetryDelay);
                    await Task.Delay(nextRetryDelay, _stopCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException ex)
                {
                    Log.ReconnectingStoppedDuringRetryDelay(_logger);
                    var stoppedEx =
                        new Exception("FbWebSocket stopped during reconnect delay. Done reconnecting.", ex);
                    CompleteClose(stoppedEx);

                    return;
                }

                try
                {
                    await ConnectAsync(_stopCts.Token, isReconnecting: true).ConfigureAwait(false);

                    Log.Reconnected(_logger, retryTimes, DateTime.UtcNow - reconnectStartTime);

                    // reset _closeException
                    _closeException = null;

                    Log.InvokingEventHandler(_logger, nameof(OnReconnected));
                    _ = OnReconnected?.Invoke();
                    return;
                }
                catch (Exception ex)
                {
                    Log.ReconnectAttemptFailed(_logger, ex);
                    if (_stopCts.IsCancellationRequested)
                    {
                        Log.ReconnectingStoppedDuringReconnectAttempt(_logger);

                        var stoppedEx =
                            new Exception("Connection stopped during reconnect attempt. Done reconnecting.", ex);
                        CompleteClose(stoppedEx);

                        return;
                    }
                }

                retryTimes++;
            }
        }

        private void CompleteClose(Exception exception)
        {
            if (exception != null)
            {
                Log.ShuttingDownWithError(_logger, exception);
            }
            else
            {
                Log.ShuttingDown(_logger);
            }

            _stopCts = new CancellationTokenSource();

            Log.InvokingEventHandler(_logger, nameof(OnClosed));
            _ = OnClosed?.Invoke(exception, _transport.CloseStatus, _transport.CloseDescription).ConfigureAwait(false);

            if (_transport.CloseStatus.HasValue && _transport.CloseStatus != WebSocketCloseStatus.NormalClosure)
            {
                Log.AbnormallyClosed(_logger, _transport.CloseStatus, _transport.CloseDescription);
            }
        }

        private async Task KeepAliveAsync(CancellationToken ct = default)
        {
            await SendAsync(PingMessage, ct);

            Log.InvokingEventHandler(_logger, nameof(OnKeepAlive));
            _ = OnKeepAlive?.Invoke();
        }

        public async Task SendAsync(ReadOnlyMemory<byte> source, CancellationToken ct = default)
        {
            if (_transport.State == WebSocketState.Open)
            {
                await _transport.Output.WriteAsync(source, ct);
            }
            else
            {
                Log.FailedToSendMessage(_logger, _transport.State);
            }
        }

        public async Task CloseAsync()
        {
            // _transport = null indicates we never started or stopped already
            if (_transport != null)
            {
                _stopCts.Cancel();
                _keepAliveTimer?.Dispose();

                Log.TerminatingReceiveLoop(_logger);
                _transport.Input.CancelPendingRead();

                Log.WaitingForReceiveLoopToTerminate(_logger);
                await (_receiveTask ?? Task.CompletedTask).ConfigureAwait(false);
            }

            Log.Closed(_logger, _transport?.CloseStatus);
            _transport = null;
        }
    }
}