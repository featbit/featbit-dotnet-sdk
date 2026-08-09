using System;

namespace FeatBit.Sdk.Server;

/// <summary>
/// Provides information about a change to feature data stored by an <see cref="IFbClient"/>.
/// </summary>
public sealed class FeatureDataChangedEventArgs : EventArgs
{
    /// <summary>
    /// Creates event data for a feature data synchronization change.
    /// </summary>
    /// <param name="kind">The synchronization operation that changed the data.</param>
    /// <param name="featureFlagsMayHaveChanged">Whether feature flags may have changed.</param>
    /// <param name="segmentsMayHaveChanged">Whether segments may have changed.</param>
    public FeatureDataChangedEventArgs(
        FeatureDataChangeKind kind,
        bool featureFlagsMayHaveChanged,
        bool segmentsMayHaveChanged)
    {
        Kind = kind;
        FeatureFlagsMayHaveChanged = featureFlagsMayHaveChanged;
        SegmentsMayHaveChanged = segmentsMayHaveChanged;
    }

    /// <summary>
    /// Gets the synchronization operation that changed the data.
    /// </summary>
    public FeatureDataChangeKind Kind { get; }

    /// <summary>
    /// Gets whether feature flags may have changed.
    /// A full synchronization always returns <see langword="true"/> because it replaces the complete local data set.
    /// </summary>
    public bool FeatureFlagsMayHaveChanged { get; }

    /// <summary>
    /// Gets whether segments may have changed.
    /// A full synchronization always returns <see langword="true"/> because it replaces the complete local data set.
    /// </summary>
    public bool SegmentsMayHaveChanged { get; }
}
