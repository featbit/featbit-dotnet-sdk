using System;

namespace FeatBit.Sdk.Server.Options
{
    public sealed class FbOptions
    {
        /// <summary>
        /// The SDK key for your FeatBit environment.
        /// </summary>
        public string EnvSecret { get; set; }

        /// <summary>
        /// The base URI of the streaming service
        /// </summary>
        /// <value>Defaults to ws://localhost:5100</value>
        public Uri StreamingUri { get; set; }

        /// <summary>
        /// The base URI of the event service
        /// </summary>
        /// <value>Defaults to http://localhost:5100</value>
        public Uri EventUri { get; set; }

        /// <summary>
        /// How long the client constructor will block awaiting a successful connection to FeatBit.
        /// </summary>
        /// <remarks>
        /// This value must greater equal than 1 second.
        /// </remarks>
        /// <value>Defaults to 3 seconds</value>
        public TimeSpan StartWaitTime { get; set; }

        /// <summary>
        /// The connection timeout. This is the time allowed for the WebSocket client to connect to the server.
        /// </summary>
        /// <value>Defaults to 5 seconds</value>
        public TimeSpan ConnectTimeout { get; set; }

        /// <summary>
        /// The close timeout. This is the time allowed for the WebSocket client to perform a graceful shutdown.
        /// </summary>
        /// <value>Defaults to 2 seconds</value>
        public TimeSpan CloseTimeout { get; set; }

        /// <summary>
        /// The frequency at which to send Ping message.
        /// </summary>
        /// <value>Defaults to 15 seconds</value>
        public TimeSpan KeepAliveInterval { get; set; }

        /// <summary>
        /// The connection retry delays.
        /// </summary>
        public TimeSpan[] ReconnectRetryDelays { get; set; }

        /// <summary>
        /// Creates an option with all parameters set to the default.
        /// </summary>
        /// <param name="secret">the secret for your FeatBit environment</param>
        /// <returns>a <c>FbOptions</c> instance</returns>
        public static FbOptions Default(string secret)
        {
            return new FbOptionsBuilder(secret).Build();
        }

        internal FbOptions(
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

        internal FbOptions ShallowCopy()
        {
            var newOptions = new FbOptions(EnvSecret, StreamingUri, EventUri, StartWaitTime, ConnectTimeout,
                CloseTimeout, KeepAliveInterval, ReconnectRetryDelays);

            return newOptions;
        }
    }
}