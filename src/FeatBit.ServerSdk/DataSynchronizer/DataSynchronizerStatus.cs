namespace FeatBit.Sdk.Server.DataSynchronizer;

public enum DataSynchronizerStatus
{
    /// <summary>
    /// The initial state of the synchronizer when the SDK is being initialized.
    /// </summary>
    /// <remarks>
    /// If it encounters an error that requires it to retry initialization, the state will remain at
    /// <see cref="Starting"/> until it either succeeds and becomes <see cref="Stable"/>, or
    /// permanently fails and becomes <see cref="Stopped"/>.
    /// </remarks>
    Starting,

    /// <summary>
    /// Indicates that the synchronizer is currently operational and has not had any problems since the
    /// last time it received data.
    /// </summary>
    /// <remarks>
    /// In streaming mode, this means that there is currently an open stream connection and that at least
    /// one initial message has been received on the stream. In polling mode, it means that the last poll
    /// request succeeded.
    /// </remarks>
    Stable,

    /// <summary>
    /// Indicates that the synchronizer encountered an error that it will attempt to recover from.
    /// </summary>
    /// <remarks>
    /// In streaming mode, this means that the stream connection failed, or had to be dropped due to some
    /// other error, and will be retried after a backoff delay. In polling mode, it means that the last poll
    /// request failed, and a new poll request will be made after the configured polling interval.
    /// </remarks>
    Interrupted,

    /// <summary>
    /// Indicates that the synchronizer has been permanently shut down.
    /// </summary>
    /// <remarks>
    /// This could be because it encountered an unrecoverable error (for instance, the Evaluation server
    /// rejected the SDK key: an invalid SDK key will never become valid), or because the SDK client was
    /// explicitly shut down.
    /// </remarks>
    Stopped
}