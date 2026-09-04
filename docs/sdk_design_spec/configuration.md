# Configuration and Initialization

[Specification index](../sdk_design_spec.md) | [General requirements](general.md)

The scope, requirement levels, and reference baseline in [General Requirements](general.md) apply to this module.

## Configuration surface and reference defaults

| Setting | Reference default | Purpose |
| --- | --- | --- |
| Environment secret | Supplied by caller; empty builder value | Authenticate access to one environment. |
| Streaming base URI | `ws://localhost:5100` | WebSocket endpoint base. |
| Event base URI | `http://localhost:5100` | Insight HTTP endpoint base. |
| Offline | `false` | Disable all remote synchronization and analytics. |
| Disable events | `false` | Disable analytics while retaining online synchronization. |
| Bootstrap provider | Empty data set | Offline local data source. |
| Startup wait | 5 seconds | Maximum initial caller wait. |
| WebSocket connect timeout | 3 seconds | Deadline for each connection attempt. |
| WebSocket close timeout | 2 seconds | Graceful transport shutdown allowance. |
| Application keepalive interval | 15 seconds | Send the application-level ping message. |
| Reconnect delays | `[0, 1, 2, 3, 5, 8, 13, 21, 34, 55]` seconds | Repeated reconnect schedule. |
| Automatic flush interval | 5 seconds | Periodic event flush trigger. |
| Shutdown flush timeout | 5 seconds | Bound final analytics processing. |
| Maximum events in queue | 10,000 | Capacity of the ingress queue; also the reference dispatch buffer capacity. |
| Maximum events per request | 50 | Maximum payload event objects per HTTP request. |
| Maximum concurrent flush workers | `min(max(floor(cpu_count / 2), 1), 4)` | Bound parallel batch-send jobs. |
| Maximum send attempts | 2 total attempts | Includes the first request. |
| Send retry interval | 200 milliseconds | Delay between event send attempts. |
| Logger | No-op logger | Application-controlled diagnostics. |

The reference HTTP sender additionally uses a fixed one-second connection timeout where the runtime supports it and a two-second cancellation deadline per attempt. It does not expose these as builder options; the two-second deadline is not a separately enforced one-second response-read timeout.

SDKs SHOULD retain these defaults. Runtime-specific deviations, such as a single asynchronous flush worker, MUST be documented. Credentials and endpoint URLs MUST remain configurable.

## Validation and ownership (Optional)

Configuration MUST be validated before timers or workers start:

- Online mode requires a non-empty environment secret and valid absolute streaming/event URIs with appropriate schemes. Offline mode permits no secret.
- Timeouts and timer intervals MUST be positive. Retry delays MAY be zero but MUST NOT be negative. An absent or empty reconnect schedule selects the default, matching the reference transport.
- A blocking startup configuration MUST satisfy `startup_wait >= connect_timeout`. The reference comparisons permit equality.
- Queue capacity, batch size, worker count, and total send attempts MUST be positive integers. Batch size SHOULD NOT exceed buffer capacity.
- The bootstrap input and any optional extension objects MUST be validated before use.

Configuration errors MAY use idiomatic exceptions or error results. Routine connection failures MUST NOT escape as constructor/startup exceptions that crash the application. A malformed bootstrap document is a configuration/input error, not a network failure.

The client MUST own an immutable configuration snapshot, including copies of retry arrays and other mutable collections. Editing a builder or original configuration after startup MUST NOT mutate a running client.

## Online startup

1. Validate and snapshot configuration.
2. Construct the store, evaluator, and optional event pipeline completely before starting their workers.
3. Initialize synchronization state to `Starting` and `initialized = false`.
4. Start the WebSocket connection asynchronously.
5. After each successful connection, request synchronization using the store cursor.
6. Commit a valid full/patch response before publishing readiness.
7. Complete initialization and return to a waiting caller, or return when the startup wait expires.

A successful WebSocket handshake alone MUST NOT imply readiness. An empty but valid data set is sufficient to initialize. A startup wait timeout MUST NOT cancel background recovery. Until initialization succeeds, typed evaluation MUST return the caller's fallback with `ClientNotReady` and MUST NOT record an evaluation event.

**Hardening requirement:** initialization completion MUST settle exactly once on success, terminal rejection, or explicit close. Caller timeout is separate from that completion. The inspected synchronizer only completes its initialization promise on success; new SDKs MUST also wake waiters on terminal outcomes.

## Offline mode and bootstrap

Offline mode MUST create no streaming connection, analytics HTTP request, reconnect timer, or analytics worker. It MUST populate the store from the bootstrap data or an empty data set, then report initialized and ready. An unknown flag in an initialized offline client returns `Error / flag not found`, not `ClientNotReady`.

The bootstrap format is the same JSON envelope used for synchronization, with data under `data`. The reference JSON provider is available only in offline mode and populates a complete store. A new SDK SHOULD require a `full` bootstrap data set. Supporting bootstrap as an online startup cache is an optional extension and MUST explicitly define when that cache becomes eligible for evaluation.

Disabling events alone MUST preserve normal online initialization and synchronization.
