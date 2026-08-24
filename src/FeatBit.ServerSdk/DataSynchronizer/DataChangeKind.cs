namespace FeatBit.Sdk.Server.DataSynchronizer;

/// <summary>
/// Identifies the synchronization operation that changed locally stored data.
/// </summary>
public enum DataChangeKind
{
    /// <summary>
    /// A complete data set replaced the locally stored data.
    /// </summary>
    Full,

    /// <summary>
    /// A patch updated one or more locally stored data items.
    /// </summary>
    Patch
}
