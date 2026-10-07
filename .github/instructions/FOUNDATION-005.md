# FOUNDATION-005 — Task Identity, Correlation & Idempotency Semantics

## Objective

Implement FOUNDATION-005 for the AI Game Studio Workstation.

The purpose of this task is to establish strong domain semantics for:

* Task identity
* Task revision identity
* Operation/correlation identity
* Idempotent lifecycle operations

FOUNDATION-004 established the deterministic Task lifecycle/state machine.

FOUNDATION-005 now establishes the identity guarantees required for later application orchestration, retries, recovery, agent execution, tool execution, and eventually distributed/autonomous workflows.

This remains a domain-layer task.

Do not implement orchestration, persistence, queues, agents, LLM integration, tool execution, MCP, infrastructure, or distributed coordination.

---

# 1. Architectural Context

The system follows:

* Domain-Driven Design
* Onion Architecture
* Clean Architecture
* Explicit dependency direction
* Deterministic domain behavior
* Strong domain invariants
* Default-deny behavior for future authorization/policy layers

The relevant dependency direction remains:

```text
Core.Domain
    ↓
Application
    ↓
Infrastructure
    ↓
Host
```

For FOUNDATION-005, work should remain entirely within:

```text
Core.Domain
```

The domain must not depend on:

* EF Core
* ASP.NET
* databases
* filesystems
* Git
* GitHub
* Docker
* MCP
* HTTP
* queues
* message brokers
* LLM SDKs
* operating-system APIs
* environment variables

---

# 2. Inspect Existing Implementation First

Before making changes:

1. Inspect the repository.
2. Inspect FOUNDATION-002.
3. Inspect FOUNDATION-003.
4. Inspect FOUNDATION-004.
5. Inspect all existing Task domain tests.
6. Identify the existing implementations of:

   * TaskId
   * TaskRevision
   * Task
   * TaskState
   * lifecycle transition APIs
7. Preserve existing semantics where possible.
8. Do not create duplicate identity types.

Adapt this specification to the existing implementation.

---

# 3. Problem Being Solved

The future autonomous system will perform operations such as:

```text
Task created
Task claimed
Task started
Agent executes work
Tool invoked
Tool result returned
Verification performed
Task retried
Task recovered
Task resumed
```

The same operation may potentially be delivered more than once.

Examples:

```text
claim request arrives twice

retry request arrives twice

agent completion arrives twice

tool result arrives twice

recovery operation is repeated

application process crashes and retries the same operation
```

The domain must therefore have clear identity semantics.

The system must be able to distinguish:

```text
Which Task?
Which Task revision?
Which logical operation?
```

These are different concepts.

---

# 4. Task Identity

`TaskId` identifies the logical Task itself.

The following must remain true:

```text
Same TaskId = same logical Task
Different TaskId = different logical Task
```

Task identity must not change during the lifetime of a Task.

A lifecycle transition must never create a new Task identity.

For example:

```text
PROPOSED
    TaskId = A

READY
    TaskId = A

CLAIMED
    TaskId = A

IN_PROGRESS
    TaskId = A

VERIFICATION
    TaskId = A

ACCEPTED
    TaskId = A
```

The Task identity remains:

```text
A
```

throughout.

---

# 5. TaskId Requirements

Inspect the existing `TaskId` implementation.

If it already satisfies the requirements below, preserve it.

`TaskId` must:

* be strongly typed
* be immutable
* reject invalid values
* support equality
* support deterministic hashing
* have an unambiguous representation
* not expose mutable internal state

Do not use raw strings throughout the domain where `TaskId` is expected.

Do not replace it with:

```csharp
string
Guid
int
long
```

at call sites merely for convenience.

Use the existing value object.

---

# 6. Task Revision

`TaskRevision` identifies a specific version/revision of a Task aggregate.

Conceptually:

```text
TaskId = A
Revision = 1

TaskId = A
Revision = 2

TaskId = A
Revision = 3
```

These are all the same logical Task but different aggregate revisions.

The existing FOUNDATION-003/004 revision behavior must be preserved.

A successful state-changing operation should advance the revision according to the established domain semantics.

A rejected operation must not advance it.

---

# 7. Revision Semantics

The revision must satisfy:

```text
Revision(N + 1) > Revision(N)
```

for successful mutations.

Do not permit:

```text
revision decreases
revision becomes negative
revision arbitrarily jumps
```

unless the existing FOUNDATION-003 design explicitly established a different invariant.

Do not introduce distributed versioning or vector clocks.

That belongs to later infrastructure work if ever required.

---

# 8. Operation Identity

Introduce a domain concept for identifying a logical operation if one does not already exist.

Use an explicit strongly typed value object.

A conceptual name is:

```text
OperationId
```

or an equivalent name consistent with the existing architecture.

The important distinction is:

```text
TaskId
```

identifies the Task.

```text
TaskRevision
```

identifies the Task version.

```text
OperationId
```

identifies a logical operation/request.

Do not collapse these concepts into one identifier.

---

# 9. OperationId Requirements

`OperationId` must:

* be strongly typed
* be immutable
* support equality
* support deterministic hashing
* reject invalid values
* have an unambiguous representation

It must not be a mutable object.

It must not contain execution state.

It must not contain authorization information.

It must not contain agent identity.

It must not contain tool identity.

Those belong to later domain/application concepts.

---

# 10. Correlation Identity

Where the existing architecture does not already provide equivalent semantics, introduce a domain concept for correlation.

A conceptual name is:

```text
CorrelationId
```

Its purpose is to connect related operations belonging to the same logical workflow.

For example:

```text
CorrelationId = C1

OperationId = O1
TaskId = T1
    ↓
OperationId = O2
TaskId = T1
    ↓
OperationId = O3
TaskId = T1
```

All may belong to:

```text
CorrelationId = C1
```

The important distinction is:

```text
TaskId
    = What Task?

OperationId
    = Which logical operation?

CorrelationId
    = Which larger workflow/correlation?
```

Do not automatically make these identifiers equal.

---

# 11. Do Not Over-Model Identity

Do not create identity types merely because they sound useful.

Only introduce:

* `TaskId`
* `TaskRevision`
* `OperationId`
* `CorrelationId`

if equivalent concepts do not already exist.

Do not add:

```text
AgentId
ExecutionId
AttemptId
ToolInvocationId
RunId
WorkflowId
SessionId
RequestId
MessageId
```

unless the existing architecture already requires them.

Those concepts can be introduced by later FOUNDATION tasks when their semantics are actually defined.

---

# 12. Idempotency Concept

FOUNDATION-005 must establish the domain semantics necessary to recognize repeated operations.

The key invariant is:

> Repeating the same logical operation against the same Task must not accidentally produce an additional state mutation.

For example:

```text
Operation O1
Task T1
```

is processed successfully.

If the same:

```text
Operation O1
Task T1
```

is submitted again, the domain/application boundary must be able to distinguish it from a new operation.

---

# 13. Important Boundary

Do NOT implement a persistence-backed idempotency store.

Do not create:

```text
IdempotencyDatabase
ProcessedOperationsTable
Redis cache
distributed lock
message queue
```

Those are infrastructure concerns.

FOUNDATION-005 establishes the identity semantics that make idempotency possible.

Actual durable deduplication belongs to a later Application/Infrastructure task.

---

# 14. Idempotency Semantics

Introduce a domain-level result/concept where appropriate so that a repeated operation can be distinguished from:

```text
successful new operation
invalid operation
```

The conceptual result categories are:

```text
Applied
AlreadyApplied
Rejected
```

Adapt this to the existing Result/Error conventions.

Do not force this exact type if FOUNDATION-003/004 already established an appropriate result abstraction.

---

# 15. No Duplicate Mutation

The following must never happen:

```text
Operation O1
    → Task revision 4 → revision 5

same Operation O1
    → Task revision 5 → revision 6
```

if the second invocation represents the same logical operation.

Instead, the second invocation must be recognized as already applied.

Conceptually:

```text
O1 → Applied
O1 → AlreadyApplied
```

The exact persistence mechanism is explicitly out of scope.

---

# 16. Operation Scope

An operation identity must have a clear scope.

At minimum, an operation must be associated with the Task it operates upon.

Do not assume that:

```text
OperationId = globally unique
```

is sufficient by itself if the existing domain design establishes a scoped identity.

The domain must be able to establish:

```text
OperationId belongs to TaskId
```

where required.

Do not allow an operation intended for Task A to be silently interpreted as an operation for Task B.

---

# 17. Task/Operation Association

If the implementation introduces an operation command/value object, it should conceptually contain:

```text
TaskId
OperationId
```

and, where required:

```text
CorrelationId
ExpectedRevision
```

However:

**Do not automatically add every field.**

Only add fields whose semantics can be justified by the existing domain model.

The goal is strong identity boundaries without speculative modeling.

---

# 18. Expected Revision / Optimistic Concurrency

If FOUNDATION-003 already established revision-aware mutation semantics, preserve them.

If not already implemented, introduce the minimal domain concept necessary to express:

> Apply this operation only if the Task is still at the expected revision.

Conceptually:

```text
TaskId = T1
ExpectedRevision = 5
```

If the Task is currently:

```text
Revision = 5
```

the operation may proceed.

If the Task is:

```text
Revision = 6
```

the operation must not silently overwrite it.

This is a domain invariant that protects against stale operations.

---

# 19. Stale Operation Behavior

A stale operation must fail deterministically.

Example:

```text
Current Task revision = 6

Incoming operation:
TaskId = T1
ExpectedRevision = 5
```

The operation must not:

* overwrite state
* decrement revision
* create a conflicting mutation
* silently retry
* automatically merge changes

It should return the established domain conflict/error representation.

Do not implement retry behavior.

---

# 20. Interaction With FOUNDATION-004

FOUNDATION-004 defines:

```text
Can transition from state X to state Y?
```

FOUNDATION-005 adds identity semantics around the operation performing the transition.

Conceptually:

```text
Operation
    ↓
Task identity check
    ↓
Revision/concurrency check
    ↓
Lifecycle transition validation
    ↓
Mutation
    ↓
New revision
```

Do not implement an Application Service here.

The ordering is conceptual domain behavior only.

---

# 21. State Transition + Idempotency

A successful lifecycle operation should conceptually produce:

```text
TaskId
OperationId
PreviousRevision
NewRevision
PreviousState
NewState
```

Do not necessarily create a full event object yet.

The purpose is to make the semantics clear and testable.

For example:

```text
Task T1
Revision 4
IN_PROGRESS

Operation O7 requests:
IN_PROGRESS → VERIFICATION

Successful result:

Task T1
Revision 5
VERIFICATION
```

Repeating O7 must not create:

```text
Revision 6
VERIFICATION
```

---

# 22. Idempotency vs Invalid Repetition

Distinguish these cases.

### Case A — Same operation repeated

```text
O1:
IN_PROGRESS → VERIFICATION

O1 again:
IN_PROGRESS → VERIFICATION
```

The second invocation should be recognized as already applied.

---

### Case B — Different operation requesting the same transition

```text
O1:
IN_PROGRESS → VERIFICATION

O2:
IN_PROGRESS → VERIFICATION
```

This is NOT automatically the same operation.

Do not equate operations merely because their requested transition is identical.

Whether O2 is valid depends on the current Task state and revision.

---

### Case C — Same OperationId against another Task

```text
O1 → Task A
O1 → Task B
```

This must not silently succeed as two unrelated operations.

The implementation must enforce the chosen OperationId scope.

---

# 23. Do Not Hide Idempotency in State

Do not implement idempotency using only:

```text
if currentState == targetState
```

That is incorrect.

Example:

```text
Operation O1:
IN_PROGRESS → VERIFICATION

Operation O2:
VERIFICATION → VERIFICATION
```

These are not necessarily equivalent.

Idempotency is about operation identity, not merely current state.

---

# 24. Tests — Identity Value Objects

Add tests for:

### TaskId

* valid construction
* invalid construction
* equality
* inequality
* deterministic hash behavior
* immutability

### TaskRevision

* valid construction
* invalid construction
* equality
* ordering if already supported
* increment semantics
* invalid decrement behavior

### OperationId

* valid construction
* invalid construction
* equality
* inequality
* deterministic hash behavior
* immutability

### CorrelationId

If introduced:

* valid construction
* invalid construction
* equality
* inequality
* deterministic hash behavior
* immutability

---

# 25. Tests — Task Identity Preservation

Verify that lifecycle transitions preserve TaskId.

Example:

```text
PROPOSED → READY
```

must preserve:

```text
TaskId
```

Continue testing across the complete lifecycle.

Task identity must never change.

---

# 26. Tests — Revision

Verify:

```text
successful mutation → revision advances
```

and:

```text
rejected mutation → revision unchanged
```

Also verify:

```text
stale operation → revision unchanged
```

---

# 27. Tests — Operation Identity

Verify that:

```text
O1 != O2
```

even when both operate on the same Task.

Verify that two independently created identifiers are not accidentally equal.

Do not make equality depend on object reference.

---

# 28. Tests — Idempotency

Test:

```text
O1 → successful mutation
O1 → AlreadyApplied
```

The second invocation must not mutate the Task.

Verify:

* state unchanged
* revision unchanged
* TaskId unchanged
* timestamp unchanged where applicable

---

# 29. Tests — Different Operations

Verify:

```text
O1
O2
```

are independent logical operations.

For example:

```text
O1 → IN_PROGRESS → VERIFICATION
O2 → VERIFICATION → ACCEPTED
```

should be distinguishable.

Do not accidentally treat O2 as a duplicate merely because O1 changed the state.

---

# 30. Tests — Cross-Task Safety

Verify that an operation associated with:

```text
Task A
```

cannot silently mutate:

```text
Task B
```

The domain must reject a mismatched association.

---

# 31. Tests — Stale Revision

Explicitly test:

```text
Task revision = 5

Operation expects revision = 4
```

The operation must be rejected.

Verify:

* state unchanged
* revision unchanged
* no partial mutation

Also test the valid case:

```text
Task revision = 5

Operation expects revision = 5
```

which may proceed if the lifecycle transition itself is legal.

---

# 32. Exhaustive Combination Testing

Where practical, test the combination:

```text
Task state
+
target state
+
operation identity
+
expected revision
```

At minimum establish that identity/concurrency checks do not bypass FOUNDATION-004 lifecycle rules.

For example:

```text
IN_PROGRESS
Operation O1
ExpectedRevision = current
Target = ACCEPTED
```

must still fail because:

```text
IN_PROGRESS → ACCEPTED
```

is illegal.

Identity must never bypass lifecycle validation.

---

# 33. Security / Trust Boundary

This work is foundational to the later autonomous security model.

The following must remain impossible through public domain APIs:

```text
untrusted content
    ↓
arbitrary TaskId mutation

untrusted content
    ↓
arbitrary revision mutation

untrusted content
    ↓
arbitrary lifecycle state mutation

LLM output
    ↓
direct Task mutation

tool result
    ↓
direct Task mutation
```

The future application layer will mediate these operations.

The domain must remain authoritative over its own invariants.

---

# 34. Determinism

Given identical:

```text
Task state
Task revision
Task identity
Operation identity
requested transition
```

the domain must produce the same result.

Do not use:

* randomness
* current time for decision-making
* network state
* external services
* environment state

except where the existing Task model requires a timestamp for mutation bookkeeping.

Timestamp generation must not influence whether a transition is valid.

---

# 35. No Persistence

Do not implement:

* repositories
* database tables
* EF configurations
* caches
* Redis
* files
* JSON persistence
* event stores

FOUNDATION-005 defines semantics.

A later Application/Infrastructure foundation will provide durable storage and deduplication.

---

# 36. No Event Sourcing

Do not convert the Task aggregate into an event-sourced aggregate.

Do not introduce:

```text
TaskCreatedEvent
TaskTransitionedEvent
TaskAcceptedEvent
```

unless the existing implementation already uses domain events.

If domain events already exist, preserve compatibility but do not expand the event architecture as part of this task.

---

# 37. No Distributed Locking

Do not implement:

* mutexes
* distributed locks
* database locks
* Redis locks
* lease systems

Those belong to infrastructure.

The domain only establishes the identity and revision rules that allow those mechanisms to be built safely later.

---

# 38. API Design

Keep public APIs minimal.

Prefer:

```text
strongly typed identifiers
+
explicit domain operations
+
explicit results
```

over:

```text
generic dictionaries
+
strings
+
reflection
+
dynamic objects
```

Do not expose internal mutable collections.

Do not expose internal transition registries.

---

# 39. Documentation

Update relevant domain documentation if present.

Document:

* TaskId semantics
* TaskRevision semantics
* OperationId semantics
* CorrelationId semantics if introduced
* optimistic revision semantics
* idempotency semantics
* distinction between Task identity and operation identity

Keep the documentation concise.

---

# 40. Scope Restrictions

FOUNDATION-005 must NOT implement:

* Agent Runtime
* Orchestrator
* scheduler
* queue
* message broker
* Tool Gateway
* MCP
* LLM integration
* persistence
* repository implementations
* database
* distributed locking
* retry engine
* recovery engine
* event sourcing
* API endpoints
* UI
* Git integration
* Docker integration
* GMod integration

Those remain future work.

---

# 41. Required Workflow

Before implementation:

1. Inspect the current repository.
2. Inspect FOUNDATION-002.
3. Inspect FOUNDATION-003.
4. Inspect FOUNDATION-004.
5. Inspect all Task domain tests.
6. Identify existing identity/revision/result abstractions.
7. Avoid duplicating existing concepts.

Then:

1. Implement missing identity concepts.
2. Integrate operation identity with Task lifecycle operations where appropriate.
3. Preserve existing lifecycle rules.
4. Add revision/concurrency protection if not already present.
5. Establish idempotency semantics.
6. Add exhaustive tests.
7. Run all tests.
8. Run the complete build.
9. Verify Core dependency isolation.
10. Verify no unrelated modifications.

---

# 42. Definition of Done

FOUNDATION-005 is complete when:

## Identity

* [ ] TaskId is strongly typed and immutable.
* [ ] TaskRevision is strongly typed and immutable.
* [ ] OperationId exists if not already present.
* [ ] CorrelationId exists only if justified by the existing architecture.
* [ ] Identity concepts have deterministic equality.
* [ ] Invalid identity values are rejected.
* [ ] Identity concepts cannot be externally mutated.

## Task

* [ ] TaskId remains stable throughout lifecycle transitions.
* [ ] Revision changes only on successful mutation.
* [ ] Revision does not change on rejected operations.
* [ ] Stale revisions are rejected.
* [ ] Operations cannot silently target the wrong Task.

## Idempotency

* [ ] Logical operations have explicit identity.
* [ ] Repeating the same operation is distinguishable from a new operation.
* [ ] Repeated operations do not cause duplicate mutation.
* [ ] Different operations are not incorrectly treated as duplicates.
* [ ] Idempotency does not rely solely on current state.

## Lifecycle

* [ ] FOUNDATION-004 transition rules remain authoritative.
* [ ] Identity checks cannot bypass lifecycle validation.
* [ ] ACCEPTED still requires VERIFICATION.
* [ ] Terminal states remain terminal.

## Architecture

* [ ] Core remains independent.
* [ ] No persistence was introduced.
* [ ] No Infrastructure dependency was introduced.
* [ ] No Application dependency was introduced.
* [ ] No external services were introduced.
* [ ] No distributed coordination was introduced.

## Tests

* [ ] Identity tests pass.
* [ ] Revision tests pass.
* [ ] Idempotency tests pass.
* [ ] Stale revision tests pass.
* [ ] Cross-Task safety tests pass.
* [ ] Existing FOUNDATION-002/003/004 tests still pass.
* [ ] Complete test suite passes.
* [ ] Build succeeds.

---

# 43. Required Final Report

After implementation, provide:

## Implemented

List all files/classes created or modified.

## Identity

Explain the implemented:

* TaskId
* TaskRevision
* OperationId
* CorrelationId, if applicable

## Idempotency

Explain how repeated operations are represented and distinguished.

## Concurrency

Explain how revision checks work.

## Tests

Report:

* total test count
* new test count
* passing/failing count
* build result
* warnings/errors

## Architecture

Confirm that:

* Core remains isolated
* no persistence was introduced
* no infrastructure dependencies were introduced
* no orchestration was introduced

## Deviations

List every deviation from this specification and explain why.

## Remaining Work

List only work that belongs to future FOUNDATION tasks.

Do not implement future work as part of FOUNDATION-005.
