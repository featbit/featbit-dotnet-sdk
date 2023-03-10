using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FeatBit.Sdk.Server.Options
{
    public sealed class FbOptions
    {
        /// <summary>
        /// The SDK key for your FeatBit environment.
        /// </summary>
        public string EnvSecret { get; }

        /// <summary>
        /// The base URI of the streaming service
        /// </summary>
        /// <value>Defaults to ws://localhost:5100</value>
        public Uri StreamingUri { get; }

        /// <summary>
        /// The base URI of the event service
        /// </summary>
        /// <value>Defaults to http://localhost:5100</value>
        public Uri EventUri { get; }

        /// <summary>
        /// How long the client constructor will block awaiting a successful connection to FeatBit.
        /// </summary>
        /// <remarks>
        /// This value must greater equal than 1 second.
        /// </remarks>
        /// <value>Defaults to 5 seconds</value>
        public TimeSpan StartWaitTime { get; }

        /// <summary>
        /// The connection timeout. This is the time allowed for the WebSocket client to connect to the server.
        /// </summary>
        /// <value>Defaults to 3 seconds</value>
        public TimeSpan ConnectTimeout { get; }

        /// <summary>
        /// The close timeout. This is the time allowed for the WebSocket client to perform a graceful shutdown.
        /// </summary>
        /// <value>Defaults to 2 seconds</value>
        public TimeSpan CloseTimeout { get; }

        /// <summary>
        /// The frequency at which to send Ping message.
        /// </summary>
        /// <value>Defaults to 15 seconds</value>
        public TimeSpan KeepAliveInterval { get; }

        /// <summary>
        /// The connection retry delays.
        /// </summary>
        public TimeSpan[] ReconnectRetryDelays { get; }

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
        public ILoggerFactory LoggerFactory { get; }

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
            TimeSpan[] reconnectRetryDelays,
            int maxFlushWorker,
            TimeSpan autoFlushInterval,
            TimeSpan flushTimeout,
            int maxEventsInQueue,
            int maxEventPerRequest,
            int maxSendEventAttempts,
            TimeSpan sendEventRetryInterval,
            ILoggerFactory loggerFactory)
        {
            EnvSecret = envSecret;
            StreamingUri = streamingUri;
            EventUri = eventUri;
            StartWaitTime = startWaitTime;
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
            LoggerFactory = loggerFactory;
        }

        internal FbOptions ShallowCopy()
        {
            var newOptions = new FbOptions(EnvSecret, StreamingUri, EventUri, StartWaitTime, ConnectTimeout,
                CloseTimeout, KeepAliveInterval, ReconnectRetryDelays, MaxFlushWorker, AutoFlushInterval, FlushTimeout,
                MaxEventsInQueue, MaxEventPerRequest, MaxSendEventAttempts, SendEventRetryInterval, LoggerFactory);

            return newOptions;
        }
    }
}