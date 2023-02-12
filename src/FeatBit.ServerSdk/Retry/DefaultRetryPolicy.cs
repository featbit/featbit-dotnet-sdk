using System;
using System.Collections.Generic;

namespace FeatBit.Sdk.Server.Retry
{
    internal sealed class DefaultRetryPolicy : IRetryPolicy
    {
        private static readonly TimeSpan[] DefaultRetryDelays =
        {
            // retry immediately for the first
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(8),
            TimeSpan.FromSeconds(13),
            TimeSpan.FromSeconds(21),
            TimeSpan.FromSeconds(34),
            TimeSpan.FromSeconds(55)
        };

        private readonly TimeSpan[] _retryDelays;

        public DefaultRetryPolicy()
        {
            _retryDelays = DefaultRetryDelays;
        }

        public DefaultRetryPolicy(IReadOnlyList<TimeSpan> retryDelays)
        {
            if (retryDelays == null || retryDelays.Count == 0)
            {
                throw new ArgumentException("retry delays cannot be null or empty", nameof(retryDelays));
            }

            _retryDelays = new TimeSpan[retryDelays.Count];
            for (var i = 0; i < retryDelays.Count; i++)
            {
                _retryDelays[i] = retryDelays[i];
            }
        }

        public TimeSpan NextRetryDelay(RetryContext retryContext)
        {
            var index = retryContext.RetryAttempt % _retryDelays.Length;
            return _retryDelays[index];
        }
    }
}