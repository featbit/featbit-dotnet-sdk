# Data Model, Storage, and Retrieval

[Specification index](../sdk_design_spec.md) | [General requirements](general.md)

The scope, requirement levels, and reference baseline in [General Requirements](general.md) apply to this module.

## Required evaluation data

| Entity | Required fields and meaning |
| --- | --- |
| Common stored entity | `updatedAt` as an ISO-8601 timestamp, converted to signed 64-bit Unix milliseconds; `isArchived` as a deletion marker. |
| Feature flag | `id`, `key`, `variationType`, `variations`, `targetUsers`, `rules`, `isEnabled`, `disabledVariationId`, `fallthrough`, `exptIncludeAllTargets`. |
| Variation | `id`, `value`; the value is a string on the wire, including boolean, numeric, and JSON values. |
| Individual target | `keyIds` list and `variationId`. |
| Target rule | `name`, optional `dispatchKey`, `includedInExpt`, `conditions`, and rollout `variations`. |
| Fallthrough | Optional `dispatchKey`, `includedInExpt`, and rollout `variations`. |
| Rollout variation | `id`, two-element `rollout` interval, and `exptRollout`. |
| Condition | `property`, `op`, and string `value`. Lists are JSON arrays encoded inside that string. |
| Segment | `id`, `included`, `excluded`, and `rules`; each segment rule contains `conditions`. |

An entity's `updatedAt` is its synchronization version; it is not an evaluation-event timestamp. Preserve millisecond precision and timezone offsets during conversion. Do not replace versions with local receive times.

Unknown JSON fields MUST be tolerated to allow compatible server additions. Preserve targeting and rollout array order. Null or malformed required structures MUST be handled according to [error isolation](error_isolation.md), not silently interpreted as empty targeting rules.

## Store contract

| Operation | Semantics |
| --- | --- |
| Populate | Replace the complete store, independent of previous versions. |
| Get | Return an active entity of the expected type, or absent. |
| Find/enumerate | Return a stable collection of matching active entities. |
| Upsert | Accept only a new key or strictly greater entity version; report whether it changed state. |
| Version | Maximum entity version, including archives; zero when empty. |
| Populated | Whether a full population operation has occurred, even if it contained zero entities. |

The reference key namespaces are `ff_<flag-key>` and `segment_<segment-id>`. Physical key formats may differ, but flags and segments MUST have separate logical namespaces. Flag keys and user keys are case-sensitive. Segment UUIDs SHOULD use canonical lowercase hyphenated strings; parsing and lookup MUST agree on their canonical representation.

Archives MUST be hidden from normal lookup and enumeration while retaining their versions. Otherwise an old patch could resurrect a deleted entity. A later, strictly newer non-archived entity may restore it. A full replacement may discard old tombstones because it replaces the complete snapshot.

`Populated`, `initialized`, and `status` MUST NOT be conflated: a patch can modify an existing store without calling Populate, and an initialized store can remain usable while disconnected.

## Consistency and memory

The store MUST provide atomic publication and safe concurrent reads, writes, and enumeration.

Published model objects and nested collections MUST be immutable to callers and background workers. Do not return a mutable internal dictionary or reuse a received buffer after its ownership ends.

New SDKs MUST NOT expire the last good data merely because the network is unavailable. Persistent storage is optional; if implemented, partition it by environment, atomically persist data plus its cursor, and define cached-data readiness independently from remote freshness.
