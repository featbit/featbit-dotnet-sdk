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
        private static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan DefaultTimeout = DefaultConnectTimeout + DefaultReadTimeout;

        private readonly Uri _eventUri;
        private const string EventPath = "/api/public/insight/track";
        private readonly int _maxAttempts;
        private readonly TimeSpan _retryInterval;

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

            _eventUri = new Uri(options.EventUri, $"{options.EventUri.LocalPath.TrimEnd('/')}{EventPath}");
            _maxAttempts = options.MaxSendEventAttempts;
            _retryInterval = options.SendEventRetryInterval;

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
                using var cts = new CancellationTokenSource(DefaultTimeout);

                try
                {
                    var response = await SendCoreAsync(payload, cts);
                    if (response.IsSuccessStatusCode)
                    {
                        return DeliveryStatus.Succeeded;
                    }

                    error = response.ReasonPhrase;
                    isRecoverable = HttpErrors.IsRecoverable((int)response.StatusCode);
                }
                catch (TaskCanceledException ex)
                {
                    if (ex.CancellationToken == cts.Token)
                    {
                        Log.SendTaskWasCanceled(_logger);

                        // The task was canceled due to a handle timeout, do not retry it.
                        return DeliveryStatus.Failed;
                    }

                    // Otherwise this was a request timeout.
                    isRecoverable = true;
                    error = "request timeout";
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
                    return DeliveryStatus.FailedAndMustShutDown;
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
#if NETCOREAPP || NET6_0
            var handler = new SocketsHttpHandler
            {
                ConnectTimeout = DefaultConnectTimeout
            };
#else
            var handler = new HttpClientHandler();
#endif
            var client = new HttpClient(handler, false);
            return client;
        }

        private void AddDefaultHeaders(FbOptions options)
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", options.EnvSecret);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "fb-dotnet-server-sdk");
        }
    }
}