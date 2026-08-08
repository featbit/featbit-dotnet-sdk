using System;

namespace FeatBit.Sdk.Server;

/// <summary>
/// Provides notifications after FeatBit feature data has changed locally.
/// </summary>
public interface IFbClientDataChangeNotifier
{
    /// <summary>
    /// Occurs after a full synchronization replaces feature data or a patch changes stored feature data.
    /// </summary>
    /// <remarks>
    /// The event does not expose feature flag keys, values, segments, or evaluation results. Event
    /// handlers should finish quickly and must not rely on the event for every connection or status change.
    /// Notifications that occurred before a handler was added are not replayed. A subscriber that needs
    /// initial state should add its handler before explicitly refreshing that state.
    /// </remarks>
    event EventHandler<FeatureDataChangedEventArgs> DataChanged;
}
