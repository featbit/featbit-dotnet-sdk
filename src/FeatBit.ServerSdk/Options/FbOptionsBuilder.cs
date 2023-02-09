using System;

namespace FeatBit.Sdk.Server.Options
{
    public class FbOptionsBuilder
    {
        private Uri _streamingUri;
        private Uri _eventUri;
        private TimeSpan _connectTimeout;
        private TimeSpan _closeTimeout;
        private TimeSpan _keepAliveInterval;

        public FbOptionsBuilder()
        {
            _connectTimeout = TimeSpan.FromSeconds(5);
            _closeTimeout = TimeSpan.FromSeconds(2);
            _keepAliveInterval = TimeSpan.FromSeconds(15);
        }

        public FbOptions Build()
        {
            return new FbOptions(_eventUri, _streamingUri, _connectTimeout, _closeTimeout, _keepAliveInterval);
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
    }
}