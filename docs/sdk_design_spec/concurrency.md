# Concurrency Requirements

[Specification index](../sdk_design_spec.md) | [General requirements](general.md)

The scope, requirement levels, and reference baseline in [General Requirements](general.md) apply to this module.

The requirements in this module include hardening guarantees for new SDKs; see [reference implementation gaps](reference.md#reference-implementation-gaps-and-deliberate-portability-decisions) for differences from the inspected .NET implementation.

These requirements apply to threaded runtimes and to asynchronous interleavings in single-threaded runtimes.

| Shared resource | Required invariant |
| --- | --- |
| Configuration and user context | Immutable snapshots; no mutable aliases from builders. |
| Store | Atomic batch publication and coherent flag/segment reads. |
| Model objects | Immutable after publication; readers do not race collection mutation. |
| Initialization | Exactly one completion; data is visible before readiness. |
| Lifecycle state | Legal transitions serialized; stopped state cannot be reversed by late work. |
| WebSocket sends | One writer/serialized message submission per connection. |
| Receive buffer | Parse while owned, or copy before asynchronous handoff. |
| Reconnect state | At most one owner; cancellation and joining precede disposal. |
| Event ingress | Bounded multi-producer-safe enqueue; record and close may race safely. |
| Dispatch buffer | Single owner, or equivalent explicit synchronization. |
| Worker snapshots | Immutable and owned until worker completion. |
| Flush workers | Atomically bounded; completion counted on every exit path. |
| Listeners | Snapshot subscribers safely; invoke outside locks. |
| Timers and callbacks | No use of partially constructed/disposed state. |
| Sender diagnostics | Request-local timing/state when requests overlap. |

No lock protecting SDK state may be held during network I/O, retry sleep, or user callback execution. Lock ordering MUST be documented where several locks are unavoidable. Close and flush MUST NOT wait for work that requires a lock held by their caller.

A concurrent map alone is insufficient for coherent evaluation across multiple keys. Similarly, replacing a dictionary atomically only makes full replacement safe; it does not make later in-place updates safe. The chosen synchronization strategy MUST be tested under simultaneous evaluation, full replacement, patching, enumeration, event recording, flush, and close.
