# POLICY-002 Implementation — Capability Rules & Deterministic Policy Evaluation

## Objective

POLICY-002 extends the POLICY-001 Core policy model with an immutable capability context, explicit action-to-capability mapping, and simple action-scoped policy rules. Evaluation remains deterministic and fail-closed.

## Capability model

`PolicyCapability` is an immutable value object. It rejects empty or whitespace input and normalizes its value by trimming whitespace and converting to invariant lowercase. This gives capability matching consistent case-insensitive behavior.

The initial action-to-capability mapping is intentionally small and explicit:

| Action category | Required capability |
|---|---|
| `read` | `workspace.read` |
| `write` | `workspace.write` |
| `execute` | `process.execute` |

Action categories are matched exactly, without case sensitivity or fuzzy matching. Unsupported categories are not mapped to a capability.

## Context and rules

`PolicyContext` may contain capabilities and immutable `PolicyRule` values. It copies, sorts, and exposes these collections through read-only wrappers. Capability duplicates are removed after canonicalization.

A rule targets one action category and imposes a `PolicyDecision` with its reason. Matching is exact and case-insensitive. Rules are static input data; the implementation does not discover or load rules dynamically.

## Decision semantics

For complete context, a supported action with its required capability yields `Allow` with reason `capability.granted`. If the capability is absent, evaluation escalates with `capability.missing`. Absence is not treated as an explicit prohibition because the current context does not assert that its capability set is an authoritative deny-list.

Incomplete and unknown contexts retain POLICY-001's distinct escalation reasons. Unsupported actions escalate with `action.unknown`. An applicable explicit deny rule takes precedence over these baseline results, including when the capability is absent or context is incomplete.

Applicable rule decisions combine using the conservative ordering `Deny > Escalate > Allow`. A less restrictive rule can never elevate the baseline capability/context result. When decisions have equal restriction, evaluation retains the baseline or the first deterministically ordered rule reason.

## Determinism and immutability

Evaluation uses only the action and immutable context values. It performs no I/O, clock reads, randomness, environment inspection, persistence, external calls, or execution. Capability and rule inputs are copied and ordered during context construction, so later caller changes do not alter policy state.

## Boundaries

This is **not an authorization system**. It consumes a capability set already present in policy context; it does not authenticate a principal or decide who may receive a capability.

An `Allow` is only a policy decision. It does not enforce filesystem, process, OS, network, sandbox, tool, or secret-access security. No action is executed by policy.

Policy does not orchestrate tasks, agents, tools, retries, scheduling, recovery, or human approval. Policy decisions and rules are not persisted, and no persistence or audit linkage is added.

## Tests

Core tests cover all three action mappings, case normalization, missing capability and unsupported-action escalation, explicit denial with and without capabilities, denial precedence, all six required two-rule conflict combinations, immutable copied collections, invalid inputs, existing incomplete/unknown context behavior, and determinism.

## Deferred functionality

Deferred work includes capability registration and provenance, authorization, richer resource/action scopes, policy auditing/persistence, tool gateways and security enforcement, orchestration, approvals, dynamic policy management, and future policy phases. No generic rule engine or additional dependency is introduced.
