using System;
using System.Buffers;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FeatBit.Sdk.Server.Options;
using FeatBit.Sdk.Server.Retry;

namespace FeatBit.Sdk.Server.Transport
{
    internal sealed class FbWebSocket
    {
        public event Func<Task> OnConnected;
        public event Action<Exception> OnConnectError;
        public event Func<Task> OnKeepAlive;
        public event Func<Exception, Task> OnReconnecting;
        public event Func<Task> OnReconnected;
        public event Func<Exception, WebSocketCloseStatus?, string, Task> OnClosed;
        public event Func<ReadOnlySequence<byte>, Task> OnReceived;

        private static readonly ReadOnlyMemory<byte> PingMessage =
            new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("{\"messageType\":\"ping\",\"data\":{}}"));

        private readonly FbOptions _options;
        private readonly WebSocketTransport _transport;
        private Task _receiveTask;
        private readonly TimeSpan _keepAliveInterval;
        private Timer _keepAliveTimer;
        private Exception _closeException;
        private readonly IRetryPolicy _retryPolicy;
        private CancellationTokenSource _stopCts = new CancellationTokenSource();

        internal FbWebSocket(FbOptions options, WebSocketTransport transport = null)
        {
            _options = options;
            _transport = transport ?? new WebSocketTransport();
            _keepAliveInterval = options.KeepAliveInterval;
            _retryPolicy = options.ReconnectRetryDelays?.Length > 0
                ? new DefaultRetryPolicy(options.ReconnectRetryDelays)
                : new DefaultRetryPolicy();
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _transport.StartAsync(_options.StreamingUri, _options.CloseTimeout, cancellationToken);

                _receiveTask = ReceiveLoop();
                _keepAliveTimer = new Timer(
                    state => _ = KeepAliveAsync(),
                    null,
                    _keepAliveInterval,
                    _keepAliveInterval
                );

                // Fire-and-forget the connected event
                _ = OnConnected?.Invoke();
            }
            catch (Exception ex)
            {
                OnConnectError?.Invoke(ex);
            }
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
                            // We were canceled. Possibly because we were stopped gracefully
                            break;
                        }
                        else if (!buffer.IsEmpty)
                        {
                            // Processing {MessageLength} byte message from server.
                            while (TextMessageParser.TryParseMessage(ref buffer, out var payload))
                            {
                                // Fire-and-forget the message received event
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
                // The server connection was terminated with an error.
                _closeException = ex;
            }
            finally
            {
                await HandleConnectionCloseAsync();
            }
        }

        private async Task HandleConnectionCloseAsync()
        {
            try
            {
                // Stop(Dispose) current transport
                await _transport.StopAsync();

                var closeStatus = _transport.CloseStatus;
                if (closeStatus == WebSocketCloseStatus.NormalClosure || closeStatus == (WebSocketCloseStatus)4003)
                {
                    CompleteClose(_closeException);
                }
                else
                {
                    // Fire-and-forget the reconnect action
                    _ = ReconnectAsync();
                }
            }
            catch
            {
                // The transport threw an exception while stopping.
            }
        }

        private async Task ReconnectAsync()
        {
            // Fire-and-forget the reconnecting event
            _ = OnReconnecting?.Invoke(_closeException).ConfigureAwait(false);

            var retryTimes = 0;
            while (true)
            {
                // Reconnect attempt number {retryTimes} will start in {nextRetryDelay}
                var nextRetryDelay = _retryPolicy.NextRetryDelay(new RetryContext { RetryAttempt = retryTimes });
                try
                {
                    await Task.Delay(nextRetryDelay, _stopCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException ex)
                {
                    var stoppedEx =
                        new Exception("FbWebSocket stopped during reconnect delay. Done reconnecting.", ex);
                    CompleteClose(stoppedEx);

                    return;
                }

                try
                {
                    await StartAsync(_stopCts.Token).ConfigureAwait(false);

                    // reconnected successfully after {retryTimes} attempts.

                    // Fire-and-forget the reconnected event
                    _ = OnReconnected?.Invoke();
                }
                catch (Exception ex)
                {
                    // Reconnect attempt failed.

                    // Connection stopped during reconnect attempt
                    if (_stopCts.IsCancellationRequested)
                    {
                        var stoppedEx =
                            new Exception("Connection stopped during reconnect attempt. Done reconnecting.", ex);
                        CompleteClose(stoppedEx);

                        return;
                    }

                    retryTimes++;
                }
            }
        }

        private void CompleteClose(Exception exception)
        {
            _stopCts = new CancellationTokenSource();

            // Fire-and-forget the closed event
            _ = OnClosed?.Invoke(exception, _transport.CloseStatus, _transport.CloseDescription)
                .ConfigureAwait(false);
        }

        private async Task KeepAliveAsync(CancellationToken ct = default)
        {
            await SendAsync(PingMessage, ct);

            // Fire-and-forget the ping event
            _ = OnKeepAlive?.Invoke();
        }

        public async Task SendAsync(ReadOnlyMemory<byte> source, CancellationToken ct = default)
            => await _transport.Output.WriteAsync(source, ct);

        public async Task StopAsync()
        {
            _stopCts.Cancel();

            _keepAliveTimer?.Dispose();

            // Terminating receive loop, which should cause everything to shut down
            _transport.Input.CancelPendingRead();

            // Waiting for the receive loop to terminate.
            await (_receiveTask ?? Task.CompletedTask).ConfigureAwait(false);
        }
    }
}