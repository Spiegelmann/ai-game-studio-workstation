# POLICY-006 Implementation — Policy Evaluation Determinism & Replay Verification

## Audit findings

The policy evaluator uses only immutable action and context values. It performs no I/O and does not consult time, randomness, environment state, dictionaries, sets, or external services. Capability registrations and rule inputs are copied during context construction; registration evaluations retain that canonical order, and the trace copies and orders rule evaluations by their explicit order.

Registration ordering was already total for the registration model: capability value (ordinal), provenance source, trust, scope kind, and scope boundary identifier (ordinal), followed by duplicate elimination. Candidate selection in the evaluator uses this order. In particular, when more than one non-trusted potential match is present, the first candidate in canonical order selects the structured provenance reason. Scope matching remains tri-state, and candidate selection does not change the existing deny/escalate/allow semantics.

Rule ordering already used action category (case-insensitive, then ordinal), decision outcome, reason code (ordinal), and reason explanation (ordinal). The audit found that rules tied on these keys but carrying different action scopes retained input order. Since action scope is observable in each rule trace, the resulting traces could differ when equivalent rule collections were supplied in another order. The canonical sort now uses action scope kind and boundary identifier (ordinal) as tie-breakers after the existing keys. The evaluator still applies rules by action category only; scope ordering does not change applicability or policy meaning.

Rules tied on every documented sort key and scope key retain their original relative order, as provided by LINQ's stable ordering. Exact duplicate rule values are observationally interchangeable. An embedded prior evaluation trace in otherwise identical rule decision fields is not a policy tie-break key; if callers use such decisions as rules, their supplied relative order is retained. This preserves the existing stable-order behavior rather than comparing or reinterpreting nested historical traces.

No nondeterminism was found in scope comparisons, provenance comparisons, capability matching, precedence handling, or trace equality. Scope and provenance are immutable value records; evaluation traces defensively copy collections, expose read-only wrappers, and compare nested collections structurally. `PolicyDecision` remains compatible with direct construction without a trace. Evaluator-produced traces record the same final outcome and reason as their returned decisions; equal-precedence rules do not replace the earlier decision.

## Determinism contract

* Repeated evaluation of equivalent action and context values produces value-equal decisions and structurally equal traces.
* Capability registrations are independent of input collection order and are ordered by capability, provenance source, trust, scope kind, then scope boundary identifier; exact duplicate registrations are removed.
* Rules are ordered by action category case-insensitively, then ordinally, decision outcome, reason code, reason explanation, action scope kind, and action scope boundary identifier. The initial ordering keys and rule applicability are unchanged.
* Stable input order is retained only when rules tie on the documented sort keys, including scope. This is meaningful for otherwise indistinguishable policy fields; embedded prior traces do not participate in ordering.
* Candidate evaluation and trace collection follow those canonical context orders. Rule trace order is contiguous and is the order used by precedence evaluation.
* For a fixed rule order, only a strictly more restrictive decision overrides the current one. `Deny > Escalate > Allow`; ties keep the earlier reason/source. Candidate registration ordering similarly resolves which non-trusted potential match supplies the aggregate provenance reason.
* Unknown scope remains `Unknown`, never an implicit match or global scope. Existing trusted-match, all-known-mismatch, unknown/untrusted-provenance, deny, escalation, and allow results are preserved.
* Trace equality and hash codes account for the same structural fields and ordered nested collections. Trace data remains immutable through its exposed interfaces and does not retain mutable input collections.
* Decisions created outside `PolicyEvaluator` remain trace-free and value-equal based on their decision fields.

## Tests

Focused Core tests compare repeated evaluations and reversed registration/rule input collections, assert canonical candidate and rule ordering, verify winning-source/precedence consistency and structural equality/hash behavior, and preserve direct-decision trace compatibility. Existing policy evaluator and trace tests continue to cover unknown scope and deny/escalate/allow behavior.

## Architectural boundaries

The work remains entirely within Core policy-domain context ordering and Core tests. No authorization enforcement, execution, orchestration, scheduling, persistence, logging, telemetry, external services, serialization frameworks, or dependencies were introduced. An evaluation trace is explanatory policy data; deterministic output does not make it an authorization token, tamper-proof audit record, or proof of trustworthy inputs.

## Limitations and deferred work

No cryptographic integrity, signed evidence, durable audit storage, external replay format, or serialization compatibility contract is provided. Determinism applies to the same domain values and documented ordering contract; it does not establish authenticity, authorization, or resource enforcement. Embedded prior traces are preserved as input data and are not used to break rule-order ties.
