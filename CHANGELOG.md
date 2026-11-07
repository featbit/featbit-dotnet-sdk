# Changelog

## 2.1.0 (Unreleased)

### Features

- Respect the per-flag `insightsEnabled` setting when recording evaluation events. The setting defaults to `true` when omitted and does not affect evaluation results or custom events sent through `Track`. (#40)

### Fixes

- Treat missing user attributes as non-matching for targeting conditions, including negative operators. (#37)
- Return fallback values for invalid evaluation inputs and malformed flag data instead of propagating evaluation errors to application code. Isolate malformed flags when calling `GetAllVariations`. (#38)
- Avoid recording evaluation events when the returned variation cannot be converted to the requested type.

### Event Payload Changes

- Remove SDK-side experiment eligibility calculations and the `sendToExperiment` field from evaluation events. Flag targeting and rollout behavior remain unchanged. (#41)

**Full Changelog**: https://github.com/featbit/dotnet-server-sdk/compare/v2.0.1...v2.1.0
