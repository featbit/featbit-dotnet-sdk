namespace FeatBit.Sdk.Server.Events
{
    internal enum DeliveryStatus
    {
        Succeeded,

        /// <summary>
        /// A transient failure (timeout, network error, 5xx, 408, 429) that persisted through all attempts.
        /// </summary>
        Failed,

        /// <summary>
        /// The server rejected the request with a non-recoverable status (e.g. 401, 403, 404).
        /// Delivery should pause and resume later rather than retry immediately.
        /// </summary>
        Rejected,

        /// <summary>
        /// The server rejected the request as too large (413). The batch should be split.
        /// </summary>
        PayloadTooLarge
    }
}
