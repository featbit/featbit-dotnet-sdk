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
    /// <param name="hasFeatureFlagChanges">Whether feature flags changed.</param>
    /// <param name="hasSegmentChanges">Whether segments changed.</param>
    public FeatureDataChangedEventArgs(
        FeatureDataChangeKind kind,
        bool hasFeatureFlagChanges,
        bool hasSegmentChanges)
    {
        Kind = kind;
        HasFeatureFlagChanges = hasFeatureFlagChanges;
        HasSegmentChanges = hasSegmentChanges;
    }

    /// <summary>
    /// Gets the synchronization operation that changed the data.
    /// </summary>
    public FeatureDataChangeKind Kind { get; }

    /// <summary>
    /// Gets whether feature flags changed.
    /// </summary>
    public bool HasFeatureFlagChanges { get; }

    /// <summary>
    /// Gets whether segments changed.
    /// </summary>
    public bool HasSegmentChanges { get; }
}
