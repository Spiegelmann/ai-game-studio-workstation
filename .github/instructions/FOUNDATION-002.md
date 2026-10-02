# FOUNDATION-002 — Core Domain Primitives

## Objective

Establish the minimal domain foundation in the `Core` project for the AI Game Studio Workstation.

This task follows `FOUNDATION-001`.

The purpose is to introduce the first stable domain concepts and domain invariants without implementing persistence, infrastructure, tools, process execution, Git adapters, LLM integration, policy evaluation, orchestration, or autonomous execution.

The Core project must remain independent of Infrastructure, external services, frameworks, and execution mechanisms.

---

## Starting assumptions

The repository already contains:

* A root .NET solution.
* `Core`
* `Application`
* `Infrastructure`
* `Host`
* Corresponding test projects.
* The dependency direction:

```text
Core
 ↑
Application
 ↑
Infrastructure
 ↑
Host
```

`FOUNDATION-001` established the architectural skeleton.

Do not recreate or restructure that work unless something is objectively incorrect.

---

# 1. Scope

Implement only the following domain foundation:

### Domain concepts

Introduce minimal representations for:

* Task
* TaskState
* TaskId
* TaskRevision
* TaskScope
* AcceptanceCriterion
* StopCondition
* ChangeBudget

Also introduce the minimum supporting value objects/enums required to make these concepts meaningful and testable.

The implementation should be deliberately small.

Do not implement the full autonomous runtime yet.

---

# 2. Task identity

Create a strongly typed `TaskId`.

Requirements:

* Must represent a task identifier.
* Must not rely on raw `string` throughout the domain.
* Must support equality.
* Must have deterministic string representation.
* Must reject invalid/empty identifiers.
* Must not generate IDs implicitly as a side effect of constructing a Task.
* Do not introduce database-specific identity behavior.

A simple immutable value object/record is preferred.

Do not introduce GUID/database assumptions unless there is a compelling domain reason.

---

# 3. Task revision

Create a strongly typed `TaskRevision`.

Requirements:

* Represents the revision/version of a task definition.
* Must be immutable.
* Must not accept negative values.
* Must have deterministic equality.
* The initial revision should be representable explicitly.

Do not implement persistence or automatic revision management yet.

---

# 4. Task states

Implement the currently defined task states:

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

Use an appropriate domain representation.

The state itself should not contain orchestration logic.

However, the domain must establish valid state-transition rules.

---

# 5. Task state transitions

Implement a domain-level mechanism for validating task state transitions.

The following principles must be enforced:

### General

* Invalid transitions must be rejected.
* A task must not silently change state.
* State transitions must be explicit.
* Terminal states must not silently mutate.

### Terminal states

The following are terminal:

```text
ACCEPTED
FAILED
CANCELLED
```

A terminal task cannot transition to another state through the normal transition mechanism.

### Important rules

At minimum enforce:

```text
PROPOSED → READY
PROPOSED → BLOCKED
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
IN_PROGRESS → HUMAN_REVIEW_REQUIRED

VERIFICATION → ACCEPTED
VERIFICATION → FAILED
VERIFICATION → BLOCKED
VERIFICATION → HUMAN_REVIEW_REQUIRED
VERIFICATION → RECOVERING

RECOVERING → CLAIMED
RECOVERING → IN_PROGRESS
RECOVERING → HUMAN_REVIEW_REQUIRED
RECOVERING → FAILED

BLOCKED → READY
BLOCKED → CANCELLED
BLOCKED → HUMAN_REVIEW_REQUIRED

HUMAN_REVIEW_REQUIRED → READY
HUMAN_REVIEW_REQUIRED → CANCELLED
HUMAN_REVIEW_REQUIRED → BLOCKED
```

If the implementation requires a slightly different representation, preserve these semantics.

Do not add speculative transitions simply because they may become useful later.

Do not implement recovery policy here.

This task only establishes whether a requested transition is structurally valid.

---

# 6. Task scope

Create a minimal `TaskScope` value object.

Its purpose is to describe what part of the repository a task is permitted to affect.

At this stage it should represent scope conceptually, not implement filesystem authorization.

The scope must be immutable.

It should support representing one or more bounded scope entries.

Examples:

```text
src/Core
tests/Core.Tests
docs/contracts
```

Do not:

* access the filesystem
* canonicalize paths
* resolve symlinks
* inspect Git
* authorize writes
* execute commands

Those responsibilities belong to later layers.

The Core domain should only represent the declared scope.

---

# 7. Acceptance criteria

Create a minimal immutable representation of an acceptance criterion.

An acceptance criterion should contain enough information to express:

* identity
* description
* whether it is mandatory

Do not implement evaluation.

The domain should represent the requirement, not determine whether it has passed.

Do not introduce LLM-based evaluation.

Do not introduce verification implementations.

---

# 8. Stop conditions

Create a minimal immutable representation for task stop conditions.

A stop condition should contain:

* identity
* description
* whether it is mandatory

The domain represents the condition.

It does not execute or evaluate the condition.

Do not implement:

* shell checks
* filesystem checks
* process checks
* Git checks
* model evaluation
* runtime monitoring

Those belong elsewhere.

---

# 9. Change budget

Create a minimal `ChangeBudget` value object.

The purpose is to represent explicit limits on task changes.

At minimum support:

* maximum production/source files changed
* maximum test files changed
* maximum total files changed
* maximum dependency additions
* maximum file deletions

Requirements:

* immutable
* values cannot be negative
* zero must be valid
* equality must work deterministically

This object only represents the budget.

It does not count files.

It does not inspect Git.

It does not enforce the budget.

Those responsibilities belong to verification/tooling layers later.

---

# 10. Task aggregate

Create the initial `Task` domain object.

The Task should contain the currently established task concepts:

* TaskId
* intent
* description
* dependencies
* scope
* constraints, where a minimal representation is genuinely required
* required capabilities, only if a minimal domain representation is necessary
* acceptance criteria
* stop conditions
* change budget
* state
* timestamps
* revision

Do not attempt to implement every future property in full detail.

The objective is a stable domain model, not a complete autonomous workflow.

---

# 11. Task invariants

The Task must enforce basic domain invariants.

At minimum:

### Identity

A Task must have a valid `TaskId`.

### Description

A Task must have a meaningful non-empty description.

Avoid arbitrary length limits unless required by a clearly stated domain invariant.

### State

A Task must always have a valid `TaskState`.

### Revision

A Task must always have a valid `TaskRevision`.

### Collections

Dependencies, acceptance criteria, stop conditions, and scope collections must not expose mutable internal collections.

Do not allow callers to mutate the Task's internal state through a returned collection.

### Change budget

A Task must contain a valid `ChangeBudget`.

### State transition

State changes must go through the domain transition mechanism.

Do not expose a public mutable `State` setter.

---

# 12. Dependencies

Represent task dependencies minimally.

A dependency should reference another `TaskId`.

Do not implement dependency resolution.

Do not query other Tasks.

Do not implement scheduling.

Do not implement graph traversal.

The domain only needs to represent:

```text
Task A depends on Task B
```

Dependency resolution belongs to the Application/Orchestrator layer later.

---

# 13. Timestamps

The Task should have creation/update timestamps if the architecture established in previous work requires them.

Do not introduce a framework-specific time abstraction unless necessary.

Do not introduce persistence concerns.

Do not implement automatic persistence of timestamps.

If the implementation needs a time provider to maintain testability, keep the abstraction minimal and domain-appropriate.

Do not introduce a dependency on Infrastructure.

---

# 14. Domain errors

Introduce a small domain-level mechanism for invalid operations where useful.

Examples:

* invalid TaskId
* invalid TaskRevision
* invalid ChangeBudget
* invalid Task construction
* invalid state transition

Do not build a large exception hierarchy.

Do not introduce generic application error handling.

Do not introduce logging frameworks.

Keep domain errors explicit and easy to test.

---

# 15. Tests

Add focused unit tests in `Core.Tests`.

Tests must cover at minimum:

### TaskId

* valid identifier
* empty identifier rejected
* equality
* deterministic representation

### TaskRevision

* valid revision
* negative revision rejected
* equality

### ChangeBudget

* valid values
* zero values
* negative values rejected
* equality

### TaskState

* all defined states exist
* terminal states are identified correctly

### State transitions

Test every defined transition category.

At minimum verify:

* valid transitions are accepted
* invalid transitions are rejected
* terminal states cannot transition
* `ACCEPTED` cannot become `IN_PROGRESS`
* `FAILED` cannot become `READY`
* `CANCELLED` cannot become `READY`

### Task

Test:

* valid construction
* invalid identity rejected
* invalid description rejected
* invalid revision rejected
* immutable state transition behavior
* dependencies represented correctly
* scope represented correctly
* acceptance criteria represented correctly
* stop conditions represented correctly
* change budget represented correctly
* collections cannot mutate internal Task state

Do not write tests for functionality that has not been implemented.

---

# 16. Immutability

Prefer immutable domain structures.

Use:

* records
* readonly members
* immutable collections
* private setters where mutation is genuinely required
* explicit methods for state changes

Do not use public mutable lists for domain collections.

Do not introduce mutable global state.

---

# 17. Architecture constraints

The Core project must remain completely independent of:

* Infrastructure
* Host
* databases
* filesystem
* Git
* process execution
* network
* LLM providers
* AI SDKs
* external APIs
* configuration systems
* secrets
* logging frameworks

The Core project must not perform I/O.

---

# 18. No premature abstractions

Do NOT introduce:

* generic repository interfaces
* generic unit-of-work abstractions
* mediator/CQRS
* event buses
* dependency injection infrastructure
* domain event infrastructure
* message buses
* plugin systems
* reflection-based command systems
* generic `Result<T>` frameworks
* generic `Entity<T>` base classes
* generic `AggregateRoot<T>` frameworks

Only introduce abstractions that are directly required by this task.

---

# 19. Required capabilities are not authorization

If `Task` represents required capabilities, they must only represent the task's declared requirements.

They must NOT:

* grant permission
* invoke tools
* execute anything
* bypass policy
* determine authorization

Authorization will be implemented later.

---

# 20. Security constraints

This task must not introduce any mechanism capable of:

* executing shell commands
* starting processes
* modifying arbitrary files
* accessing arbitrary filesystem paths
* modifying Git
* accessing network resources
* reading secrets
* invoking an LLM
* dynamically loading executable plugins

If Copilot encounters a design decision requiring one of these capabilities, stop and report it rather than implementing it.

---

# 21. Scope restrictions

Modify only:

* `src/Core`
* `tests/Core.Tests`
* the solution/project files when required to register the new files/projects
* minimal documentation if required to accurately describe the new domain foundation

Do not modify:

* Infrastructure implementation
* Host implementation
* Application implementation beyond what is strictly necessary
* CI/CD
* GitHub workflows
* agent definitions
* Copilot instructions
* implementation plan
* security policy
* runtime tooling

Do not create unrelated folders or files.

---

# 22. Dependencies

Do not add NuGet packages unless absolutely necessary.

Prefer the .NET BCL.

If a package appears necessary:

1. Do not add it automatically.
2. Report why it appears necessary.
3. Stop for human review.

For this task, the expected result is **zero new external dependencies**.

---

# 23. Verification

After implementation:

Run:

```text
dotnet build
dotnet test
```

Confirm:

* Core builds successfully.
* Core.Tests builds successfully.
* All tests pass.
* No unrelated projects were broken.
* No new external dependency was introduced.
* No infrastructure dependency was introduced into Core.

Inspect the final Git state.

At minimum:

```text
git status
git diff --stat
git diff
```

Check specifically for:

* unexpected files
* unrelated modifications
* generated files
* dependency changes
* accidental architecture violations

---

# 24. Acceptance criteria

The task is complete only if all of the following are true.

### AC-001 — Domain primitives

`TaskId`, `TaskRevision`, `TaskState`, `TaskScope`, `AcceptanceCriterion`, `StopCondition`, and `ChangeBudget` exist in the Core domain.

### AC-002 — Task

A minimal Task domain object exists and represents the established task concepts.

### AC-003 — State machine

Valid task state transitions are explicitly represented and invalid transitions are rejected.

### AC-004 — Terminal states

`ACCEPTED`, `FAILED`, and `CANCELLED` cannot transition through the normal state-transition mechanism.

### AC-005 — Immutability

Domain value objects and Task collections do not expose uncontrolled mutable state.

### AC-006 — Validation

Invalid domain values are rejected.

### AC-007 — Dependencies

Task dependencies are represented using `TaskId` without implementing dependency resolution.

### AC-008 — Acceptance criteria

Acceptance criteria are represented but not evaluated.

### AC-009 — Stop conditions

Stop conditions are represented but not evaluated.

### AC-010 — Change budget

Change budgets are represented but not enforced by the Core domain.

### AC-011 — Tests

Focused Core tests cover the domain invariants and state transitions.

### AC-012 — Architecture

Core has no dependency on Infrastructure, Host, filesystem, process execution, Git, network, LLMs, secrets, or external services.

### AC-013 — Dependencies

No unnecessary NuGet dependencies were introduced.

### AC-014 — Build

`dotnet build` succeeds.

### AC-015 — Tests

`dotnet test` succeeds.

### AC-016 — Scope

No unrelated files or architectural systems were implemented.

---

# 25. Explicitly out of scope

Do NOT implement:

* Task persistence
* database
* EF Core
* repositories
* task scheduler
* orchestrator
* task dependency resolution
* TaskAttempt
* ToolInvocation
* StateTransition persistence
* EvidenceRecord
* Checkpoint
* RecoveryDecision
* HumanDecision
* capability registry
* capability policy
* tool gateway
* filesystem gateway
* process gateway
* Git gateway
* build adapter
* test adapter
* verification engine
* agent runtime
* LLM provider
* model abstraction
* autonomous execution
* recovery engine
* human approval workflow
* GMod integration
* Blender integration
* Hammer integration
* sound pipeline
* texture pipeline
* multi-agent coordination
* Level 5 artistic evaluation

Those belong to later tasks.

---

# 26. Completion report

When finished, report:

```text
FOUNDATION-002 COMPLETION REPORT

Implemented:
- ...

Domain invariants:
- ...

State transitions:
- ...

Tests:
- dotnet build: PASS/FAIL
- dotnet test: PASS/FAIL

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

Next recommended task:
FOUNDATION-003

STOP.
```

Do not begin `FOUNDATION-003`.

Do not implement additional functionality after the acceptance criteria are satisfied.

The task ends with the completion report.
