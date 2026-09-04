# Error Isolation

[Specification index](../sdk_design_spec.md) | [General requirements](general.md)

The scope, requirement levels, and reference baseline in [General Requirements](general.md) apply to this module.

The requirements in this module include hardening guarantees for new SDKs; see [reference implementation gaps](reference.md#reference-implementation-gaps-and-deliberate-portability-decisions) for differences from the inspected .NET implementation.

## Isolation boundaries

| Failure | Containment and recovery |
| --- | --- |
| Invalid configuration/bootstrap | Return a clear configuration error before background work begins. |
| Connect failure/outage | Keep retrying as appropriate; retain usable cached data. |
| Invalid JSON/envelope or unsupported sync event | Do not commit or mark ready; log and request resynchronization with bounded retry/backoff. |
| Malformed entity in an otherwise parseable update | Isolate that entity and its dependents; allow unrelated valid entities to remain usable. |
| Missing selected variation or broken segment dependency | Return fallback for that flag with an error detail. |
| Numeric user input that cannot be compared | Condition is false. |
| Invalid rule JSON/regex or regex timeout | Contain as malformed data for the affected flag. |
| Unexpected store/evaluator exception | Contain at the public evaluation boundary; return fallback and preserve a diagnostic identifying the internal failure. |
| Event enqueue/serialize/send failure | Isolate analytics; retain the already selected evaluation result. |
| Subscriber exception | Log and continue with other subscribers. |
| Cancellation during close | Complete shutdown; never interpret it as a reason to reconnect. |

The public evaluation boundary MUST prevent recoverable SDK errors from escaping into application request handling. Internally, distinguish malformed data from I/O, cancellation, and programming failures; do not catch every exception and relabel it as a missing flag. Runtime-fatal errors are subject to the host language's fatal-error rules.

## Malformed entity handling and recovery

For parseable envelopes, decode entities independently and retain an explicit invalid/quarantined record when an entity has a usable identity and version but unusable contents. This record preserves version ordering and causes dependent evaluation to fail safely. A strictly newer valid update replaces it normally.

A malformed segment MUST NOT abort unrelated flag evaluations or be treated as a matching/non-matching segment. A malformed flag MUST NOT abort all-variations enumeration. A newer invalid update MUST NOT silently leave an older valid entity presented as the current version; represent the failure explicitly. These requirements do not mandate any particular internal placeholder type.

During event serialization, isolate invalid payload events and allow valid events from the same snapshot to progress. All worker completion signals MUST run even on exceptions, including serializer failures. Background tasks MUST be observed and cleaned up; exception handling MUST NOT create a busy loop on a completed or disposed queue.
