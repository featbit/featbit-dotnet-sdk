# Conformance and Acceptance Criteria

[Specification index](../sdk_design_spec.md) | [General requirements](general.md)

The scope, requirement levels, and reference baseline in [General Requirements](general.md) apply to this module.

Each new SDK MUST ship deterministic tests plus integration tests against the supported FeatBit evaluation/insight service. The following matrix is a release gate for the new SDK, not a statement that all scenarios are covered by current .NET tests. [Data-change notifications](notifications.md) are optional and preliminary: all notification-specific assertions, including those in initialization and synchronization scenarios, apply only when the capability is implemented. Omitting this capability does not block release.

| Area | Required scenarios and assertions |
| --- | --- |
| Configuration | Defaults; invalid schemes/ranges; equality of startup/connect timeouts; immutable option snapshots; offline without a secret. |
| Initialization | Empty full response initializes; handshake alone does not; timeout permits later recovery; rejection/close settles waiters; if notifications are implemented, callback sees initialized committed state. |
| Offline/events disabled | Bootstrap evaluation; empty-store fallback; zero outbound traffic offline; synchronization still works with analytics disabled. |
| Protocol | Exact request/response envelopes; token encoding with fixed time/index; URI prefixes and escaping; fragmented UTF-8 messages; no network record separator. |
| Full synchronization | Atomic replacement; empty full clears data; prior higher versions do not survive replacement; if notifications are implemented, both categories invalidate. |
| Patch/versioning | Insert/newer/equal/older versions; archive replay protection; newer restoration; atomic multi-entity batch; no-op patch restores readiness without notification. |
| Cursor recovery | Cursor includes archives; reconnect sends committed maximum; malformed/unaccounted updates force full resync rather than silently advancing. |
| Reconnect | Initial outage; abrupt disconnect; normal server closure; `4003`; repeated real server restarts; recovery only after sync; no duplicate reconnect owner. |
| Reconnect cancellation | Close after retry delay actually begins, during a dial, and concurrently; no later connection attempts or use-after-dispose. |
| Evaluation order | Off before targets; first target; first matching rule; fallthrough; malformed selected variation/rollout never proceeds to later rules. |
| Conditions | Every listed operator; unknown operators; null/missing/empty attributes; Unicode; locale independence; invalid lists; invalid/expensive regexes. |
| Segments | Excluded beats included; OR rules/AND conditions; empty references; positive/negative conditions; missing/archived/malformed dependencies. |
| Rollout | Shared vectors; UTF-8; signed little-endian conversion; endpoints; near-one shortcut; zero-width intervals; custom/missing dispatch attributes. |
| Typed results | Value and ID; every reason kind; exact fallback; wrong type; integer overflow; finite floating-point requirements; JSON returned unchanged as string. |
| Experiments | Off/target/rule/fallthrough eligibility; include-all precedence; zero width; ratio clamping; `expt`-prefixed independent hashing. |
| Bulk evaluation | No analytics; archived entities omitted; one bad flag does not abort healthy results; stable snapshot. |
| Event protocol | Evaluation and metric golden JSON; mixed batches; raw authorization; supported language identity; original timestamps retained on retry. |
| Event semantics | One event per successful typed evaluation, recorded after conversion and containing the selected variation's ID and raw string value; wrong-type/error/not-ready records no evaluation event; caller fallback is never reported as a selected variation; Track before initialization; immutable recorded users. |
| Delivery | `2xx`, `400`, `408`, `429`, other `4xx`, `5xx`, network errors, deadline expiry; exact attempt limit; event rejection leaves evaluation/sync working. |
| Backpressure/flush | Drop new payloads on saturation; bounded total in-flight work; reliable control path; earlier workers included in barrier; timeout does not imply delivery. |
| Notifications (optional; if implemented) | Effective patch only; segment-only changes; subscription races; exception isolation; ordered callbacks; callback-triggered close cannot deadlock. |
| Shutdown | Uninitialized, ready, stale, rejected, and offline clients; full queue; saturated workers; concurrent close; bounded completion; no background resurrection. |
| Concurrency | Simultaneous reads/patches/full replacements/bulk reads; mutation isolation; record/flush/close races; safe disposal under active workers. |

Golden evaluation fixtures SHOULD include input flags, segments, user, requested type, fallback, expected value/variation ID/reason, and expected analytics eligibility. Golden protocol fixtures MUST compare semantic JSON content rather than property order. Hash outputs MUST agree exactly with the documented binary64 calculation.

The SDK release documentation MUST state supported runtime versions, known regex differences, numeric ranges, configuration deviations, extension ownership, and event-delivery guarantees.
