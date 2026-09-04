# Shutdown and Resource Ownership

[Specification index](../sdk_design_spec.md) | [General requirements](general.md)

The scope, requirement levels, and reference baseline in [General Requirements](general.md) apply to this module.

The requirements in this module include hardening guarantees for new SDKs; see [reference implementation gaps](reference.md#reference-implementation-gaps-and-deliberate-portability-decisions) for differences from the inspected .NET implementation.

Close MUST be idempotent and safe when called before initialization, during retry delay, during an active connection attempt, after terminal rejection, and concurrently from multiple callers.

The required shutdown sequence is:

1. Atomically enter closing state and prevent new connections, timers, analytics acceptance, and callback scheduling.
2. Cancel pending retry waits and connection attempts; stop keepalive and automatic-flush timers.
3. Wake blocked receive/send/queue operations and close or abort the transport within its timeout.
4. Join tracked receive, send, and reconnect work before clearing transport references.
5. Flush previously accepted analytics and wait for their processing barrier within the remaining event-flush budget.
6. Cancel or abandon remaining delivery work according to the documented deadline, safely join/clean up workers, and dispose owned resources.
7. Publish `Closed`, settle pending initialization/flush waiters, and finish close for all callers.

Do not dispose synchronization primitives while a worker can still signal them. A payload-full queue MUST NOT prevent shutdown signaling. Disposal MUST distinguish SDK-owned resources from caller-supplied transports, HTTP clients, and loggers; caller-owned resources must not be disposed unless ownership transfer is explicit.

After close completes, no reconnect attempt, new HTTP send, or new callback invocation may start. Already-running callbacks require an explicit policy; a callback requesting close must not wait for itself. Local read-only evaluation remains available under [synchronization states](synchronization.md#states), with analytics suppressed.
