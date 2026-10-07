# FOUNDATION-004 — Explicit Task Lifecycle State Machine

## Objective

Implement FOUNDATION-004 for the AI Game Studio Workstation.

The purpose of this task is to make the Task lifecycle an explicit, deterministic, strongly validated domain state machine.

FOUNDATION-002 established the foundational Task domain primitives.

FOUNDATION-003 established the Task aggregate and its domain concepts.

FOUNDATION-004 must now make lifecycle transitions explicit, centralized, deterministic, and thoroughly tested.

This is a domain-layer task.

Do not implement orchestration, agents, LLM integration, tool execution, persistence, infrastructure, policy engines, authorization, recovery workflows, or UI.

---

# 1. Architectural Context

The system follows:

* Domain-Driven Design
* Onion Architecture
* Clean Architecture
* Explicit dependency direction
* Deterministic domain behavior
* Default-deny behavior where authorization/policy is eventually involved
* Strong separation between domain decisions and infrastructure concerns

Current architectural direction:

```text
Core
  └── Domain

Application
  └── Use Cases / Application Services

Infrastructure
  └── Persistence / External Systems / Adapters

Host
  └── Composition / Runtime
```

For this task:

```text
Core.Domain
    ↓
Task lifecycle/state machine
```

The lifecycle must remain entirely inside the domain.

The domain must not depend on:

* EF Core
* ASP.NET
* filesystem APIs
* Docker
* Git
* GitHub
* LLM SDKs
* MCP
* HTTP
* databases
* logging frameworks
* configuration providers
* environment variables
* external processes
* operating-system APIs

---

# 2. Existing Domain

FOUNDATION-002/003 already established Task-related domain concepts.

Before modifying anything:

1. Inspect the current repository.
2. Inspect the existing Task domain implementation.
3. Inspect all existing Task tests.
4. Identify the exact existing APIs and types.
5. Preserve working behavior unless it directly conflicts with this specification.
6. Do not invent duplicate concepts that already exist.

Do not assume exact class/property names.

Adapt the implementation to the existing codebase while preserving the semantic requirements below.

---

# 3. Required Task States

The Task lifecycle must support these explicit states:

```text
PROPOSED
READY
CLAIMED
IN_PROGRESS
VERIFICATION
ACCEPTED
BLOCKED
FAILED
CANCELLED
RECOVERING
HUMAN_REVIEW_REQUIRED
```

Represent these as a strongly typed domain concept, preferably an enum or equivalent immutable value representation consistent with the existing codebase.

Do not represent lifecycle state using arbitrary strings.

---

# 4. Lifecycle Semantics

The lifecycle represents the progression of autonomous work.

The intended conceptual lifecycle is:

```text
PROPOSED
    ↓
READY
    ↓
CLAIMED
    ↓
IN_PROGRESS
    ↓
VERIFICATION
    ↓
ACCEPTED
```

Exceptional paths may lead to:

```text
BLOCKED
FAILED
CANCELLED
RECOVERING
HUMAN_REVIEW_REQUIRED
```

The state machine must explicitly define which transitions are legal.

Do not rely on callers to enforce lifecycle rules.

The domain itself must reject illegal transitions.

---

# 5. Legal Transitions

Implement an explicit transition table.

At minimum, support the following transitions.

## PROPOSED

Allowed:

```text
PROPOSED → READY
PROPOSED → CANCELLED
```

Disallowed:

```text
PROPOSED → CLAIMED
PROPOSED → IN_PROGRESS
PROPOSED → VERIFICATION
PROPOSED → ACCEPTED
```

---

## READY

Allowed:

```text
READY → CLAIMED
READY → BLOCKED
READY → CANCELLED
```

Disallowed:

```text
READY → IN_PROGRESS
READY → VERIFICATION
READY → ACCEPTED
```

A task must be claimed before work begins.

---

## CLAIMED

Allowed:

```text
CLAIMED → IN_PROGRESS
CLAIMED → BLOCKED
CLAIMED → CANCELLED
CLAIMED → RECOVERING
```

The exact recovery semantics must remain domain-level and deterministic.

---

## IN_PROGRESS

Allowed:

```text
IN_PROGRESS → VERIFICATION
IN_PROGRESS → BLOCKED
IN_PROGRESS → FAILED
IN_PROGRESS → CANCELLED
IN_PROGRESS → RECOVERING
```

A task cannot become ACCEPTED directly from IN_PROGRESS.

Verification must occur first.

---

## VERIFICATION

Allowed:

```text
VERIFICATION → ACCEPTED
VERIFICATION → FAILED
VERIFICATION → BLOCKED
VERIFICATION → RECOVERING
VERIFICATION → HUMAN_REVIEW_REQUIRED
```

A task must pass through VERIFICATION before it can become ACCEPTED.

---

## ACCEPTED

This is a successful terminal state.

No normal lifecycle transitions are permitted from:

```text
ACCEPTED
```

In particular:

```text
ACCEPTED → IN_PROGRESS
ACCEPTED → VERIFICATION
ACCEPTED → FAILED
ACCEPTED → CANCELLED
```

must all be rejected.

This prevents accidentally reopening completed work.

---

## BLOCKED

Allowed:

```text
BLOCKED → READY
BLOCKED → CLAIMED
BLOCKED → RECOVERING
BLOCKED → CANCELLED
BLOCKED → HUMAN_REVIEW_REQUIRED
```

The implementation may restrict some of these if the existing FOUNDATION-003 domain model already encodes a stronger invariant.

Do not introduce arbitrary automatic unblocking behavior.

The state machine should merely permit the explicitly defined transition.

---

## FAILED

Allowed:

```text
FAILED → RECOVERING
FAILED → HUMAN_REVIEW_REQUIRED
FAILED → CANCELLED
```

A failed task must not silently return to active execution.

Recovery must be an explicit state transition.

---

## CANCELLED

Terminal state.

No transitions are permitted from:

```text
CANCELLED
```

---

## RECOVERING

Allowed:

```text
RECOVERING → READY
RECOVERING → CLAIMED
RECOVERING → IN_PROGRESS
RECOVERING → FAILED
RECOVERING → BLOCKED
RECOVERING → HUMAN_REVIEW_REQUIRED
RECOVERING → CANCELLED
```

Do not implement automatic retry logic here.

RECOVERING describes lifecycle state only.

Retry policy belongs to a later policy/application layer.

---

## HUMAN_REVIEW_REQUIRED

Allowed:

```text
HUMAN_REVIEW_REQUIRED → READY
HUMAN_REVIEW_REQUIRED → BLOCKED
HUMAN_REVIEW_REQUIRED → CANCELLED
```

The domain must not automatically bypass human review.

Do not implement autonomous approval from this state.

---

# 6. Important Invariants

The following invariants are mandatory.

## Invariant 1 — No arbitrary transitions

A Task may only transition through explicitly legal edges.

Never implement:

```csharp
task.State = newState;
```

as an unrestricted public operation.

The aggregate must control state changes.

---

## Invariant 2 — ACCEPTED requires verification

A Task cannot transition directly:

```text
IN_PROGRESS → ACCEPTED
```

or:

```text
CLAIMED → ACCEPTED
```

or:

```text
READY → ACCEPTED
```

etc.

The lifecycle must require:

```text
... → VERIFICATION → ACCEPTED
```

---

## Invariant 3 — Terminal states remain terminal

At minimum:

```text
ACCEPTED
CANCELLED
```

must be terminal.

No transition may leave either state.

---

## Invariant 4 — No implicit reopening

Once accepted, the task cannot be reopened.

Do not provide convenience APIs that circumvent the state machine.

---

## Invariant 5 — Transition validation belongs to the domain

Application code must not need to know the complete transition table to determine whether a transition is legal.

The domain should expose a single authoritative mechanism for determining/performing legal transitions.

---

# 7. Transition Representation

Introduce an explicit domain concept for lifecycle transitions if the current architecture does not already provide one.

A transition should conceptually contain:

```text
CurrentState
TargetState
```

Optionally, where justified by the existing domain model:

```text
TransitionReason
```

However, do not introduce speculative metadata.

The important requirement is that the transition graph has exactly one authoritative representation.

Avoid duplicating the transition rules across:

* Task
* validators
* tests
* application services
* helper methods

---

# 8. Transition Validation API

Provide a domain-level API capable of answering:

```text
Can state X transition to state Y?
```

and/or performing the transition.

The exact API should follow existing project conventions.

Examples of acceptable conceptual APIs include:

```csharp
TaskStateTransitionResult TryTransitionTo(TaskState target)
```

or:

```csharp
bool CanTransitionTo(TaskState target)
```

combined with a domain transition operation.

Prefer an API that prevents callers from bypassing validation.

Do not expose mutable state.

---

# 9. Failure Behavior

Illegal transitions must fail deterministically.

Do not silently ignore invalid transitions.

Do not automatically find an alternative transition.

Do not mutate the Task when a transition is rejected.

The rejected transition must leave the Task completely unchanged.

If the existing domain uses a Result/Error pattern, use that.

If it does not, introduce the smallest appropriate domain mechanism consistent with the existing architecture.

Do not introduce exceptions merely as a convenience if the established domain conventions already use explicit results.

---

# 10. Atomicity

A transition must behave atomically.

For a successful transition:

```text
old state
    ↓
validated transition
    ↓
new state
```

For a failed transition:

```text
old state
    ↓
rejected
    ↓
old state remains unchanged
```

No partial mutation is permitted.

This includes timestamps and revision information.

If FOUNDATION-003 established immutable aggregate updates, preserve that model.

---

# 11. Revision and Timestamp Behavior

Respect the revision and timestamp semantics established in FOUNDATION-003.

A successful lifecycle transition must produce the appropriate new aggregate revision/update timestamp according to the existing Task model.

An unsuccessful transition must not advance:

* revision
* updated timestamp
* state
* any other mutable lifecycle information

Do not invent a second revision mechanism.

---

# 12. Transition History

Do NOT add persistent transition history unless the existing FOUNDATION-003 design explicitly requires it.

FOUNDATION-004 is responsible for enforcing the state graph.

Evidence/audit history is a later concern.

Do not prematurely implement:

* event sourcing
* audit databases
* event buses
* append-only logs
* persistence models

The domain state machine should remain small and deterministic.

---

# 13. Separation From Policy

Do not add policy decisions to the state machine.

For example, this task must NOT decide:

* whether an agent is allowed to claim a task
* whether a tool call is authorized
* whether a model is trusted
* whether a task is safe
* whether a retry should happen
* whether a human is required because of risk
* whether a particular capability is available
* whether a resource is authorized
* whether a task should be prioritized

The state machine only determines:

> Given the current lifecycle state and requested target state, is this transition structurally legal?

Later policy/application layers determine whether a transition should actually be requested.

---

# 14. Separation From Verification

VERIFICATION is a lifecycle state.

It is NOT the verification system itself.

Do not implement:

* test execution
* artifact validation
* static analysis
* gameplay validation
* screenshots
* evidence collection
* LLM evaluation
* acceptance scoring

Those belong to later FOUNDATION stages.

For now:

```text
IN_PROGRESS → VERIFICATION
```

simply represents entering the verification phase.

Likewise:

```text
VERIFICATION → ACCEPTED
```

represents a legal lifecycle transition.

The actual verification conditions will be implemented later.

---

# 15. Separation From Recovery

RECOVERING is a lifecycle state.

Do not implement:

* retry scheduling
* exponential backoff
* checkpoint restoration
* process restart
* agent reassignment
* rollback
* automatic remediation

Those belong to later recovery/orchestration work.

FOUNDATION-004 only establishes that recovery is an explicit lifecycle state and that only explicitly legal transitions can enter or leave it.

---

# 16. Recommended Internal Structure

Use the smallest clean structure that fits the existing repository.

A possible conceptual structure is:

```text
src/
└── Core/
    └── Domain/
        └── Tasks/
            ├── Task.cs
            ├── TaskState.cs
            ├── TaskStateTransition.cs
            ├── TaskStateTransitionResult.cs
            └── ...
```

Do not blindly create these exact files.

First inspect FOUNDATION-002/003.

If equivalent concepts already exist, extend them instead of duplicating them.

The transition graph should have one authoritative implementation.

---

# 17. Testing Requirements

Add comprehensive domain tests.

Tests must verify every legal transition.

At minimum, test:

```text
PROPOSED → READY
PROPOSED → CANCELLED

READY → CLAIMED
READY → BLOCKED
READY → CANCELLED

CLAIMED → IN_PROGRESS
CLAIMED → BLOCKED
CLAIMED → CANCELLED
CLAIMED → RECOVERING

IN_PROGRESS → VERIFICATION
IN_PROGRESS → BLOCKED
IN_PROGRESS → FAILED
IN_PROGRESS → CANCELLED
IN_PROGRESS → RECOVERING

VERIFICATION → ACCEPTED
VERIFICATION → FAILED
VERIFICATION → BLOCKED
VERIFICATION → RECOVERING
VERIFICATION → HUMAN_REVIEW_REQUIRED

BLOCKED → READY
BLOCKED → CLAIMED
BLOCKED → RECOVERING
BLOCKED → CANCELLED
BLOCKED → HUMAN_REVIEW_REQUIRED

FAILED → RECOVERING
FAILED → HUMAN_REVIEW_REQUIRED
FAILED → CANCELLED

RECOVERING → READY
RECOVERING → CLAIMED
RECOVERING → IN_PROGRESS
RECOVERING → FAILED
RECOVERING → BLOCKED
RECOVERING → HUMAN_REVIEW_REQUIRED
RECOVERING → CANCELLED

HUMAN_REVIEW_REQUIRED → READY
HUMAN_REVIEW_REQUIRED → BLOCKED
HUMAN_REVIEW_REQUIRED → CANCELLED
```

Also test invalid transitions.

Do not merely test a handful of invalid examples.

Build exhaustive coverage over the transition matrix:

```text
every state × every possible target state
```

and assert that:

* legal transitions succeed
* illegal transitions fail
* rejected transitions do not mutate the Task
* rejected transitions do not change revision
* rejected transitions do not change updated timestamp

---

# 18. Terminal State Tests

Explicitly test:

```text
ACCEPTED → every state
```

and:

```text
CANCELLED → every state
```

Every transition must be rejected.

This is particularly important because terminal-state reopening is a serious orchestration integrity problem.

---

# 19. Acceptance-Path Tests

Explicitly verify:

```text
PROPOSED → READY → CLAIMED → IN_PROGRESS → VERIFICATION → ACCEPTED
```

succeeds.

Also verify that the following fail:

```text
PROPOSED → ACCEPTED

READY → ACCEPTED

CLAIMED → ACCEPTED

IN_PROGRESS → ACCEPTED
```

This protects the core invariant that acceptance requires verification.

---

# 20. Immutability Tests

Where the existing Task implementation is immutable, verify that:

* successful transition returns the correct updated aggregate
* original Task remains unchanged
* failed transition leaves original Task unchanged
* state cannot be externally mutated
* lifecycle collections/representations cannot be externally mutated

Preserve FOUNDATION-003's immutability guarantees.

---

# 21. No Hidden Side Effects

Tests should establish that lifecycle transitions do not:

* access filesystem
* access network
* access Git
* invoke processes
* access databases
* call external services
* invoke an LLM
* write files
* depend on environment variables

The domain state machine must remain pure/deterministic.

---

# 22. Exhaustive Transition Matrix

Prefer representing the transition rules in a way that makes the graph easy to inspect and audit.

For example, conceptually:

```text
PROPOSED:
    READY
    CANCELLED

READY:
    CLAIMED
    BLOCKED
    CANCELLED

CLAIMED:
    IN_PROGRESS
    BLOCKED
    CANCELLED
    RECOVERING

IN_PROGRESS:
    VERIFICATION
    BLOCKED
    FAILED
    CANCELLED
    RECOVERING

VERIFICATION:
    ACCEPTED
    FAILED
    BLOCKED
    RECOVERING
    HUMAN_REVIEW_REQUIRED

ACCEPTED:
    none

BLOCKED:
    READY
    CLAIMED
    RECOVERING
    CANCELLED
    HUMAN_REVIEW_REQUIRED

FAILED:
    RECOVERING
    HUMAN_REVIEW_REQUIRED
    CANCELLED

CANCELLED:
    none

RECOVERING:
    READY
    CLAIMED
    IN_PROGRESS
    FAILED
    BLOCKED
    HUMAN_REVIEW_REQUIRED
    CANCELLED

HUMAN_REVIEW_REQUIRED:
    READY
    BLOCKED
    CANCELLED
```

The actual implementation may differ structurally, but these semantics must remain intact unless an existing domain invariant makes a transition impossible.

---

# 23. Architecture Rules

Do not:

* introduce service locator patterns
* introduce static global state
* introduce mutable global transition registries
* introduce dependency injection into Core merely for this feature
* reference Application from Core
* reference Infrastructure from Core
* reference Host from Core
* introduce persistence
* introduce external packages without necessity
* redesign FOUNDATION-003
* duplicate domain primitives

Keep the implementation boring, explicit, deterministic, and easy to audit.

---

# 24. Security / Trust Considerations

The state machine is part of the trusted domain boundary.

Therefore:

* untrusted task content must not define lifecycle transitions
* task descriptions must not control the state machine
* generated Lua/code/docs/logs must not control the state machine
* LLM output must not directly mutate lifecycle state
* external tools must not directly mutate lifecycle state
* callers must request a transition through the domain API
* the domain must validate the transition independently

This is foundational for the later prompt-injection and autonomous-execution defenses.

Do not implement the full security model here.

Simply ensure this domain boundary cannot be bypassed through public mutable state.

---

# 25. Documentation

Update the relevant domain documentation if the repository contains documentation for the Task lifecycle.

Document:

1. The available states.
2. The legal transition graph.
3. Terminal states.
4. The requirement that acceptance follows verification.
5. The fact that policy/authorization is intentionally outside this component.

Keep documentation concise and authoritative.

Do not create extensive architectural documentation for later systems.

---

# 26. Scope Restrictions

FOUNDATION-004 must NOT implement:

* Agent Runtime
* Orchestrator
* Policy Engine
* Tool Gateway
* MCP
* LLM integration
* prompt handling
* prompt injection detection
* persistence
* database integration
* event sourcing
* message bus
* task scheduling
* retry engine
* checkpoint recovery
* filesystem operations
* Git operations
* Docker integration
* GMod integration
* CI/CD changes unless required to run the existing tests
* UI
* API endpoints

Those are future work.

---

# 27. Required Workflow

Before making changes:

1. Inspect the repository.
2. Inspect FOUNDATION-002/003 implementation.
3. Inspect existing tests.
4. Identify existing state and transition concepts.
5. Identify the smallest implementation consistent with the current architecture.

Then:

1. Implement the authoritative transition graph.
2. Integrate it with Task lifecycle behavior.
3. Add exhaustive transition tests.
4. Add invariant tests.
5. Run the complete test suite.
6. Run the build.
7. Verify no architectural dependency violations were introduced.
8. Verify no unrelated files were modified.

---

# 28. Definition of Done

FOUNDATION-004 is complete only when:

### Domain

* [ ] All required lifecycle states exist.
* [ ] States are strongly typed.
* [ ] Legal transitions are explicitly defined.
* [ ] Illegal transitions are rejected.
* [ ] Transition rules have one authoritative implementation.
* [ ] Task state cannot be arbitrarily mutated.
* [ ] Terminal states cannot be reopened.
* [ ] ACCEPTED requires VERIFICATION.
* [ ] Failed transitions produce no partial mutation.
* [ ] Revision/timestamp behavior follows FOUNDATION-003 semantics.

### Tests

* [ ] Every legal transition is tested.
* [ ] Every possible state/target combination is covered.
* [ ] Terminal states are exhaustively tested.
* [ ] Acceptance-path tests exist.
* [ ] Invalid transitions leave the Task unchanged.
* [ ] Revision does not change on rejected transitions.
* [ ] Timestamp does not change on rejected transitions.
* [ ] Existing tests remain passing.

### Architecture

* [ ] Core remains independent.
* [ ] No Infrastructure dependency exists.
* [ ] No Application dependency exists.
* [ ] No Host dependency exists.
* [ ] No external service is required.
* [ ] No persistence is introduced.
* [ ] No orchestration logic is introduced.

### Quality

* [ ] Build succeeds.
* [ ] Tests pass.
* [ ] No warnings were introduced unless already present.
* [ ] No duplicated transition logic exists.
* [ ] Public APIs are minimal.
* [ ] Domain behavior is deterministic.
* [ ] Code follows existing project conventions.

---

# 29. Required Final Report

After implementation, provide a concise completion report containing:

## Implemented

List the files/classes changed or created.

## Lifecycle

Summarize the implemented state machine.

## Tests

Report:

* total tests
* new tests
* passing/failing count
* build result
* warnings/errors

## Architecture

Confirm that Core remains isolated and no forbidden dependencies were introduced.

## Deviations

Explicitly report any deviation from this specification and why it was necessary.

Do not silently reinterpret requirements.

## Remaining Work

Identify only work belonging to later FOUNDATION tasks.

Do not implement that work as part of FOUNDATION-004.
