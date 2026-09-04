# User Context and Feature Flag Evaluation

[Specification index](../sdk_design_spec.md) | [General requirements](general.md)

The scope, requirement levels, and reference baseline in [General Requirements](general.md) apply to this module.

## User context

A user consists of a required stable string key, an optional name defaulting to `""`, and custom string attributes. Attribute lookup follows the reference:

| Property | Value |
| --- | --- |
| `keyId` | User key. |
| `name` | User name. |
| Existing custom property | Its string value. |
| Missing custom property | Not found. |
| Empty or whitespace-only property name | Invalid; treat as not found. |

Attribute lookup MUST preserve both presence and value so that a missing attribute is distinguishable from an existing attribute whose value is `""`. Reserved properties take precedence over custom attributes, preventing custom fields from overriding user identity.

## Decision order

For each typed evaluation, use this exact precedence:

1. If the client is not initialized, return the caller's fallback with `ClientNotReady`.
2. Look up the active flag. If absent or archived, return fallback with `Error / flag not found`.
3. If the flag is disabled, return its configured disabled variation with `Off`. This is a normal result, not the caller's fallback.
4. Search individual targets in stored order. The first target containing the user key wins, with `TargetMatch`.
5. Search rules in stored order. The first rule whose conditions all match wins. Select its first matching rollout variation and return `RuleMatch`.
6. If no target or rule matches, select the first matching fallthrough rollout variation and return `Fallthrough`.
7. Resolve the selected variation ID, produce evaluation analytics eligibility, and convert the selected string value to the requested type.

A matched rule with no matching rollout MUST produce `Error / malformed flag`; it MUST NOT silently proceed to later rules or fallthrough. A missing selected variation is also malformed data. The hardening boundary in [error isolation](error_isolation.md) covers every selection path, including individual targets.

The declared `variationType` is metadata in this reference: typed APIs attempt conversion of the selected string rather than rejecting solely on that declaration. Ports MUST preserve that behavior.

## Result details and conversion

| Reason kind | Meaning | Reference reason text |
| --- | --- | --- |
| `ClientNotReady` | No initialized data state. | `client not ready` |
| `Off` | Configured disabled variation. | `flag off` |
| `TargetMatch` | Individual target matched. | `target match` |
| `RuleMatch` | Target rule matched. | `match rule <rule-name>` |
| `Fallthrough` | Default rollout selected. | `fall through targets and rules` |
| `WrongType` | Selected value cannot be converted. | `type mismatch` |
| `Error` | Missing flag, malformed data, or another contained evaluation failure. | `flag not found`, `malformed flag`, or a diagnostic reason. |

Every detail MUST include flag key, value, variation ID, kind, and reason. Fallback results MUST contain the caller's exact fallback and an empty variation ID. Successful results MUST contain the selected variation's ID. Reason categories are stable API contracts; consumers SHOULD NOT parse human-readable reason text.

Strings are returned unchanged. Booleans accept case-insensitive `true` and `false`; do not use generic truthiness. Integers MUST reject fractional values and overflow. Floating-point APIs MUST preserve their documented precision.

## Rules and condition operators

Conditions within one rule are ANDed and short-circuit on failure. Rules are ordered alternatives. An empty condition list matches vacuously; malformed or null condition lists MUST NOT be normalized into an empty matching rule.

Before applying an operator, look up the condition property and check whether it exists. A missing attribute, or an empty or whitespace-only property name, MUST return false for every ordinary condition, including negative operators such as `NotEqual`, `NotContain`, `NotMatchRegex`, and `NotOneOf`. Do not implement negative operators by negating the result of an entire positive condition, because that would turn a missing attribute into a match. If the attribute exists and its value is `""`, apply the selected operator normally using that empty string.

| Wire operator | Semantics |
| --- | --- |
| `LessThan`, `LessEqualThan` | Numeric `<`, `<=`. |
| `BiggerThan`, `BiggerEqualThan` | Numeric `>`, `>=`. Preserve these exact wire names. |
| `Equal`, `NotEqual` | Case-sensitive string equality/inequality, not numeric equality. |
| `Contains`, `NotContain` | Case-sensitive substring match or its negation. |
| `StartsWith`, `EndsWith` | Case-sensitive prefix/suffix match. |
| `MatchRegex`, `NotMatchRegex` | Regex search match or its negation; not implicitly a full-string match. |
| `IsOneOf`, `NotOneOf` | Exact string membership/non-membership in a JSON-encoded string array. |
| `IsTrue`, `IsFalse` | Case-insensitive comparison to `true` or `false`; condition value is otherwise unused. |
| Unknown operator | False, including an unknown name that sounds like a negated operator. |

The reference returns false when an attribute is missing or either operand is null, even for boolean and negative operators. Numeric parse failures and NaN comparisons return false. Empty strings remain actual operands when the attribute exists. `[]` is a valid membership list: `IsOneOf` is false and `NotOneOf` is true for an existing, non-null user operand.

**Hardening requirements:** use ordinal, locale-independent string operations and the finite numeric parsing rules above. An invalid list JSON value, invalid regex, or regex timeout MUST become a contained malformed-data outcome for the affected flag, rather than being inverted into a successful negative condition. Regex execution MUST have a bounded runtime or use an engine with bounded evaluation characteristics. SDKs MUST document their supported regex dialect and test common patterns across languages; unsupported patterns MUST NOT be silently reinterpreted.

## Segment matching

The following special condition properties bypass ordinary operator dispatch:

- `User is in segment`: true if any referenced segment matches.
- `User is not in segment`: true if no referenced segment matches.

For these properties, `value` is a JSON-encoded array of segment ID strings and `op` is ignored. A valid empty ID array matches no segments, so its positive form is false and its negative form is true.

Within one segment, evaluate in this order:

1. If the user is explicitly excluded, return false.
2. Otherwise, if explicitly included, return true.
3. Otherwise, return true if any segment rule matches; all conditions within that rule must match.
4. Otherwise return false.

Exclusion therefore wins when the user appears in both lists. Empty segment rules obey the same AND/OR semantics as ordinary rules. The reference evaluates segment rules with ordinary condition operators; recursive segment references are not allowed in FeatBit.

## Deterministic percentage rollout

Cross-language rollout compatibility is mandatory. For a rule or fallthrough:

```text
attribute = user.key                         if dispatchKey is null/empty/whitespace
            user.valueOf(dispatchKey) ?? ""        otherwise
dispatchInput = flag.key + attribute        // no delimiter, salt, or normalization
digest = MD5(UTF8(dispatchInput))
n = signed_int32_little_endian(digest[0:4])
bucket = abs(float64(n) / -2147483648.0)
```

The bucket lies in `[0, 1]`, inclusive. Convert to floating point before taking the absolute value to avoid signed-integer overflow. Explicit little-endian decoding preserves the behavior of the reference on its usual little-endian platforms; native-endian APIs MUST NOT determine the result in a port. Do not use a language's built-in hash, unsigned decoding, a hash of hexadecimal text, or modulo 100.

For a valid interval `[lower, upper]`, match as follows:

```text
if lower == 0 and 1 - upper < 0.00001: true
else if lower == 0 and upper == 0: false
else: lower <= bucket and bucket <= upper
```

Both boundaries are inclusive. Adjacent intervals can both match their shared boundary; the first variation in stored order wins. Preserve the near-one shortcut exactly. Missing custom dispatch attributes produce `flag.key + ""`; do not silently substitute the user key. Distribution percentages are not recomputed or normalized by the SDK.

The following is the original C# implementation from [DispatchAlgorithm.cs](../../src/FeatBit.ServerSdk/Evaluation/DispatchAlgorithm.cs). The `key` argument is the combined `dispatchInput` described above. `BitConverter.ToInt32` uses native byte order in C#; ports MUST use the explicit little-endian decoding specified above.

```csharp
using System;
using System.Security.Cryptography;
using System.Text;

namespace FeatBit.Sdk.Server.Evaluation
{
    internal static class DispatchAlgorithm
    {
        public static bool IsInRollout(string key, double[] rollouts)
        {
            var min = rollouts[0];
            var max = rollouts[1];

            // if [0, 1]
            if (min == 0d && 1d - max < 1e-5)
            {
                return true;
            }

            // if [0, 0]
            if (min == 0d && max == 0d)
            {
                return false;
            }

            var rollout = RolloutOfKey(key);
            return rollout >= min && rollout <= max;
        }

        public static double RolloutOfKey(string key)
        {
            using (var hasher = MD5.Create())
            {
                var hashedKey = hasher.ComputeHash(Encoding.UTF8.GetBytes(key));
                var magicNumber = BitConverter.ToInt32(hashedKey, 0);
                var percentage = Math.Abs((double)magicNumber / int.MinValue);

                return percentage;
            }
        }
    }
}
```

These inputs are already-combined hash keys, not separate flag/user arguments:

| Hash input | Expected bucket |
| --- | --- |
| `test-value` | `0.14653629204258323` |
| `qKPKh1S3FolC` | `0.9105919692665339` |
| `3eacb184-2d79-49df-9ea7-edd4f10e4c6f` | `0.08994403155520558` |

These vectors come from `DispatchAlgorithmTests`. Conformance tests MUST additionally cover UTF-8 non-ASCII input, negative signed hashes, both interval endpoints, and the near-one shortcut.

## Experiment eligibility

An evaluation event's `sendToExperiment` is calculated independently from its selected flag value:

- Disabled flag: always false.
- Individual target: equal to `flag.exptIncludeAllTargets`.
- Rule/fallthrough: true immediately when `exptIncludeAllTargets` is true. Otherwise false if that rule/fallthrough is not included in experiments.
- For an included rule/fallthrough, let `width = rollout[1] - rollout[0]`. If `width == 0` or `exptRollout == 0`, return false. Otherwise compute `upper = min(exptRollout / width, 1)` and test interval `[0, upper]` using the same rollout algorithm with hash input `"expt" + dispatchInput`.

Do not reuse the flag bucket for experiment sampling: the `expt` prefix produces a different assignment. `exptRollout` is divided by the selected variation's dispatch width; it is not used directly as a percentage threshold. Its value MUST be finite and non-negative; values larger than the width are clamped by the ratio calculation.

## All-variations API

Return one raw-string evaluation detail per active flag and record no analytics events. Ordering is unspecified. The reference evaluates whatever is in the store without the typed API's initialization check; its initially empty store naturally returns an empty array.
