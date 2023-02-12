using System;

namespace FeatBit.Sdk.Server.Retry
{
    public class BackoffAndJitterRetryPolicy : IRetryPolicy
    {
        private static readonly Random Random = new Random();

        private readonly TimeSpan _firstRetryDelay;
        private readonly TimeSpan _maxRetryDelay;
        private readonly double _jitterRatio;

        private static readonly TimeSpan DefaultFirstRetryDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan DefaultMaxRetryDelay = TimeSpan.FromSeconds(30);

        public BackoffAndJitterRetryPolicy(TimeSpan? firstRetryDelay = null, TimeSpan? maxRetryDelay = null)
        {
            _firstRetryDelay = firstRetryDelay ?? DefaultFirstRetryDelay;
            _maxRetryDelay = maxRetryDelay ?? DefaultMaxRetryDelay;
            _jitterRatio = 0.5d;
        }

        public TimeSpan NextRetryDelay(RetryContext retryContext)
        {
            var backoffTime = Backoff(retryContext.RetryAttempt);
            var delay = (long)Math.Round(Jitter(backoffTime) + backoffTime / 2);

            return TimeSpan.FromMilliseconds(delay);
        }

        private double Backoff(int retryAttempt)
        {
            var delay = _firstRetryDelay.TotalMilliseconds * Math.Pow(2, retryAttempt);
            var max = _maxRetryDelay.TotalMilliseconds;
            return Math.Min(delay, max);
        }

        private double Jitter(double backoff)
        {
            return backoff * _jitterRatio * Random.NextDouble();
        }
    }
}