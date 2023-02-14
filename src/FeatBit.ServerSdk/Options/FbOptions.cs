using System;
using FeatBit.Sdk.Server.Transport;

namespace FeatBit.Sdk.Server.Options
{
    public class FbOptions
    {
        public string EnvSecret { get; set; }

        public Uri StreamingUri { get; set; }

        public Uri EventUri { get; set; }

        public TimeSpan StartWaitTime { get; set; }

        public TimeSpan ConnectTimeout { get; set; }

        public TimeSpan CloseTimeout { get; set; }

        public TimeSpan KeepAliveInterval { get; set; }

        public TimeSpan[] ReconnectRetryDelays { get; set; }

        public FbOptions(
            string envSecret,
            Uri streamingUri,
            Uri eventUri,
            TimeSpan startWaitTime,
            TimeSpan connectTimeout,
            TimeSpan closeTimeout,
            TimeSpan keepAliveInterval,
            TimeSpan[] reconnectRetryDelays)
        {
            EnvSecret = envSecret;
            StreamingUri = streamingUri;
            EventUri = eventUri;
            StartWaitTime = startWaitTime;
            ConnectTimeout = connectTimeout;
            CloseTimeout = closeTimeout;
            KeepAliveInterval = keepAliveInterval;
            ReconnectRetryDelays = reconnectRetryDelays;
        }

        internal Uri ResolveWebSocketUri()
        {
            var token = ConnectionToken.New(EnvSecret);

            var webSocketUri = new UriBuilder(StreamingUri)
            {
                Path = "streaming",
                Query = $"?type=server&token={token}"
            }.Uri;

            return webSocketUri;
        }

        internal FbOptions ShallowCopy()
        {
            var newOptions = new FbOptions(EnvSecret, StreamingUri, EventUri, StartWaitTime, ConnectTimeout,
                CloseTimeout, KeepAliveInterval, ReconnectRetryDelays);

            return newOptions;
        }
    }
}