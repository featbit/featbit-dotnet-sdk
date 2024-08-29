using System;
using FeatBit.Sdk.Server.Bootstrapping;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FeatBit.Sdk.Server.Options
{
    public sealed class FbOptions
    {
        /// <summary>
        /// How long the client constructor will block awaiting a successful connection to FeatBit.
        /// </summary>
        /// <remarks>
        /// This value must greater than <see cref="ConnectTimeout"/>.
        /// </remarks>
        /// <value>Defaults to 5 seconds</value>
        public TimeSpan StartWaitTime { get; set; }

        /// <summary>
        /// Whether this client is offline. If true, no calls to FeatBit will be made.
        /// </summary>
        /// <value>Defaults to <c>false</c></value>
        public bool Offline { get; set; }

        /// <summary>
        /// The bootstrap provider.
        /// </summary>
        /// <value>Defaults to <see cref="NullBootstrapProvider"/></value>
        internal IBootstrapProvider BootstrapProvider { get; set; }

        /// <summary>
        /// Whether to disable events collections.
        /// </summary>
        public bool DisableEvents { get; set; }

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
        /// The connection timeout. This is the time allowed for the WebSocket client to connect to the server.
        /// </summary>
        /// <remarks>
        /// This value must lower then <see cref="StartWaitTime"/>.
        /// </remarks>
        /// <value>Defaults to 3 seconds</value>
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
        /// The event flush timeout.
        /// </summary>
        /// <value>Defaults to 5 seconds</value>
        public TimeSpan FlushTimeout { get; set; }

        /// <summary>
        /// The maximum number of flush workers.
        /// </summary>
        /// <value>Defaults to <c>Math.Min(Math.Max(Environment.ProcessorCount / 2, 1), 4)</c></value>
        public int MaxFlushWorker { get; set; }

        /// <summary>
        /// The time interval between each flush operation.
        /// </summary>
        /// <value>Defaults to 5 seconds</value>
        public TimeSpan AutoFlushInterval { get; set; }

        /// <summary>
        /// The maximum number of events in queue.
        /// </summary>
        /// <value>Defaults to 10_000</value>
        public int MaxEventsInQueue { get; set; }

        /// <summary>
        /// The maximum number of events per request. 
        /// </summary>
        /// <value>Defaults to 50</value>
        public int MaxEventPerRequest { get; set; }

        /// <summary>
        /// The maximum number of attempts to send an event before giving up.
        /// </summary>
        /// <value>Defaults to 2</value>
        public int MaxSendEventAttempts { get; set; }

        /// <summary>
        /// The time interval between each retry attempt to send an event.
        /// </summary>
        /// <value>Defaults to 200 milliseconds</value>
        public TimeSpan SendEventRetryInterval { get; set; }

        /// <summary>
        /// The logger factory used by FbClient.
        /// </summary>
        /// <value>Defaults to <see cref="NullLoggerFactory.Instance"/></value>
        public ILoggerFactory LoggerFactory { get; set; }

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
            TimeSpan startWaitTime,
            bool offline,
            bool disableEvents,
            string envSecret,
            Uri streamingUri,
            Uri eventUri,
            TimeSpan connectTimeout,
            TimeSpan closeTimeout,
            TimeSpan keepAliveInterval,
            TimeSpan[] reconnectRetryDelays,
            int maxFlushWorker,
            TimeSpan autoFlushInterval,
            TimeSpan flushTimeout,
            int maxEventsInQueue,
            int maxEventPerRequest,
            int maxSendEventAttempts,
            TimeSpan sendEventRetryInterval,
            IBootstrapProvider bootstrapProvider,
            ILoggerFactory loggerFactory)
        {
            StartWaitTime = startWaitTime;
            Offline = offline;
            DisableEvents = disableEvents;

            EnvSecret = envSecret;
            StreamingUri = streamingUri;
            EventUri = eventUri;
            ConnectTimeout = connectTimeout;
            CloseTimeout = closeTimeout;
            KeepAliveInterval = keepAliveInterval;
            ReconnectRetryDelays = reconnectRetryDelays;

            MaxFlushWorker = maxFlushWorker;
            AutoFlushInterval = autoFlushInterval;
            FlushTimeout = flushTimeout;
            MaxEventsInQueue = maxEventsInQueue;
            MaxEventPerRequest = maxEventPerRequest;
            MaxSendEventAttempts = maxSendEventAttempts;
            SendEventRetryInterval = sendEventRetryInterval;

            BootstrapProvider = bootstrapProvider;

            LoggerFactory = loggerFactory;
        }

        internal FbOptions ShallowCopy()
        {
            var newOptions = new FbOptions(StartWaitTime, Offline, DisableEvents, EnvSecret, StreamingUri, EventUri,
                ConnectTimeout, CloseTimeout, KeepAliveInterval, ReconnectRetryDelays, MaxFlushWorker,
                AutoFlushInterval, FlushTimeout, MaxEventsInQueue, MaxEventPerRequest, MaxSendEventAttempts,
                SendEventRetryInterval, BootstrapProvider, LoggerFactory);

            return newOptions;
        }
    }
}