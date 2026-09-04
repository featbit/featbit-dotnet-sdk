# Reference Implementation Notes

[Specification index](../sdk_design_spec.md) | [General requirements](general.md)

## Reference implementation gaps and deliberate portability decisions

These observations apply to the inspected commit. They explain why some new-SDK requirements are stronger than a direct source translation; they are not a request to modify the .NET implementation as part of this document.

| Reference behavior observed in source | Requirement for new SDKs |
| --- | --- |
| `DefaultMemoryStore` locks writes but performs unsynchronized reads/enumeration of an in-place mutable dictionary. | Safe snapshots or locking for all readers; atomic batch updates and consistent dependency reads. |
| Synchronizer invokes `DataChanged` before setting `Stable` and initialization. | Commit, publish readiness, then notify. |
| Public data callbacks run inline with the receive path. | Isolate callback dispatch; prevent synchronous close/self-wait deadlocks. The existing async callback shutdown test does not prove synchronous-wait safety. |
| Evaluation handles some malformed paths explicitly, but missing segments, missing target variations, invalid regex/list data, and other exceptions can escape. | Complete per-flag fallback boundary and isolation of bulk evaluation. |
| A sync-message catch protects the receive path, but entity parsing/patching can fail at message scope or after partial mutation. | Explicit malformed-entity isolation, atomic commits, and cursor recovery. |
| Unknown sync event types can fall through to readiness publication. | Only supported valid sync operations establish readiness. |
| Number parsing and some string operations use ambient culture; rollout integer decoding uses native byte order. | Defined numeric grammar, ordinal comparisons, and explicit little-endian hash decoding. |
| Options remain mutable and the user builder shares its custom dictionary. | Immutable owned snapshots. |
| Initializer promise completes only on success. | Also complete waiting callers on rejection and shutdown. |
| Event dispatcher/timer startup can precede full field initialization. | Construct dependencies before scheduling work. |
| Flush completion can mean skipped/rejected work; shutdown controls share the bounded payload queue. | Reliable control signaling and an honest processing barrier. |
| A terminal event-send result marks the dispatcher stopped but does not immediately exit the current worker's remaining slices. | Stop scheduling further sends after terminal rejection. |
| Some shutdown/background-task disposal paths do not enforce all ownership and joining guarantees described above. | Idempotent close, tracked workers, bounded cancellation, safe resource disposal. |
| Trace event logging includes payloads and transport logging can include token-bearing URLs. | Credential redaction and explicitly opt-in payload diagnostics. |

For valid inputs, selection order, wire fields, hash assignments, interval boundaries, and experiment eligibility remain compatibility requirements. Any future change to these contracts MUST be reviewed as an explicit cross-SDK behavior change and accompanied by updated shared fixtures.

## Source and test map

Paths below are relative to this repository. They are traceability references for implementers, not a requirement to reproduce the .NET class structure.

| Topic | Primary source | Existing test/fixture reference |
| --- | --- | --- |
| Client API/lifecycle | `src/FeatBit.ServerSdk/FbClient.cs`, `IFbClient.cs`, `FbClientStatus.cs` | `tests/FeatBit.ServerSdk.Tests/FbClientTests.cs`, `FbClientOfflineTests.cs` |
| Configuration | `src/FeatBit.ServerSdk/Options/FbOptions.cs`, `FbOptionsBuilder.cs` | `tests/FeatBit.ServerSdk.Tests/Options/FbOptionsBuilderTests.cs` |
| Offline bootstrap | `src/FeatBit.ServerSdk/Bootstrapping/`, `DataSynchronizer/NullDataSynchronizer.cs` | `tests/FeatBit.ServerSdk.Tests/Bootstrapping/` |
| Sync/notification semantics | `src/FeatBit.ServerSdk/DataSynchronizer/WebSocketDataSynchronizer.cs`, `DataSet.cs`, `DataChangeEventArgs.cs` | `tests/FeatBit.ServerSdk.Tests/DataSynchronizer/WebSocketDataSynchronizerTests.cs`, `full-data-set.json`, `patch-data-set.json` |
| Transport and reconnect | `src/FeatBit.ServerSdk/Transport/FbWebSocket.cs`, `WebSocketTransport.cs`, `ConnectionToken.cs`, `TextMessageParser.cs`, `TextMessageFormatter.cs`, `Retry/DefaultRetryPolicy.cs` | `tests/FeatBit.ServerSdk.Tests/Transport/`, `Retry/`, `UriTests.cs` |
| Store and versions | `src/FeatBit.ServerSdk/Store/`, `Json/VersionJsonConverter.cs` | `tests/FeatBit.ServerSdk.Tests/Store/DefaultMemoryStoreTests.cs` |
| Models and user context | `src/FeatBit.ServerSdk/Model/` | `tests/FeatBit.ServerSdk.Tests/Model/one-flag.json`, `one-segment.json`, `DeserializationTests.cs` |
| Evaluation and rollout | `src/FeatBit.ServerSdk/Evaluation/`, `ValueConverters.cs` | `tests/FeatBit.ServerSdk.Tests/Evaluation/`, `ValueConverterTests.cs` |
| Event serialization | `src/FeatBit.ServerSdk/Events/IEvent.cs`, `DefaultEventSerializer.cs` | `tests/FeatBit.ServerSdk.Tests/Events/DefaultEventSerializerTests.cs` and its `.verified.txt` snapshots |
| Event processing/delivery | `src/FeatBit.ServerSdk/Events/DefaultEventProcessor.cs`, `DefaultEventDispatcher.cs`, `DefaultEventSender.cs`, `Http/HttpErrors.cs` | `tests/FeatBit.ServerSdk.Tests/Events/` |
| Concurrency and hosting | `src/FeatBit.ServerSdk/Concurrent/`, `DependencyInjection/` | `tests/FeatBit.ServerSdk.Tests/Concurrent/`, client/transport/event tests |
