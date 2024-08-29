using System;
using FeatBit.Sdk.Server.Bootstrapping;
using FeatBit.Sdk.Server.Retry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FeatBit.Sdk.Server.Options
{
    public class FbOptionsBuilder
    {
        private TimeSpan _startWaitTime;
        private bool _offline;
        private bool _disableEvents;

        private readonly string _envSecret;

        private Uri _streamingUri;
        private Uri _eventUri;

        private TimeSpan _connectTimeout;
        private TimeSpan _closeTimeout;
        private TimeSpan _keepAliveInterval;
        private TimeSpan[] _reconnectRetryDelays;

        private int _maxFlushWorker;
        private TimeSpan _autoFlushInterval;
        private TimeSpan _flushTimeout;
        private int _maxEventsInQueue;
        private int _maxEventPerRequest;
        private int _maxSendEventAttempts;
        private TimeSpan _sendEventRetryInterval;

        private IBootstrapProvider _bootstrapProvider;

        private ILoggerFactory _loggerFactory;

        public FbOptionsBuilder() : this(string.Empty)
        {
        }

        public FbOptionsBuilder(string envSecret)
        {
            _startWaitTime = TimeSpan.FromSeconds(5);
            _offline = false;
            _disableEvents = false;

            _envSecret = envSecret;

            // uris
            _streamingUri = new Uri("ws://localhost:5100");
            _eventUri = new Uri("http://localhost:5100");

            // websocket configs
            _connectTimeout = TimeSpan.FromSeconds(3);
            _closeTimeout = TimeSpan.FromSeconds(2);
            _keepAliveInterval = TimeSpan.FromSeconds(15);
            _reconnectRetryDelays = DefaultRetryPolicy.DefaultRetryDelays;

            // event configs
            _maxFlushWorker = Math.Min(Math.Max(Environment.ProcessorCount / 2, 1), 4);
            _autoFlushInterval = TimeSpan.FromSeconds(5);
            _flushTimeout = TimeSpan.FromSeconds(5);
            _maxEventsInQueue = 10_000;
            _maxEventPerRequest = 50;
            _maxSendEventAttempts = 2;
            _sendEventRetryInterval = TimeSpan.FromMilliseconds(200);

            _bootstrapProvider = new NullBootstrapProvider();

            _loggerFactory = NullLoggerFactory.Instance;
        }

        public FbOptions Build()
        {
            return new FbOptions(_startWaitTime, _offline, _disableEvents, _envSecret, _streamingUri, _eventUri,
                _connectTimeout, _closeTimeout, _keepAliveInterval, _reconnectRetryDelays, _maxFlushWorker,
                _autoFlushInterval, _flushTimeout, _maxEventsInQueue, _maxEventPerRequest, _maxSendEventAttempts,
                _sendEventRetryInterval, _bootstrapProvider, _loggerFactory);
        }

        public FbOptionsBuilder StartWaitTime(TimeSpan timeout)
        {
            if (timeout < _connectTimeout)
            {
                throw new InvalidOperationException("The start wait time must be greater than the connect timeout.");
            }

            _startWaitTime = timeout;
            return this;
        }

        public FbOptionsBuilder Offline(bool offline)
        {
            _offline = offline;
            return this;
        }

        public FbOptionsBuilder Streaming(Uri uri)
        {
            _streamingUri = uri;
            return this;
        }

        public FbOptionsBuilder Event(Uri uri)
        {
            _eventUri = uri;
            return this;
        }

        public FbOptionsBuilder ConnectTimeout(TimeSpan timeout)
        {
            if (timeout > _startWaitTime)
            {
                throw new InvalidOperationException("The connect timeout must be lower than the start wait time.");
            }

            _connectTimeout = timeout;
            return this;
        }

        public FbOptionsBuilder CloseTimeout(TimeSpan timeout)
        {
            _closeTimeout = timeout;
            return this;
        }

        public FbOptionsBuilder DisableEvents(bool disableEvents)
        {
            _disableEvents = disableEvents;
            return this;
        }

        public FbOptionsBuilder MaxFlushWorker(int maxFlushWorker)
        {
            _maxFlushWorker = maxFlushWorker;
            return this;
        }

        public FbOptionsBuilder AutoFlushInterval(TimeSpan autoFlushInterval)
        {
            _autoFlushInterval = autoFlushInterval;
            return this;
        }

        public FbOptionsBuilder FlushTimeout(TimeSpan timeout)
        {
            _flushTimeout = timeout;
            return this;
        }

        public FbOptionsBuilder MaxEventsInQueue(int maxEventsInQueue)
        {
            _maxEventsInQueue = maxEventsInQueue;
            return this;
        }

        public FbOptionsBuilder MaxEventPerRequest(int maxEventPerRequest)
        {
            _maxEventPerRequest = maxEventPerRequest;
            return this;
        }

        public FbOptionsBuilder MaxSendEventAttempts(int maxSendEventAttempts)
        {
            _maxSendEventAttempts = maxSendEventAttempts;
            return this;
        }

        public FbOptionsBuilder SendEventRetryInterval(TimeSpan sendEventRetryInterval)
        {
            _sendEventRetryInterval = sendEventRetryInterval;
            return this;
        }

        public FbOptionsBuilder KeepAliveInterval(TimeSpan interval)
        {
            _keepAliveInterval = interval;
            return this;
        }

        public FbOptionsBuilder ReconnectRetryDelays(TimeSpan[] delays)
        {
            _reconnectRetryDelays = delays;
            return this;
        }

        public FbOptionsBuilder LoggerFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            return this;
        }

        public FbOptionsBuilder UseJsonBootstrapProvider(string json)
        {
            if (!_offline)
            {
                throw new InvalidOperationException("Bootstrap provider can only be set when offline mode is enabled.");
            }

            _bootstrapProvider = new JsonBootstrapProvider(json);
            return this;
        }
    }
}