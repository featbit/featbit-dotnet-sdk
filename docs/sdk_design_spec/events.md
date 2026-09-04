# Event Collection and Delivery

[Specification index](../sdk_design_spec.md) | [General requirements](general.md)

The scope, requirement levels, and reference baseline in [General Requirements](general.md) apply to this module.

## Collection rules

For a typed evaluation, an off, targeted, rule, or fallthrough result records one evaluation event only after the selected variation has been successfully converted to the requested type. The event contains the selected variation's ID and raw string value. SDKs MUST perform typed conversion before recording the event. `WrongType, ClientNotReady`, flag-not-found, and malformed-data fallback MUST NOT record evaluation events either; the caller's fallback MUST NOT be reported as a selected variation.

All-variations calls produce no evaluation events. Custom Track calls are independent of initialization and may be recorded while the client is not ready. Offline mode, disabled events, and a closed client suppress analytics. The last condition is an explicit hardening guarantee for new SDKs.

Capture the event timestamp at record time as Unix milliseconds. Custom numeric metrics default to `1.0`.

## Wire payloads

Send a JSON array of payload event objects via:

```text
POST <event-base>/api/public/insight/track
Authorization: <environment-secret>
Content-Type: application/json; charset=utf-8
User-Agent: <language-specific-server-sdk-identifier>
```

The authorization value is the raw environment secret, without an added `Bearer` prefix. Endpoint path resolution follows [endpoint construction and authentication](synchronization.md#endpoint-construction-and-authentication).

An evaluation event has this structure:

```json
[
  {
    "user": {
      "keyId": "user-123",
      "name": "Example User",
      "customizedProperties": [{"name": "country", "value": "us"}]
    },
    "variations": [
      {
        "featureFlagKey": "checkout-v2",
        "variation": {"id": "variation-on", "value": "true"},
        "timestamp": 1700000000000,
        "sendToExperiment": true
      }
    ]
  }
]
```

A custom metric event has this structure; `dotnet-server-side` is the reference SDK identifier:

```json
[
  {
    "user": {"keyId": "user-123", "name": "", "customizedProperties": []},
    "metrics": [
      {
        "appType": "dotnet-server-side",
        "route": "index/metric",
        "type": "CustomEvent",
        "eventName": "checkout-completed",
        "numericValue": 1.0,
        "timestamp": 1700000000000
      }
    ]
  }
]
```

Other languages MUST select the corresponding server-supported SDK `appType` and document it; Preserve `route`, `type`, and the other field names. An integration test MUST verify the chosen identifier with the target service.

User custom attributes are an array of `{name, value}` objects, not a dictionary. Variation values remain strings. Evaluation and metric objects may be mixed in one request. The reference sends individual payload objects without deduplication or aggregation; ports MUST NOT silently sample, merge, or deduplicate recorded events.

## Queue, buffering, and batching

Use a bounded, non-blocking ingress queue. When capacity is exhausted, drop the new payload event, return/record its rejected status where the API allows, and emit a rate-limited diagnostic. Never block application evaluation waiting for network capacity.

A single dispatcher SHOULD own its mutable buffer. On a flush trigger, it takes an immutable snapshot and releases the buffer for subsequent records. A worker splits that snapshot into requests containing at most the configured number of payload objects, sending those requests sequentially. Multiple workers may overlap; server delivery order is not guaranteed.

In the reference, the ingress queue and buffer each have a 10,000-event capacity, and worker snapshots hold additional events. Thus `MaxEventsInQueue` is not a total process-memory limit. New SDKs MUST bound the ingress queue, buffer, number of snapshots in flight, and message/payload bytes; consider their combined worst-case memory budget.

## Send outcomes and retry

| HTTP/transport outcome | Reference classification | Required handling |
| --- | --- | --- |
| Any `2xx` response | Succeeded | Finish the batch. |
| `400`, `408`, `429` | Recoverable | Retry within the attempt budget. |
| Other `4xx`, including `401`, `403`, `404` | Terminal for event delivery | Disable subsequent event delivery for the client. |
| `5xx` and other unsuccessful non-`4xx` responses | Recoverable | Retry within the attempt budget. |
| Transient network exception | Recoverable | Retry within the attempt budget. |
| Explicit shutdown cancellation | Canceled | Stop promptly; do not retry. |

Do not assume every `4xx` is terminal; `400` is intentionally recoverable in the reference. Event-delivery rejection MUST NOT shut down the synchronizer or evaluator.

The reference treats cancellation attributed to its own per-attempt deadline as failed without retry; other request-timeout exceptions are recoverable.

Retries reuse the original event payload and timestamps. Delivery is best effort: events may be dropped on overflow, failed attempts, shutdown timeout, or process exit, and retries may cause duplicates if the server accepted a request whose response was lost. The SDK MUST NOT promise durable, at-least-once, or exactly-once delivery.

**Hardening requirement:** once event delivery becomes terminal, do not schedule further HTTP requests, including later slices of a worker snapshot. Already in-flight requests may finish. Workers and control operations must still complete; the event subsystem must not spin or block indefinitely.
