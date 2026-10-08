# POLICY-005 Implementation — Policy Decision Explainability & Evaluation Trace

## Objective

Add deterministic, structured policy evaluation facts to evaluator-produced decisions. The trace is generated during the existing evaluation pass and does not independently recalculate the policy result.

## Evaluation trace

Every decision returned by `PolicyEvaluator` includes a `PolicyEvaluationTrace`. Decisions constructed directly for policy rules remain compatible and have no trace until evaluated.

The trace contains:

* the evaluated action and input context status;
* required capability, availability, aggregate capability outcome, and structured reason;
* each matching capability registration, its provenance and trust, tri-state scope relationship, candidate outcome, reason where applicable, and whether it contributes to the aggregate capability result;
* every context rule in deterministic order, whether it applies, its configured outcome, the decision before and after it, and whether it overrides the prior result;
* final outcome, reason, precedence, decision source, and winning rule order when a rule overrides the capability result.

`PolicyCapabilityAvailability.NotEvaluated` distinguishes an unsupported action or context that prevents capability evaluation from a mapped capability that is `Missing`. A missing capability retains the existing `capability.missing` escalation.

## Deterministic ordering and precedence

Registration evaluations follow the context's canonical order: capability, provenance source, trust, scope kind, scope boundary identifier, then duplicate elimination. Rule evaluations follow `PolicyContext.Rules` order, which is canonicalized by action category, outcome, reason code, and reason explanation. Each rule's index in that ordered list is retained as its trace order.

`PolicyDecisionPrecedence` explicitly orders `Allow < Escalate < Deny`, preserving the evaluator's existing `Deny > Escalate > Allow` combination semantics. An applicable rule is identified as the winning source only when it is strictly more restrictive than the current result; equal outcomes do not replace the current reason, matching prior behavior.

## Provenance and scope

Each registration candidate retains its original immutable registration, including source and trust. Its scope relationship is represented as `Match`, `NoMatch`, or `Unknown`. Trusted matches can contribute to `Allow`; all-known mismatches contribute to the existing scope `Deny`; an unknown/untrusted potential match or an unknown scope relationship contributes to the existing `Escalate` result. Inapplicable registrations remain visible without introducing new final reasons or changing POLICY-003/POLICY-004 behavior.

## Immutability

Trace and nested evaluation collections are defensively copied and exposed as read-only lists. All nested domain facts are immutable values/records. Trace equality compares its nested collections structurally so equivalent evaluations produce equal decisions and traces.

## Architectural boundaries

This is **domain-level explainability**, not:

* audit persistence or audit integration;
* security logging or operational logging;
* authorization, identity, or security enforcement;
* telemetry, event publishing, or external logging;
* execution, orchestration, approvals, or recovery.

Core remains provider-independent and performs no I/O. No persistence, infrastructure, application, or host changes and no dependencies were added.

## Tests

`PolicyEvaluationTraceTests` covers successful trusted matching registrations, explicit deny rules, unknown/untrusted provenance, known and unknown scope relationships, final precedence, registration/rule ordering, structural determinism, collection immutability, missing capabilities, and unsupported actions. Existing policy evaluator tests continue to cover POLICY-001 through POLICY-004 semantics.

## Deferred concerns

Human-readable formatting/localization, durable audit records, authorization, authentication, scope hierarchy, policy configuration loading, persistence, logging/telemetry, execution, and orchestration remain deferred.
