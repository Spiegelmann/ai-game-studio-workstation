# FOUNDATION-003 — Core Execution & Audit Domain Contracts

## Objective

Extend the Core domain with the remaining foundational domain contracts required by the AI Game Studio Workstation architecture.

This task follows:

* `FOUNDATION-001` — architectural foundation
* `FOUNDATION-002` — task domain primitives

The purpose of this task is to establish the domain vocabulary for:

* Task attempts
* Tool invocations
* State transitions
* Evidence
* Checkpoints
* Recovery decisions
* Human decisions

These concepts must remain pure domain representations.

They must not execute tools, access the filesystem, invoke Git, start processes, call LLMs, persist data, or make infrastructure decisions.

---

# 1. Architectural boundary

All implementation belongs in:

```text
src/Core
tests/Core.Tests
```

Core must remain independent of:

* Infrastructure
* Host
* databases
* filesystem
* Git
* processes
* network
* LLM providers
* AI SDKs
* external APIs
* secrets
* logging frameworks
* dependency injection frameworks

The dependency direction remains:

```text
Core
 ↑
Application
 ↑
Infrastructure
 ↑
Host
```

Do not modify this architecture.

---

# 2. Domain concepts to introduce

Implement the following domain concepts:

```text
TaskAttempt
ToolInvocation
StateTransition
EvidenceRecord
Checkpoint
RecoveryDecision
HumanDecision
```

Also introduce only the minimal supporting value objects/enums required by these concepts.

Do not create generic abstractions for them.

---

# 3. TaskAttempt

Create a `TaskAttempt` domain object.

A Task represents the work itself.

A TaskAttempt represents one concrete execution attempt against that Task.

The distinction is important:

```text
Task
 ├── Attempt 1
 ├── Attempt 2
 └── Attempt 3
```

An attempt must have its own identity.

At minimum represent:

* AttemptId
* TaskId
* attempt number
* state/status
* start timestamp
* completion timestamp where applicable

Use a strongly typed `AttemptId`.

Do not use a database identity.

Do not automatically generate attempts.

The Application/Orchestrator layer will create attempts later.

---

# 4. Attempt state

Create a minimal attempt-state representation.

The exact implementation may use an enum/value object, but it must distinguish at least:

```text
CREATED
RUNNING
COMPLETED
FAILED
CANCELLED
UNKNOWN
```

Important:

`UNKNOWN` must remain distinct from `FAILED`.

An unknown execution outcome means the system cannot safely establish what happened.

It must never be silently converted into success or failure.

Do not implement recovery policy here.

---

# 5. TaskAttempt invariants

Enforce:

* valid AttemptId
* valid TaskId
* positive attempt number
* valid state
* valid start timestamp
* completion timestamp cannot precede start timestamp
* completed/failed/cancelled/unknown attempts cannot silently become running again

Do not assume that every attempt will have a completion timestamp.

An interrupted or unknown attempt may legitimately lack one until the outer system records the outcome.

Do not implement automatic state transitions.

---

# 6. ToolInvocation

Create a domain representation of one tool invocation.

The object represents the fact that an operation was requested/executed.

It does not execute anything.

At minimum represent:

* InvocationId
* TaskId
* AttemptId
* CapabilityId
* invocation state
* start timestamp
* completion timestamp where available
* outcome

Use a strongly typed `InvocationId`.

Use a strongly typed `CapabilityId` rather than a raw string if practical.

Do not implement the capability registry yet.

Do not implement authorization.

Do not implement the Tool Gateway.

---

# 7. Tool invocation state

Represent at least:

```text
REQUESTED
AUTHORIZED
STARTED
COMPLETED
FAILED
CANCELLED
TIMED_OUT
UNKNOWN
```

Do not imply that these states are authorization decisions.

The domain merely represents lifecycle state.

Policy evaluation comes later.

---

# 8. Tool invocation outcome

Create a minimal representation of an invocation outcome.

It must distinguish:

* success
* failure
* unknown

Do not treat an exit code as the universal definition of success.

Do not implement process execution.

Do not introduce stdout/stderr handling here.

Tool-specific normalized evidence belongs to later infrastructure/application layers.

---

# 9. StateTransition

Create a `StateTransition` domain record.

It represents an explicit change of domain state.

At minimum:

* TransitionId
* TaskId
* previous state
* next state
* timestamp
* reason

Use a strongly typed `TransitionId`.

The object should be immutable.

It must not perform the transition itself.

The Task domain remains responsible for validating whether a Task state transition is legal.

This object records the transition.

Do not persist it.

Do not emit events.

Do not introduce an event bus.

---

# 10. StateTransition invariants

Enforce:

* valid TransitionId
* valid TaskId
* valid previous state
* valid next state
* non-empty reason
* valid timestamp

The transition must represent a meaningful state change.

Therefore:

```text
previous state != next state
```

must be enforced.

Do not duplicate the entire Task transition matrix here.

The Task/state-transition mechanism from FOUNDATION-002 remains authoritative for whether a Task transition is legal.

---

# 11. EvidenceRecord

Create an immutable `EvidenceRecord`.

Evidence is one of the central concepts of the system.

The architecture requires:

```text
Agent claim != evidence
```

Evidence must represent an observable result or reference to one.

At minimum include:

* EvidenceId
* TaskId
* optional AttemptId
* evidence type
* source
* summary
* timestamp

Use a strongly typed `EvidenceId`.

Do not store arbitrary executable objects.

Do not execute evidence.

---

# 12. Evidence type

Represent a minimal set of evidence categories.

At minimum:

```text
BUILD
TEST
GIT
SCOPE
ARTIFACT
PROCESS
TOOL
TASK
SYSTEM
```

Do not attempt to enumerate every future verification type.

The design must allow additional evidence categories later without requiring the entire architecture to be redesigned.

---

# 13. Evidence source

Represent where the evidence originated.

At minimum distinguish:

```text
SYSTEM
TOOL
VERIFIER
AGENT
HUMAN
EXTERNAL
```

Important:

The source describes provenance.

It does not describe trust.

Do not introduce logic such as:

```text
AGENT evidence = trusted
```

or:

```text
TOOL evidence = automatically authoritative
```

Trust policy is implemented later.

---

# 14. Evidence trust

Do not implement an evidence trust hierarchy in this task.

The previously established trust model remains:

```text
SYSTEM_TRUSTED
POLICY_TRUSTED
TASK_TRUSTED
PROJECT_DATA
TOOL_OUTPUT
EXTERNAL_DATA
UNTRUSTED
```

Do not collapse provenance and trust into one property.

If trust metadata is required to preserve provenance, represent it minimally and neutrally.

Do not allow an EvidenceRecord to grant authority.

---

# 15. Evidence references

Evidence may refer to an external artifact/result.

If required, introduce a minimal immutable evidence-reference representation.

Examples:

```text
build result
test result
Git revision
diff
artifact
process invocation
tool invocation
verification result
```

Do not create filesystem URLs or infrastructure-specific references.

Do not make references executable.

---

# 16. Checkpoint

Create an immutable `Checkpoint`.

A checkpoint represents a recoverable snapshot of workflow state.

At minimum represent:

* CheckpointId
* TaskId
* AttemptId
* TaskState
* step identifier
* repository revision/reference
* workspace state reference
* last completed invocation reference where available
* active invocation reference where available
* evidence references
* recovery context
* timestamp

Use a strongly typed `CheckpointId`.

The checkpoint must be descriptive.

It does not itself perform recovery.

---

# 17. Checkpoint immutability

Checkpoints must be immutable.

Once created:

* state cannot be changed
* evidence references cannot be mutated
* task identity cannot be changed
* attempt identity cannot be changed

Do not implement checkpoint persistence yet.

Do not overwrite checkpoints.

The future persistence layer will store them as append-only records.

---

# 18. Recovery context

Represent recovery context minimally.

It should be possible for a future recovery engine to understand why a checkpoint exists without relying on mutable process memory.

The context may include:

* failure classification
* reason
* relevant invocation reference
* recovery notes

Do not implement recovery logic.

Do not let an LLM decide recovery inside Core.

---

# 19. Failure classification

Introduce a domain representation of the previously established failure categories.

At minimum:

```text
VALIDATION_FAILURE
POLICY_FAILURE
SCOPE_FAILURE
ENVIRONMENT_FAILURE
TOOL_FAILURE
PROCESS_FAILURE
TIMEOUT
CANCELLATION
BUILD_FAILURE
TEST_FAILURE
DEPENDENCY_FAILURE
STATE_FAILURE
GIT_FAILURE
EXTERNAL_FAILURE
SECURITY_FAILURE
UNKNOWN_FAILURE
```

This classification is descriptive.

It does not determine what the system should do.

---

# 20. RecoveryDecision

Create a `RecoveryDecision` domain record.

It represents a selected recovery action.

At minimum:

* RecoveryDecisionId
* TaskId
* AttemptId
* failure classification
* recovery action
* reason
* timestamp

Use a strongly typed `RecoveryDecisionId`.

---

# 21. Recovery actions

Represent:

```text
RETRY
RESUME
ROLLBACK
RESTART
ESCALATE
ABANDON
```

Do not implement these actions.

The domain object only records which action was selected.

Important:

A recovery decision must not grant additional authority.

The later policy/orchestration layer must revalidate capabilities and scope.

---

# 22. RecoveryDecision invariants

Enforce:

* valid identity
* valid TaskId
* valid AttemptId
* valid failure classification
* valid recovery action
* non-empty reason
* valid timestamp

Do not allow arbitrary recovery-action strings.

---

# 23. HumanDecision

Create a `HumanDecision` domain record.

This represents an explicit human decision or approval.

At minimum:

* HumanDecisionId
* TaskId
* decision type
* decision
* reason
* timestamp

Use a strongly typed `HumanDecisionId`.

Do not represent a real user/account identity yet.

Do not integrate authentication.

Do not integrate authorization.

---

# 24. Human decision types

Represent at least:

```text
APPROVE
REJECT
CANCEL
RETRY
RESUME
AUTHORIZE
DENY
OVERRIDE
```

The system must preserve the fact that the decision was human-originated.

Do not automatically grant permission merely because a decision is represented.

Actual authorization remains a policy-layer responsibility.

---

# 25. Human decision invariants

Enforce:

* valid identity
* valid TaskId
* valid decision type
* valid decision value
* non-empty reason
* valid timestamp

Avoid introducing assumptions about who the human is.

---

# 26. Shared identifiers

Use strongly typed identifiers for:

```text
TaskId
AttemptId
InvocationId
TransitionId
EvidenceId
CheckpointId
RecoveryDecisionId
HumanDecisionId
CapabilityId
```

Do not create one universal:

```text
EntityId
```

or:

```text
GuidId
```

abstraction merely to reduce code.

Each identifier represents a different domain concept.

---

# 27. Relationships

The following relationships should be representable:

```text
Task
 └── TaskAttempt
      ├── ToolInvocation
      ├── Checkpoint
      └── EvidenceRecord

Task
 ├── StateTransition
 ├── EvidenceRecord
 ├── RecoveryDecision
 └── HumanDecision
```

Do not create object graphs that require loading an entire task history into memory.

References should normally be represented through strongly typed identifiers.

---

# 28. No persistence

Do NOT create:

* repositories
* database entities
* EF Core configurations
* migrations
* serialization frameworks
* database schemas
* file persistence
* JSON persistence
* event stores

The Core domain objects must be persistence-agnostic.

---

# 29. No execution

Do NOT create:

* process runners
* shell execution
* filesystem operations
* Git operations
* network operations
* tool implementations
* capability policy
* LLM calls
* agent execution
* orchestrator logic

These concepts merely describe the domain state required by those future systems.

---

# 30. No policy

Do not implement:

* capability authorization
* human approval enforcement
* security policy evaluation
* scope authorization
* trust evaluation
* recovery policy
* scheduling policy

The Core model may represent the inputs and outcomes of those systems, but it must not implement them.

---

# 31. Immutability

Prefer immutable records/value objects.

Collections must be defensively copied and exposed read-only.

No global mutable state.

No mutable singleton state.

No static workflow state.

---

# 32. Domain errors

Use the existing domain error approach from FOUNDATION-002 where possible.

Do not introduce a large exception hierarchy.

Invalid construction should be rejected clearly.

Examples:

* invalid identifier
* invalid attempt number
* invalid state
* invalid timestamp
* empty reason
* invalid transition representation
* invalid recovery action

---

# 33. Tests

Extend `tests/Core.Tests`.

Create focused tests for every new domain concept.

At minimum test:

### TaskAttempt

* valid construction
* invalid identity
* invalid TaskId
* non-positive attempt number
* invalid state
* completion before start rejected
* terminal attempt cannot silently return to running
* unknown outcome remains distinct

### ToolInvocation

* valid construction
* invalid identifiers
* invalid state
* valid lifecycle states
* success/failure/unknown outcomes remain distinct

### StateTransition

* valid construction
* invalid identity
* empty reason rejected
* identical previous/next state rejected
* immutable properties

### EvidenceRecord

* valid construction
* invalid identity
* valid evidence types
* valid provenance/source
* optional AttemptId behavior
* immutable collections/references where applicable

### Checkpoint

* valid construction
* required references
* immutable state
* evidence references cannot be externally mutated

### FailureClassification

* all defined categories exist
* values are distinct

### RecoveryDecision

* valid construction
* invalid values rejected
* all recovery actions represented
* decision does not mutate Task state

### HumanDecision

* valid construction
* invalid values rejected
* all required decision types represented
* decision remains a record of human input rather than authorization logic

### Cross-domain relationships

Verify that:

* TaskAttempt references TaskId.
* ToolInvocation references TaskId and AttemptId.
* Checkpoint references TaskId and AttemptId.
* RecoveryDecision references TaskId and AttemptId.
* Evidence can reference TaskId and optionally AttemptId.

---

# 34. Architecture tests

Preserve the architecture tests from FOUNDATION-001/002.

Core must remain independent of:

```text
Infrastructure
Host
Application
```

and external infrastructure.

No test should require:

* filesystem
* Git
* process execution
* network
* database
* LLM
* environment secrets

---

# 35. Dependency restrictions

Do not add NuGet packages.

Use the existing project dependencies and .NET BCL.

If a package appears necessary:

1. Do not install it.
2. Explain why it appears necessary.
3. Stop for human review.

Expected dependency change:

```text
None
```

---

# 36. Scope restrictions

Modify only:

```text
src/Core
tests/Core.Tests
```

and project/solution files only if genuinely required.

Do not modify:

* Infrastructure
* Application implementation
* Host implementation
* CI/CD
* GitHub workflows
* agent definitions
* Copilot instructions
* implementation plan
* policy documents
* runtime tooling

Do not create future subsystem directories such as:

```text
Agents/
Orchestrator/
Tools/
Git/
Process/
GMod/
Blender/
Hammer/
Audio/
Assets/
```

---

# 37. No overengineering

Do not introduce:

* generic domain frameworks
* aggregate base classes
* generic repositories
* domain event frameworks
* event sourcing
* CQRS
* mediator
* messaging
* serialization infrastructure
* dependency injection
* service locators
* reflection
* dynamic dispatch systems
* plugin architecture

The domain model should remain understandable by a developer opening the repository for the first time.

---

# 38. Verification

Run:

```text
dotnet build
dotnet test
git diff --check
```

Then inspect:

```text
git status
git diff --stat
git diff
```

Remember that untracked files do not appear in normal `git diff`.

Verify the actual contents of newly created files.

Check specifically for:

* unexpected dependencies
* Infrastructure references
* Host references
* Application references
* filesystem access
* process execution
* network access
* persistence
* generic abstractions
* mutable collections
* accidental authorization logic
* accidental recovery logic
* accidental execution logic

---

# 39. Acceptance criteria

### AC-001 — TaskAttempt

A TaskAttempt domain object exists with its own identity and Task reference.

### AC-002 — Attempt state

Attempt lifecycle states include:

```text
CREATED
RUNNING
COMPLETED
FAILED
CANCELLED
UNKNOWN
```

### AC-003 — Unknown outcome

UNKNOWN remains distinct from FAILED and SUCCESS/COMPLETED.

### AC-004 — ToolInvocation

A ToolInvocation domain object exists with Task, Attempt, Capability, lifecycle state, timestamps, and outcome representation.

### AC-005 — StateTransition

State transitions are represented independently from the mechanism that validates Task transitions.

### AC-006 — Evidence

EvidenceRecord exists with identity, Task reference, optional Attempt reference, type, source, summary, and timestamp.

### AC-007 — Provenance

Evidence source/provenance is represented separately from trust/authority.

### AC-008 — Checkpoint

Checkpoint exists and can represent the previously established recovery information.

### AC-009 — Checkpoint immutability

Checkpoints cannot be mutated after construction.

### AC-010 — Failure classification

All established failure categories are represented.

### AC-011 — RecoveryDecision

RecoveryDecision represents the six established recovery actions.

### AC-012 — HumanDecision

HumanDecision represents explicit human decisions without implementing authorization.

### AC-013 — Strong identifiers

Domain concepts use dedicated strongly typed identifiers rather than one generic identifier abstraction.

### AC-014 — Domain purity

Core performs no external I/O.

### AC-015 — No persistence

No persistence implementation was introduced.

### AC-016 — No execution

No tools, processes, Git, filesystem, network, or LLM execution was introduced.

### AC-017 — No policy

No authorization, policy, scheduling, recovery, or trust evaluation was introduced.

### AC-018 — Tests

All new domain concepts have focused unit tests.

### AC-019 — Architecture

Core remains independent of Application, Infrastructure, and Host.

### AC-020 — Dependencies

No new external dependencies were introduced.

### AC-021 — Build

`dotnet build` succeeds.

### AC-022 — Tests

`dotnet test` succeeds.

### AC-023 — Scope

No unrelated files or systems were modified.

---

# 40. Completion report

When complete, report exactly in this general structure:

```text
FOUNDATION-003 COMPLETION REPORT

Task:
FOUNDATION-003 — Core Execution & Audit Domain Contracts

Implemented:
- ...

Domain concepts:
- TaskAttempt
- ToolInvocation
- StateTransition
- EvidenceRecord
- Checkpoint
- RecoveryDecision
- HumanDecision

Invariants:
- ...

Tests:
- dotnet build: PASS/FAIL
- dotnet test: PASS/FAIL
- git diff --check: PASS/FAIL

Dependencies added:
- None
OR
- <dependency + justification>

Git status:
- CLEAN/DIRTY
- <summary>

Out-of-scope changes:
- None
OR
- <details>

Architecture concerns:
- None
OR
- <details>

Acceptance:
- AC-001: PASS/FAIL
- ...
- AC-023: PASS/FAIL

Next recommended task:
FOUNDATION-004

STOP.
```

Do not begin FOUNDATION-004.

Do not implement persistence, infrastructure, tools, policy, agents, orchestration, or recovery behavior as part of this task.

Stop after the completion report.
