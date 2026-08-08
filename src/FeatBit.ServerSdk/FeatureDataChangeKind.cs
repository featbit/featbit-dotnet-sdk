namespace FeatBit.Sdk.Server;

/// <summary>
/// Identifies the synchronization operation that changed locally stored feature data.
/// </summary>
public enum FeatureDataChangeKind
{
    /// <summary>
    /// A complete data set replaced the locally stored feature data.
    /// </summary>
    Full,

    /// <summary>
    /// A patch updated one or more locally stored feature data items.
    /// </summary>
    Patch
}
