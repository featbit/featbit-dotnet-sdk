using System;
using System.Collections.Generic;

namespace FeatBit.Sdk.Server.Retry
{
    internal sealed class DefaultRetryPolicy : IRetryPolicy
    {
        internal static readonly TimeSpan[] DefaultRetryDelays =
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
        private readonly Random _random;
        private readonly object _randomLock = new object();

        public DefaultRetryPolicy() : this(DefaultRetryDelays)
        {
        }

        /// <param name="retryDelays">
        /// The delay before each retry attempt. Once the attempts exceed the table, the last delay is reused.
        /// </param>
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

            // seed explicitly: on .NET Framework, Random() instances created in the same tick share a seed
            _random = new Random(Guid.NewGuid().GetHashCode());
        }

        public TimeSpan NextRetryDelay(RetryContext retryContext)
        {
            // stay at the last (largest) delay instead of wrapping back to the start of the table
            var index = Math.Min(Math.Max(retryContext.RetryAttempt, 0), _retryDelays.Length - 1);
            var delay = _retryDelays[index];

            if (delay <= TimeSpan.Zero)
            {
                return delay;
            }

            double factor;
            lock (_randomLock)
            {
                factor = 0.5 + _random.NextDouble() * 0.5;
            }

            return TimeSpan.FromTicks((long)(delay.Ticks * factor));
        }
    }
}
