using System;
using FeatBit.Sdk.Server.Retry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FeatBit.Sdk.Server.Options
{
    public class FbOptionsBuilder
    {
        private readonly string _envSecret;
        private Uri _streamingUri;
        private Uri _eventUri;
        private TimeSpan _startWaitTime;
        private TimeSpan _connectTimeout;
        private TimeSpan _closeTimeout;
        private TimeSpan _keepAliveInterval;
        private TimeSpan[] _reconnectRetryDelays;
        private ILoggerFactory _loggerFactory;

        public FbOptionsBuilder(string envSecret)
        {
            _envSecret = envSecret;

            // default values
            _streamingUri = new Uri("ws://localhost:5100");
            _eventUri = new Uri("http://localhost:5100");
            _startWaitTime = TimeSpan.FromSeconds(3);
            _connectTimeout = TimeSpan.FromSeconds(5);
            _closeTimeout = TimeSpan.FromSeconds(2);
            _keepAliveInterval = TimeSpan.FromSeconds(15);
            _reconnectRetryDelays = DefaultRetryPolicy.DefaultRetryDelays;
            _loggerFactory = NullLoggerFactory.Instance;
        }

        public FbOptions Build()
        {
            return new FbOptions(_envSecret, _streamingUri, _eventUri, _startWaitTime, _connectTimeout, _closeTimeout,
                _keepAliveInterval, _reconnectRetryDelays, _loggerFactory);
        }

        public FbOptionsBuilder Steaming(Uri uri)
        {
            _streamingUri = uri;
            return this;
        }

        public FbOptionsBuilder Event(Uri uri)
        {
            _eventUri = uri;
            return this;
        }

        public FbOptionsBuilder StartWaitTime(TimeSpan waitTime)
        {
            _startWaitTime = waitTime;
            return this;
        }

        public FbOptionsBuilder ConnectTimeout(TimeSpan timeout)
        {
            _connectTimeout = timeout;
            return this;
        }

        public FbOptionsBuilder CloseTimeout(TimeSpan timeout)
        {
            _closeTimeout = timeout;
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
    }
}