# Data-Change Notifications (Optional)

[Specification index](../sdk_design_spec.md) | [General requirements](general.md)

The scope, requirement levels, and reference baseline in [General Requirements](general.md) apply to this module.

**Implementation status: optional and preliminary.** The current .NET SDK provides an initial implementation of data-change notifications. Its API and behavior may change as the design evolves. Other SDKs MAY omit this capability for now without affecting conformance to the core SDK specification.

The requirements below, and notification-specific requirements in other modules, apply only when an SDK chooses to implement this capability. They describe the current design and are not a commitment to a stable cross-SDK notification API.

If implemented, expose a notification containing `kind = Full | Patch`, `featureFlagsChanged`, and `segmentsChanged`.

- Full replacement always reports both categories as changed, even when empty or equal-looking, because it invalidates the previous data set.
- A patch reports only categories with effective inserts, updates, archives, or hardening-related quarantine changes. Duplicate/older records produce no notification.
- A segment-only update must be visible to consumers because it can change flag evaluation.

The notification describes changed local data; it does not guarantee that a particular user's evaluated value changed. The current API does not identify individual changed flag keys. Initial full synchronization may occur before an application subscribes, so applications SHOULD subscribe and then perform an explicit initial refresh. Notifications are not replayed by the reference.

**Hardening requirements:** invoke callbacks after commit and readiness publication, outside all store and lifecycle locks. Preserve commit notification order with a serial callback dispatcher or equivalent facility. Isolate exceptions per subscriber so one failure cannot suppress later subscribers. Slow callbacks MUST NOT block the network receive loop; any bounded notification queue MUST define an overflow/coalescing policy that preserves invalidation of all affected categories.

Handlers MUST be able to evaluate flags and request shutdown without a self-wait deadlock. A dispatcher handling a close request MUST NOT join itself. SDK documentation MUST specify callback context, unsubscribe behavior, and treatment of callbacks already running during close.
