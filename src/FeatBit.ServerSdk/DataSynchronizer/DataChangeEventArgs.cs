using System;

namespace FeatBit.Sdk.Server.DataSynchronizer;

/// <summary>
/// Provides information about a change to data stored by an <see cref="IFbClient"/>.
/// </summary>
public sealed class DataChangeEventArgs : EventArgs
{
    /// <summary>
    /// Creates event data for a data synchronization change.
    /// </summary>
    /// <param name="kind">The synchronization operation that changed the data.</param>
    /// <param name="featureFlagsChanged">Whether locally stored feature flag data changed.</param>
    /// <param name="segmentsChanged">Whether locally stored segment data changed.</param>
    public DataChangeEventArgs(
        DataChangeKind kind,
        bool featureFlagsChanged,
        bool segmentsChanged)
    {
        Kind = kind;
        FeatureFlagsChanged = featureFlagsChanged;
        SegmentsChanged = segmentsChanged;
    }

    /// <summary>
    /// Gets the synchronization operation that changed the data.
    /// </summary>
    public DataChangeKind Kind { get; }

    /// <summary>
    /// Gets whether locally stored feature flag data changed.
    /// A full synchronization always returns <see langword="true"/> because it replaces the complete local data set.
    /// This does not guarantee that a feature flag evaluation result changed for any particular user.
    /// </summary>
    public bool FeatureFlagsChanged { get; }

    /// <summary>
    /// Gets whether locally stored segment data changed.
    /// A full synchronization always returns <see langword="true"/> because it replaces the complete local data set.
    /// This does not guarantee that a feature flag evaluation result changed for any particular user.
    /// </summary>
    public bool SegmentsChanged { get; }
}
