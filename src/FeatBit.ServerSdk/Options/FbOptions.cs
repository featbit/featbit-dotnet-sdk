using System;

namespace FeatBit.Sdk.Server.Options
{
    public class FbOptions
    {
        public Uri EventUri { get; set; }

        public Uri StreamingUri { get; set; }

        public TimeSpan ConnectTimeout { get; set; }

        public TimeSpan CloseTimeout { get; set; }

        public TimeSpan KeepAliveInterval { get; set; }

        public TimeSpan[] ReconnectRetryDelays { get; set; }

        public FbOptions(
            Uri eventUri,
            Uri streamingUri,
            TimeSpan connectTimeout,
            TimeSpan closeTimeout,
            TimeSpan keepAliveInterval,
            TimeSpan[] reconnectRetryDelays)
        {
            EventUri = eventUri;
            StreamingUri = streamingUri;
            ConnectTimeout = connectTimeout;
            CloseTimeout = closeTimeout;
            KeepAliveInterval = keepAliveInterval;
            ReconnectRetryDelays = reconnectRetryDelays;
        }
    }
}