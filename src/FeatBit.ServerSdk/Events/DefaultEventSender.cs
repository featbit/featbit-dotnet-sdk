using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FeatBit.Sdk.Server.Http;
using FeatBit.Sdk.Server.Options;
using Microsoft.Extensions.Logging;

namespace FeatBit.Sdk.Server.Events
{
    internal partial class DefaultEventSender : IEventSender
    {
        private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(1);

        private readonly Uri _eventUri;
        private const string EventPath = "api/public/insight/track";
        private readonly int _maxAttempts;
        private readonly TimeSpan _retryInterval;
        private readonly TimeSpan _timeout;

        private readonly HttpClient _httpClient;

        private static readonly MediaTypeHeaderValue JsonContentType = new MediaTypeHeaderValue("application/json")
        {
            CharSet = "utf-8"
        };

        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly ILogger<DefaultEventSender> _logger;

        public DefaultEventSender(FbOptions options, HttpClient httpClient = null)
        {
            _httpClient = httpClient ?? NewHttpClient();
            AddDefaultHeaders(options);

            _eventUri = new Uri(options.EventUri, EventPath);
            _maxAttempts = options.MaxSendEventAttempts;
            _retryInterval = options.SendEventRetryInterval;
            _timeout = options.SendEventTimeout;

            _logger = options.LoggerFactory.CreateLogger<DefaultEventSender>();
        }

        public async Task<DeliveryStatus> SendAsync(byte[] payload)
        {
            for (var attempt = 0; attempt < _maxAttempts; attempt++)
            {
                if (attempt > 0)
                {
                    await Task.Delay(_retryInterval);
                }

                bool isRecoverable;
                string error;
                using var cts = new CancellationTokenSource(_timeout);

                try
                {
                    var response = await SendCoreAsync(payload, cts);
                    if (response.IsSuccessStatusCode)
                    {
                        return DeliveryStatus.Succeeded;
                    }

                    var status = (int)response.StatusCode;
                    error = $"HTTP {status} {response.ReasonPhrase}";
                    if (status == 413)
                    {
                        Log.SendFailed(_logger, error);
                        return DeliveryStatus.PayloadTooLarge;
                    }

                    isRecoverable = HttpErrors.IsRecoverable(status);
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    // Our own per-attempt timeout elapsed: the server is slow, which is transient. Retry.
                    Log.SendTaskWasCanceled(_logger);
                    isRecoverable = true;
                    error = $"request timed out after {_timeout.TotalSeconds:F1}s";
                }
                catch (Exception ex)
                {
                    Log.ErrorSendEvent(_logger, ex);
                    isRecoverable = true;
                    error = ex.Message;
                }

                Log.SendFailed(_logger, error);
                if (!isRecoverable)
                {
                    return DeliveryStatus.Rejected;
                }
            }

            Log.SendFailed(_logger, "Reconnect retries have been exhausted after max failed attempts.");
            return DeliveryStatus.Failed;
        }

        private async Task<HttpResponseMessage> SendCoreAsync(byte[] payload, CancellationTokenSource cts)
        {
            // check log level to avoid unnecessary string allocation
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                var body = Encoding.UTF8.GetString(payload);
                Log.SendStarted(_logger, body);
            }

            using var content = new ByteArrayContent(payload);
            content.Headers.ContentType = JsonContentType;

            _stopwatch.Restart();
            using var response = await _httpClient.PostAsync(_eventUri, content, cts.Token);
            _stopwatch.Stop();

            Log.SendFinished(_logger, _stopwatch.ElapsedMilliseconds, (int)response.StatusCode);

            return response;
        }

        private static HttpClient NewHttpClient()
        {
#if NETCOREAPP
            var handler = new SocketsHttpHandler
            {
                ConnectTimeout = DefaultConnectTimeout
            };
#else
            var handler = new HttpClientHandler();
#endif
            var client = new HttpClient(handler, false)
            {
                // each attempt is bounded by SendEventTimeout; HttpClient's own 100s default would otherwise
                // cut off attempts when SendEventTimeout is configured above 100 seconds
                Timeout = System.Threading.Timeout.InfiniteTimeSpan
            };
            return client;
        }

        private void AddDefaultHeaders(FbOptions options)
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", options.EnvSecret);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", HttpConstants.UserAgent);
        }
    }
}
