# POLICY-001 — Policy Foundation & Decision Boundaries

## Objective

Begin the Policy phase of the AI Game Studio Workstation architecture.

The purpose of this task is to establish the **foundational policy model and decision boundaries** that later orchestration, execution, recovery, security, and autonomous behavior can rely upon.

This task must **not** implement orchestration, agent execution, tool invocation, MCP integration, scheduling, autonomous loops, or game-specific policy.

The goal is to establish a small, explicit, deterministic policy foundation.

The policy layer should answer:

> **"Given a proposed action and the current known context, is this action permitted, denied, or requires an explicit escalation?"**

It should **not** answer:

> "How should the system execute this action?"

Execution belongs to later phases.

---

# 1. Read Before Changing Anything

Before modifying the repository, inspect the actual current implementation.

Read:

* `AGENTS.md`
* relevant `.github/rules/*`
* all existing architecture/design documents
* all FOUNDATION task specifications and implementation records
* all PERSISTENCE task specifications and implementation records
* `PERSISTENCE-005.md`
* current Core project
* current Application project
* current Infrastructure project
* current Host project
* existing domain primitives
* existing application contracts
* existing persistence contracts
* existing tests
* solution/project configuration

Pay particular attention to the architectural boundaries established during FOUNDATION and PERSISTENCE.

Do not assume that the task specification perfectly represents the current repository.

The repository is authoritative.

---

# 2. Understand the Policy Boundary

Policy is a **decision boundary**, not an execution mechanism.

The policy subsystem must be capable of expressing decisions such as:

```text
ALLOW
DENY
ESCALATE
```

The precise representation may differ if the existing architecture provides a better domain-appropriate abstraction, but the semantics must remain explicit.

A policy decision should be:

* deterministic
* inspectable
* testable
* independent of execution
* independent of SQLite
* independent of HTTP
* independent of MCP
* independent of specific LLM providers
* independent of operating-system process execution
* independent of game-specific implementation details

Do not create policy rules for concrete tools or agents yet.

---

# 3. Core Policy Concepts

Establish the minimum domain concepts required to represent a policy decision.

At minimum, reason about:

### PolicyDecision

Represents the outcome of policy evaluation.

It should distinguish:

* allowed
* denied
* escalation required

### PolicyReason

A policy decision must be explainable.

A denial or escalation must not be represented only as an opaque boolean.

The reason should be structured enough for later layers to understand why the decision occurred.

Avoid designing a large error taxonomy prematurely.

### PolicyContext

Represent the information that policy evaluation is allowed to inspect.

Keep this intentionally minimal.

The context should support future policy decisions without coupling the Core domain to:

* HTTP
* SQLite
* filesystem implementation details
* MCP
* LLM SDKs
* provider-specific types

Do not add fields merely because they might become useful someday.

### PolicyAction / ProposedAction

Represent the abstract action being evaluated.

The action must describe **what is being proposed**, not how it is executed.

For example, policy should eventually be able to distinguish conceptual actions such as:

```text
read
write
execute
delete
publish
modify
external-side-effect
```

However, do not hard-code a large action catalogue unless the current architecture requires it.

Prefer a validated, extensible domain representation.

---

# 4. Policy Evaluation Contract

Define a narrow policy evaluation abstraction.

Conceptually:

```text
PolicyDecision Evaluate(
    ProposedAction action,
    PolicyContext context
)
```

The actual API should follow the conventions already established in the repository.

The contract must:

* have deterministic semantics
* be side-effect free
* not perform persistence
* not execute actions
* not invoke external services
* not depend on infrastructure
* be easy to unit test

If asynchronous behavior is not required by the domain, do not introduce async merely for consistency.

---

# 5. Explicit Decision Semantics

Define and test the semantics of:

## ALLOW

The proposed action satisfies all currently applicable policy constraints.

This means:

> Policy permits the action.

It does **not** mean:

> The action will definitely succeed.

Execution remains a separate concern.

## DENY

The proposed action violates a policy constraint.

A denial must contain a deterministic reason.

It must be possible for later layers to distinguish:

```text
Policy denied the action
```

from:

```text
Execution attempted the action and failed
```

These must never be conflated.

## ESCALATE

The policy system cannot safely permit the action automatically under the current rules/context.

Escalation means:

> A higher-trust or explicitly authorized decision process is required.

It does **not** mean:

* retry
* execution failure
* temporary failure
* automatic approval
* human approval necessarily, unless later policy explicitly defines that

Keep the semantic distinction clear.

---

# 6. Fail-Closed Principle

The policy foundation must be **fail-closed**.

When the policy layer cannot establish that an action is permitted, it must not silently produce `ALLOW`.

Examples include:

* invalid policy input
* missing mandatory context
* unsupported action category
* ambiguous policy state

The exact handling should be represented through the policy model rather than arbitrary exceptions where possible.

The important invariant is:

> Uncertainty must never silently become permission.

Do not over-generalize this into unrelated application behavior.

---

# 7. Determinism

Policy evaluation must be deterministic for identical:

```text
PolicyAction
+
PolicyContext
+
PolicyRules
```

The same inputs must produce the same decision.

Do not use:

* current time
* randomness
* external network state
* database state
* environment variables
* machine-specific state

unless a concrete existing requirement explicitly demands it.

There should be no hidden policy side effects.

---

# 8. Policy Rule Abstraction

Introduce a minimal abstraction for policy rules only if required by the actual model.

A rule should conceptually answer:

```text
Does this rule apply?
What decision does it impose?
Why?
```

Avoid implementing a generic rule engine.

Do not build:

* expression languages
* dynamic scripting
* configuration-driven rule interpreters
* plugin-based policy engines
* rule priority frameworks
* complex rule composition

Those may be considered later if justified.

For POLICY-001, the objective is the **boundary**, not a sophisticated policy engine.

---

# 9. Conflict Semantics

Explicitly consider what happens if multiple policy rules produce different decisions.

For example:

```text
Rule A → ALLOW
Rule B → DENY
```

The policy foundation must not leave this ambiguous.

Use a conservative semantic model.

Unless the existing architecture establishes another rule, the default should be:

```text
DENY > ESCALATE > ALLOW
```

However, do not implement priority merely as arbitrary numeric ordering.

The important invariant is:

> A more restrictive policy decision must not be weakened by another rule.

If the current model does not yet require multi-rule evaluation, document the conflict semantics without building an elaborate rule engine.

---

# 10. Policy vs Authorization

Keep these concepts separate.

Policy determines:

> Is this proposed action permitted under system policy?

Authorization determines:

> Does this principal/actor have authority to perform this action?

Do not prematurely implement:

* users
* roles
* permissions databases
* identity providers
* OAuth
* JWT
* RBAC
* ABAC

POLICY-001 may define the conceptual boundary between policy and authorization, but authorization itself belongs to a later phase unless already required by the repository.

---

# 11. Policy vs Security

Policy is one component of the broader security model.

Do not claim that a policy `ALLOW` means an action is secure.

Later security controls may still reject the action because of:

* authorization
* sandboxing
* filesystem restrictions
* tool trust
* secret handling
* resource limits
* execution isolation

POLICY-001 should establish the decision boundary without attempting to solve all security concerns.

---

# 12. Policy vs Orchestration

Policy must not know about orchestration.

The policy layer should not:

* create tasks
* update task state
* schedule work
* invoke agents
* invoke tools
* retry actions
* recover failed actions
* choose which agent should execute an action

The future orchestrator will consume policy decisions.

Conceptually:

```text
Orchestrator
     │
     ▼
Policy Evaluation
     │
     ├── ALLOW
     ├── DENY
     └── ESCALATE
```

But policy does not call back into the orchestrator.

---

# 13. Policy and Persistence

Do not persist policy decisions in POLICY-001 unless an existing requirement explicitly demands it.

Policy evaluation should remain a pure decision boundary.

The persistence phase has already established durable task, attempt, transition, and evidence storage.

Later application/orchestration phases may decide which policy decisions become auditable records.

Do not couple the policy model to `ITaskRepository`.

---

# 14. Policy and Evidence

Review the existing `EvidenceRecord` model and the PERSISTENCE-004/PERSISTENCE-005 deferral concerning Operation linkage.

Do not modify Evidence persistence in this task merely to associate policy decisions with evidence.

Instead, document how policy decisions could later become auditable without violating the current domain boundaries.

This is an architectural observation, not an implementation requirement.

---

# 15. Tests

Create focused Core tests for the policy model.

At minimum cover:

### Decision construction

* valid ALLOW
* valid DENY
* valid ESCALATE
* invalid/empty reason where reason is required
* invalid action
* invalid context

### Determinism

Identical action/context inputs produce identical decisions.

### Fail closed

Unsupported or insufficient information cannot silently result in ALLOW.

### Conflict semantics

If multiple policy decisions are evaluated, verify restrictive decisions cannot be weakened.

If multi-rule evaluation is intentionally deferred, document that instead of implementing it.

### Purity

Policy evaluation does not:

* persist data
* mutate external state
* invoke infrastructure
* perform I/O

Do not use mocks to prove the absence of dependencies if architectural inspection already establishes it.

---

# 16. Architecture Constraints

Follow these constraints strictly:

* Core remains provider-independent.
* Application contracts remain infrastructure-independent.
* Infrastructure owns implementation details.
* No SQLite references in Core policy code.
* No HTTP references in Core policy code.
* No LLM SDK references in Core policy code.
* No MCP references in Core policy code.
* No tool execution in policy.
* No orchestration in policy.
* No persistence in policy.
* No external side effects in policy.
* No speculative dependencies.
* No generic rule engine.
* No premature authorization system.
* No game-specific policy rules.

Prefer small immutable/value-oriented domain types where appropriate.

Follow existing project conventions rather than introducing a new coding style.

---

# 17. File Placement

Place new concepts according to the repository's existing architectural conventions.

Prefer a structure conceptually similar to:

```text
src/Core/Domain/Policy/
```

and:

```text
tests/Core.Tests/
```

Do not create new projects unless the existing architecture clearly requires them.

If the repository already has a more appropriate location, follow it.

---

# 18. Documentation

Create:

```text
POLICY-001.md
```

The implementation record must document:

* objective
* policy boundary
* concepts introduced
* decision semantics
* fail-closed semantics
* conflict semantics
* policy/authorization distinction
* policy/security distinction
* policy/orchestration distinction
* policy/persistence distinction
* tests
* architectural decisions
* intentionally deferred functionality

Be explicit about what POLICY-001 **does not** implement.

---

# 19. Verification

Before completion run:

```text
dotnet test AiGameStudioWorkstation.sln --verbosity minimal
dotnet build AiGameStudioWorkstation.sln
git diff --check
```

Inspect diagnostics for all changed C# files.

Expected:

* zero test failures
* zero build errors
* zero new warnings unless explicitly justified
* zero diff-check errors

Report exact test counts.

---

# 20. Scope Guard

Do not proceed into:

* POLICY-002
* authorization
* orchestration
* agent execution
* tool execution
* MCP integration
* autonomous loops
* scheduling
* recovery orchestration
* human approval workflows
* game-specific policy

Even if implementation opportunities become apparent, document them as future work rather than implementing them.

---

# 21. Completion Report

Return a structured report containing:

## Task

* Task ID
* Task title

## Policy Model

* concepts introduced
* decision semantics
* policy contract
* fail-closed behavior
* conflict semantics

## Architecture

Explain how the policy boundary relates to:

* Core
* Application
* Infrastructure
* persistence
* orchestration
* security
* authorization

## Implementation

List every changed file and why it changed.

## Tests

List:

* tests added
* important scenarios covered
* total test count

## Deferred Concerns

Explicitly list functionality intentionally left for later phases.

## Verification

Report:

* `dotnet test`
* `dotnet build`
* `git diff --check`
* diagnostics

## Final Assessment

Classify the task as:

* **READY**
* **READY WITH DOCUMENTED DEFERMENTS**
* **NOT READY**

Do not begin POLICY-002 automatically.

Stop after producing the completion report.
