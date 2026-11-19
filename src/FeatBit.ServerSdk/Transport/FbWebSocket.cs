using System;
using System.Buffers;
using System.Diagnostics;
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
        internal const WebSocketCloseStatus ServerRejectedCloseStatus = (WebSocketCloseStatus)4003;

        public event Func<Task> OnConnected;
        public event Func<Task> OnKeepAlive;
        public event Func<Exception, Task> OnReconnecting;
        public event Func<Task> OnReconnected;
        public event Func<Exception, WebSocketCloseStatus?, string, string, Task> OnClosed;
        public event Func<ReadOnlySequence<byte>, Task> OnReceived;

        private static readonly ReadOnlyMemory<byte> PingMessage =
            new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("{\"messageType\":\"ping\",\"data\":{}}"));

        private static readonly byte[] PongMarker = Encoding.UTF8.GetBytes("\"pong\"");

        private readonly FbOptions _options;
        private readonly Func<FbOptions, WebSocketTransport> _transportFactory;
        private readonly Func<FbOptions, Uri> _webSocketUriResolver;
        private WebSocketTransport _transport;
        private Task _receiveTask;
        private Task _reconnectTask;
        private readonly TimeSpan _keepAliveInterval;
        private readonly TimeSpan _keepAliveTimeout;
        private Timer _keepAliveTimer;
        private Exception _closeException;
        private readonly IRetryPolicy _retryPolicy;
        private CancellationTokenSource _stopCts = new CancellationTokenSource();
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<FbWebSocket> _logger;

        // liveness: Stopwatch timestamp of the last message received on the current connection, and
        // whether the server on the current connection has answered a ping (timeout is only enforced then)
        private long _lastReceivedTimestamp;
        private volatile bool _serverAnswersPing;

        // reconnect backoff: only reset once a connection has proven healthy (see MarkConnectionHealthy),
        // so a server that accepts and immediately closes the socket still gets an increasing delay
        private int _retryAttempt;
        private volatile bool _hasBeenHealthy;

        // incremented for every established connection; lets timers abort only the connection they belong to
        private int _connectionId;

        /// <summary>
        /// Identifies the current connection. Pass it to <see cref="AbortConnection"/> so that a check started
        /// for one connection can never tear down a later one.
        /// </summary>
        internal int ConnectionId => Volatile.Read(ref _connectionId);

        internal FbWebSocket(
            FbOptions options,
            Func<FbOptions, WebSocketTransport> transportFactory = null,
            Func<FbOptions, Uri> webSocketUriResolver = null)
        {
            _options = options;
            _transportFactory = transportFactory;
            _webSocketUriResolver = webSocketUriResolver;
            _keepAliveInterval = options.KeepAliveInterval;

            // never shorter than two keep-alive intervals, even if KeepAliveInterval was changed after Build()
            var minTimeout = TimeSpan.FromTicks(options.KeepAliveInterval.Ticks * 2);
            _keepAliveTimeout = options.KeepAliveTimeout > minTimeout ? options.KeepAliveTimeout : minTimeout;

            _retryPolicy = options.ReconnectRetryDelays?.Length > 0
                ? new DefaultRetryPolicy(options.ReconnectRetryDelays)
                : new DefaultRetryPolicy();
            _loggerFactory = options.LoggerFactory;
            _logger = _loggerFactory.CreateLogger<FbWebSocket>();
        }

        public async Task ConnectAsync(bool isReconnecting = false, CancellationToken cancellationToken = default)
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
                await transport.StartAsync(webSocketUri, cancellationToken);
            }
            catch (Exception ex)
            {
                // don't log starting transport error when reconnecting
                if (!isReconnecting)
                {
                    Log.ErrorStartingTransport(_logger, ex);

                    // reconnect if we failed to start the transport
                    _reconnectTask = ReconnectAsync();
                }

                throw;
            }

            // a new connection starts with a fresh liveness clock and must prove it answers pings again
            _serverAnswersPing = false;
            Interlocked.Exchange(ref _lastReceivedTimestamp, Stopwatch.GetTimestamp());
            var connectionId = Interlocked.Increment(ref _connectionId);

            Log.StartingReceiveLoop(_logger);
            _receiveTask = ReceiveLoop();

            Log.StartingKeepAliveTimer(_logger);
            _keepAliveTimer = new Timer(
                _ => _ = KeepAliveAsync(connectionId),
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
                                Interlocked.Exchange(ref _lastReceivedTimestamp, Stopwatch.GetTimestamp());
                                if (!_serverAnswersPing && IsPong(payload))
                                {
                                    _serverAnswersPing = true;
                                }

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
                if (_transport.CloseStatus == ServerRejectedCloseStatus)
                {
                    // we have synchronized with this secret before, so treat the rejection as transient
                    Log.ServerRejectedRetrying(_logger, _transport.CloseDescription);
                }

                _reconnectTask = ReconnectAsync();
            }
            else
            {
                CompleteClose(exception: _closeException);
            }
        }

        private bool ShouldReconnect() =>
            ShouldReconnect(_transport.CloseStatus, _stopCts.IsCancellationRequested, _hasBeenHealthy);

        internal static bool ShouldReconnect(
            WebSocketCloseStatus? closeStatus,
            bool stopRequested,
            bool hasBeenHealthy = false)
        {
            if (stopRequested)
            {
                // connection was stopped manually, so we should not reconnect
                return false;
            }

            if (closeStatus == ServerRejectedCloseStatus)
            {
                // The server rejected the connection. If this client has never synchronized, the configuration
                // is most likely wrong (e.g. invalid secret), so give up. If it has synchronized before with
                // the same configuration, the rejection is likely transient (e.g. the server's secret store
                // is still repopulating, or a temporary clock skew), so keep retrying with backoff.
                return hasBeenHealthy;
            }

            // otherwise, we should reconnect
            return true;
        }

        /// <summary>
        /// Called once the current connection has proven healthy (e.g. a data-sync reply was applied).
        /// Resets the reconnect backoff and allows retrying after a later server rejection.
        /// </summary>
        internal void MarkConnectionHealthy()
        {
            Interlocked.Exchange(ref _retryAttempt, 0);
            _hasBeenHealthy = true;
        }

        /// <summary>
        /// Tears down the current connection (without stopping the client) so the normal reconnect path runs.
        /// Used when the connection is open but no longer useful, e.g. it stopped answering.
        /// </summary>
        /// <param name="reason">Why the connection is being replaced (logged, and reported to OnReconnecting).</param>
        /// <param name="connectionId">
        /// The <see cref="ConnectionId"/> the caller observed when it started its check. If a different connection
        /// is current by now, the request is stale and ignored.
        /// </param>
        internal void AbortConnection(Exception reason, int connectionId)
        {
            if (connectionId != ConnectionId)
            {
                return;
            }

            var transport = _transport;
            if (transport == null || transport.State != WebSocketState.Open || _stopCts.IsCancellationRequested)
            {
                return;
            }

            Log.AbortingConnection(_logger, reason.Message);
            _closeException = reason;

            try
            {
                transport.Input.CancelPendingRead();
            }
            catch (Exception)
            {
                // the transport is already shutting down; the receive loop will handle the close
            }
        }

        private static bool IsPong(in ReadOnlySequence<byte> payload)
        {
            // pong is a tiny fixed message: {"messageType":"pong","data":{}}
            if (payload.Length > 128)
            {
                return false;
            }

            Span<byte> bytes = stackalloc byte[(int)payload.Length];
            payload.CopyTo(bytes);
            return bytes.IndexOf(PongMarker) >= 0;
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
                    CompleteClose(message: "Give up reconnecting.");
                    return;
                }

                // The attempt counter is shared across reconnect cycles and only reset by MarkConnectionHealthy,
                // so a server that accepts the socket and then closes it again keeps getting longer delays.
                var retryAttempt = Interlocked.Increment(ref _retryAttempt) - 1;
                var nextRetryDelay = _retryPolicy.NextRetryDelay(new RetryContext { RetryAttempt = retryAttempt });
                try
                {
                    Log.AwaitingReconnectRetryDelay(_logger, retryAttempt, nextRetryDelay);
                    await Task.Delay(nextRetryDelay, _stopCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    Log.ReconnectingStoppedDuringRetryDelay(_logger);
                    CompleteClose(message: "FbWebSocket stopped during reconnect delay. Done reconnecting.");

                    return;
                }

                try
                {
                    await ConnectAsync(isReconnecting: true, _stopCts.Token).ConfigureAwait(false);

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
                        CompleteClose(message: "FbWebSocket stopped during reconnect attempt. Done reconnecting.");

                        return;
                    }
                }

                retryTimes++;
            }
        }

        private void CompleteClose(Exception exception = null, string message = "")
        {
            if (exception != null)
            {
                Log.ShuttingDownWithError(_logger, exception);
            }

            if (message != null)
            {
                Log.ShuttingDown(_logger, message);
            }

            _stopCts = new CancellationTokenSource();

            Log.InvokingEventHandler(_logger, nameof(OnClosed));
            _ = OnClosed?.Invoke(exception, _transport.CloseStatus, _transport.CloseDescription, message).ConfigureAwait(false);

            if (_transport.CloseStatus.HasValue && _transport.CloseStatus != WebSocketCloseStatus.NormalClosure)
            {
                Log.AbnormallyClosed(_logger, _transport.CloseStatus, _transport.CloseDescription);
            }
        }

        private async Task KeepAliveAsync(int connectionId)
        {
            // a tick from the timer of an earlier connection (disposal does not stop in-flight callbacks)
            if (connectionId != ConnectionId)
            {
                return;
            }

            // Liveness check: the server answers every ping, so if it has done so before on this connection
            // and nothing at all has arrived for the keep-alive timeout, the connection is dead (half-open,
            // black-holed by a proxy/NAT, or the server is hung) even though the socket looks open.
            if (_serverAnswersPing)
            {
                var silentFor = ElapsedSince(Interlocked.Read(ref _lastReceivedTimestamp));
                if (silentFor > _keepAliveTimeout)
                {
                    AbortConnection(new TimeoutException(
                        $"No message received from the server for {silentFor.TotalSeconds:F0}s " +
                        $"(keep-alive timeout {_keepAliveTimeout.TotalSeconds:F0}s)."
                    ), connectionId);
                    return;
                }
            }

            try
            {
                await SendAsync(PingMessage);
            }
            catch (InvalidOperationException)
            {
                // the pipe write will throw InvalidOperationException if the connection is closed
                // catch and ignore it to avoid UnobservedTaskException
            }

            Log.InvokingEventHandler(_logger, nameof(OnKeepAlive));
            _ = OnKeepAlive?.Invoke();
        }

        private static TimeSpan ElapsedSince(long stopwatchTimestamp)
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - stopwatchTimestamp;
            return TimeSpan.FromSeconds((double)elapsedTicks / Stopwatch.Frequency);
        }

        public async Task SendAsync(ReadOnlyMemory<byte> source, CancellationToken ct = default)
        {
            // CloseAsync clears _transport; a late keep-alive tick must not throw
            var transport = _transport;
            var state = transport?.State ?? WebSocketState.Closed;
            if (state == WebSocketState.Open)
            {
                await transport.Output.WriteAsync(source, ct);
            }
            else
            {
                Log.FailedToSendMessage(_logger, state);
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

                await (_reconnectTask ?? Task.CompletedTask).ConfigureAwait(false);
            }

            Log.Closed(_logger, _transport?.CloseStatus);
            _transport = null;
        }
    }
}
