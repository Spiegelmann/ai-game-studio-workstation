# POLICY-002 — Capability Rules & Deterministic Policy Evaluation

## Objective

Extend the Policy foundation established by POLICY-001 into a small, concrete, deterministic policy evaluation model.

POLICY-001 established:

* `PolicyAction`
* `PolicyContext`
* `PolicyReason`
* `PolicyDecision`
* `IPolicyEvaluator`
* `PolicyEvaluator`
* Allow / Deny / Escalate semantics
* fail-closed behavior
* policy/execution separation
* policy/authorization separation

POLICY-002 should now introduce the **first concrete policy rules** and the concept of **capabilities** required to determine whether an action may be automatically permitted.

The goal is to make policy capable of producing meaningful `ALLOW`, `DENY`, and `ESCALATE` decisions while remaining:

* deterministic
* immutable
* provider-independent
* execution-independent
* persistence-independent
* authorization-independent
* security-boundary aware
* small and understandable

This is still a **policy domain task**, not an orchestration or execution task.

---

# 1. Read Before Changing Anything

Before modifying the repository, inspect the actual current implementation.

Read:

* `AGENTS.md`
* relevant `.github/rules/*`
* all FOUNDATION task specifications and implementation records
* all PERSISTENCE task specifications and implementation records
* `PERSISTENCE-005.md`
* `POLICY-001`
* `POLICY-001-IMPLEMENTATION.md`
* all current Core policy types
* all policy tests
* existing domain primitives
* existing Application contracts
* current architecture documentation
* current solution/project boundaries

Treat the repository as authoritative.

Do not assume that the previous task specification exactly matches the implementation.

---

# 2. Core Objective

POLICY-002 must answer a concrete question:

> Given a proposed action and its current policy context, does the system have sufficient capability and policy information to allow that action?

The evaluator should now be capable of producing meaningful outcomes.

At minimum, establish rules around:

```text
Known permitted capability → ALLOW
Known prohibited capability/action → DENY
Insufficient or unknown capability/context → ESCALATE
```

The exact model should follow the existing POLICY-001 implementation rather than replacing it.

Do not redesign the existing policy model unless a concrete defect requires it.

---

# 3. Capability Concept

Introduce a small domain concept representing a capability.

A capability represents something the system is explicitly permitted to perform.

Examples may include conceptual capabilities such as:

```text
ReadWorkspace
WriteWorkspace
ExecuteProcess
DeleteResource
PublishArtifact
```

However:

**Do not create a large catalogue of capabilities.**

POLICY-002 should establish the mechanism and use only a small number of representative capabilities required to prove the model.

Capabilities must be:

* immutable
* validated
* deterministic
* independent of infrastructure
* independent of concrete tools
* independent of MCP
* independent of operating-system APIs
* independent of specific agents

Do not model a capability as an implementation detail such as:

```text
"CanRunPowerShell.exe"
"CanInvokeToolX"
"CSharpCompilerProcess"
```

Those are execution concerns.

Prefer abstract capabilities.

---

# 4. Policy Context

Extend `PolicyContext` only as necessary to allow capability evaluation.

The context should be capable of answering conceptually:

> What capabilities are available to the actor/request being evaluated?

Do not introduce:

* user accounts
* authentication
* JWT
* OAuth
* identity-provider integrations
* roles
* permission databases
* external authorization systems

Those are authorization concerns and remain deferred.

The policy context may represent an already-established capability set without defining how that capability set was obtained.

---

# 5. Action-to-Capability Mapping

Establish an explicit relationship between proposed actions and required capabilities.

For example, conceptually:

```text
Read      → ReadWorkspace
Write     → WriteWorkspace
Execute   → ExecuteProcess
Delete    → DeleteResource
Publish   → PublishArtifact
```

Do not blindly use these exact names if the existing `PolicyAction` model has a different representation.

The important requirement is:

> A policy decision must be based on an explicit relationship between an action and the capability required to perform it.

Do not infer permissions from arbitrary strings.

Do not perform fuzzy matching.

Do not use reflection-based discovery.

Do not make capability matching case-sensitive in an inconsistent way.

Follow the existing value-object conventions.

---

# 6. Policy Rules

Introduce a minimal policy-rule abstraction if the existing design supports it cleanly.

A rule should conceptually determine:

```text
Does this rule apply?
What decision does it impose?
What reason explains that decision?
```

Avoid building a generic rule engine.

Do not introduce:

* scripting
* expression languages
* JSON-defined rules
* database-defined rules
* dynamic rule loading
* plugins
* dependency injection-based rule discovery
* arbitrary rule priorities
* configurable policy DSLs

The rule model should remain simple enough to understand by reading the Core code.

---

# 7. Required Policy Semantics

Establish the following semantics.

## ALLOW

An action may be automatically permitted when:

1. the action is valid and supported,
2. the required policy context is present,
3. the action has a known required capability,
4. the context explicitly contains that capability,
5. no stronger policy rule denies the action.

The evaluator returns `ALLOW`.

The reason should identify the policy basis for the decision.

---

## DENY

An action must be denied when a known policy rule explicitly prohibits it.

Examples may include:

* explicitly prohibited capability/action
* explicitly prohibited operation category
* an immutable safety restriction represented by the policy model

The important distinction is:

> A missing capability is not automatically the same thing as an explicit denial.

Missing/unknown information should normally produce `ESCALATE`, while an explicit prohibition produces `DENY`.

This distinction must be tested.

---

## ESCALATE

An action should require escalation when:

* required context is missing,
* the action is unsupported/unknown,
* the capability state is unknown,
* the evaluator cannot establish safe permission,
* policy information is insufficient to make an automatic decision.

Escalation must never silently become `ALLOW`.

---

# 8. Missing vs Denied Capability

This distinction is critical.

The evaluator must distinguish:

### Explicit denial

```text
Action: Execute
Capability: ExecuteProcess
Policy: explicitly prohibited
→ DENY
```

from:

### Missing capability

```text
Action: Execute
Capability: ExecuteProcess
Context: capability not present
→ ESCALATE
```

Do not automatically interpret:

```text
capability not present
```

as:

```text
explicitly forbidden
```

unless the policy model explicitly defines the capability set as authoritative and complete.

The current design should prefer the conservative distinction above.

Document the reasoning.

---

# 9. Unknown Actions

An action that the policy system does not understand must not be automatically allowed.

The expected behavior should be:

```text
Unknown action
    ↓
ESCALATE
```

unless the existing domain model makes the action invalid, in which case use the repository's established invalid-input semantics.

Do not silently map unknown actions to arbitrary capabilities.

---

# 10. Explicit Deny Rules

Introduce a minimal mechanism for explicitly denying an action/capability.

Use this to demonstrate that policy can override an otherwise available capability.

For example:

```text
Capability present
+
Explicit deny rule
=
DENY
```

This is important because future policy will need stronger restrictions than simple capability presence.

Do not implement a large deny-rule framework.

A simple immutable policy rule representation is sufficient.

---

# 11. Conflict Semantics

POLICY-001 documented:

```text
DENY > ESCALATE > ALLOW
```

POLICY-002 should now enforce this where multiple applicable policy rules are evaluated.

For example:

```text
Rule A → ALLOW
Rule B → DENY
```

must result in:

```text
DENY
```

Likewise:

```text
Rule A → ALLOW
Rule B → ESCALATE
```

must result in:

```text
ESCALATE
```

Never allow a less restrictive decision to override a more restrictive one.

Do not implement this as arbitrary numeric priority.

Model the semantics explicitly.

---

# 12. Decision Reasons

Every resulting decision must remain explainable.

Examples of semantic reasons:

```text
CapabilityGranted
ExplicitPolicyDenial
MissingCapability
UnknownAction
InsufficientContext
```

Use the existing `PolicyReason` model and conventions.

Do not create dozens of reason codes.

Reasons should be:

* deterministic
* stable
* machine-readable where appropriate
* understandable to later Application layers

Do not include sensitive information unnecessarily.

---

# 13. Determinism

Policy evaluation must remain deterministic.

For identical:

```text
PolicyAction
PolicyContext
PolicyRules
```

the evaluator must produce the same:

```text
Decision
Reason
```

Do not use:

* current time
* randomness
* external services
* filesystem state
* database state
* environment variables
* LLM calls

Policy evaluation must remain a pure domain decision.

---

# 14. Immutability

Policy objects should remain immutable after construction.

Pay particular attention to collections.

If a context contains capabilities or rules:

* do not expose mutable internal collections
* do not allow callers to mutate policy state after construction
* preserve deterministic iteration/evaluation semantics

Follow existing project conventions for immutable collections/value objects.

Do not add dependencies merely to achieve immutability.

---

# 15. Evaluation Order

Define deterministic evaluation order.

A sensible conceptual evaluation is:

```text
1. Validate action/context
2. Determine required capability
3. Determine whether the action is known
4. Evaluate explicit deny rules
5. Evaluate capability availability
6. Produce ALLOW / DENY / ESCALATE
```

However, follow the actual POLICY-001 design if it establishes a better ordering.

The important requirement is that the order is explicit and tested.

---

# 16. Policy vs Authorization

Do not cross the authorization boundary.

POLICY-002 may consume an already-established capability set.

It must not determine:

> "Is this user allowed to have this capability?"

That is authorization.

The policy layer should only determine:

> "Given this capability context, is this proposed action permitted under policy?"

Keep the distinction explicit in code and documentation.

---

# 17. Policy vs Security Enforcement

Do not implement execution security.

An `ALLOW` result means only:

> Policy permits this action.

It does not guarantee:

* filesystem access
* process execution
* sandbox permission
* OS permission
* network permission
* tool trust
* secret availability

Those controls belong elsewhere.

---

# 18. Policy vs Orchestration

Do not add:

* task execution
* agent execution
* tool invocation
* retries
* scheduling
* task transitions
* recovery
* background processing
* approval workflows

The future orchestrator will consume policy decisions.

Conceptually:

```text
Proposed Action
      │
      ▼
   Policy
      │
 ┌────┼────────┐
 ▼    ▼        ▼
ALLOW DENY  ESCALATE
```

The policy subsystem must not execute anything after producing the decision.

---

# 19. Policy vs Persistence

Do not persist policy decisions in POLICY-002.

Do not modify:

* `ITaskRepository`
* SQLite schema
* migrations
* Evidence persistence
* TaskAttempt persistence
* transition history

The persistence phase is complete.

Policy decisions may eventually become auditable evidence or operation records, but that belongs to a later integration phase.

Preserve the deferment established by PERSISTENCE-005.

---

# 20. Tests

Expand the Core policy test suite substantially.

At minimum cover:

### Capability matching

* known action + required capability present → ALLOW
* known action + capability absent → ESCALATE
* known action + explicitly denied capability → DENY

### Explicit denial

* capability present + deny rule → DENY
* capability absent + deny rule → DENY
* multiple rules where deny overrides allow → DENY

### Escalation

* unknown action → ESCALATE
* missing context → ESCALATE
* insufficient capability information → ESCALATE

### Conflict semantics

Test:

```text
ALLOW + ALLOW → ALLOW
ALLOW + ESCALATE → ESCALATE
ALLOW + DENY → DENY
ESCALATE + ESCALATE → ESCALATE
ESCALATE + DENY → DENY
DENY + DENY → DENY
```

### Reasons

Verify that each decision has the correct deterministic reason.

### Determinism

Identical inputs produce identical decisions and reasons.

### Immutability

Verify that callers cannot mutate policy state through exposed collections.

### Purity

Policy evaluation performs no I/O or external side effects.

Do not create artificial mocks solely to demonstrate pure domain behavior.

---

# 21. Architecture Constraints

Follow these constraints strictly:

* Core remains provider-independent.
* Policy remains in Core.
* No SQLite references.
* No HTTP references.
* No LLM SDK references.
* No MCP references.
* No filesystem implementation references.
* No operating-system process APIs.
* No external services.
* No persistence.
* No orchestration.
* No execution.
* No authorization implementation.
* No authentication.
* No user/role system.
* No generic rule engine.
* No scripting engine.
* No dynamic policy loading.
* No speculative dependencies.

Preserve existing public contracts unless a concrete defect requires modification.

Prefer the smallest correct implementation.

---

# 22. File Placement

Follow existing repository conventions.

Policy implementation should remain in the Core project.

Tests should remain in the existing Core test project.

Do not create new projects.

Use the existing policy namespace/folder established by POLICY-001.

---

# 23. Documentation

Create:

```text
POLICY-002-IMPLEMENTATION.md
```

Document:

* objective
* capability model
* action/capability mapping
* explicit deny semantics
* missing-vs-denied semantics
* escalation semantics
* conflict semantics
* decision reasons
* determinism
* immutability
* authorization boundary
* security boundary
* orchestration boundary
* persistence boundary
* tests
* deferred functionality

Explicitly document that this is **not** an authorization system.

---

# 24. Verification

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

# 25. Scope Guard

Do not proceed into:

* POLICY-003
* authorization
* identity
* role/permission management
* orchestration
* agent execution
* tool execution
* MCP
* persistence/audit integration
* autonomous loops
* scheduling
* human approval workflows
* game-specific policy

If implementation opportunities for these areas become apparent, document them as deferred work.

Do not implement them.

---

# 26. Completion Report

Return a structured completion report containing:

## Task

* Task ID
* Task title

## Policy Model

* capability model
* action/capability mapping
* rule model
* decision semantics
* deny semantics
* escalation semantics
* conflict semantics

## Architecture

Explain the boundaries between:

* policy
* authorization
* security enforcement
* orchestration
* execution
* persistence

## Implementation

List every changed file and why it changed.

## Tests

Report:

* tests added/modified
* important scenarios
* total test count

## Deferred Concerns

Explicitly identify functionality intentionally left for later.

## Verification

Report exact results for:

* `dotnet test`
* `dotnet build`
* `git diff --check`
* diagnostics

## Final Assessment

Classify as:

* **READY**
* **READY WITH DOCUMENTED DEFERMENTS**
* **NOT READY**

Do not automatically begin POLICY-003.

Stop after producing the completion report.
