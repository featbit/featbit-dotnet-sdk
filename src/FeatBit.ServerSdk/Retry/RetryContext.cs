namespace FeatBit.Sdk.Server.Retry
{
    /// <summary>
    /// The context passed to <see cref="IRetryPolicy.NextRetryDelay"/> to help the policy determine
    /// how long to wait before the next retry and whether there should be another retry at all.
    /// </summary>
    public class RetryContext
    {
        /// <summary>
        /// The number of consecutive failed retries so far.
        /// </summary>
        public int RetryAttempt { get; set; }
    }
}