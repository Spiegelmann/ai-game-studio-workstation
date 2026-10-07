# PERSISTENCE-001 — Durable Task State Foundation

## Objective

Implement PERSISTENCE-001 for the AI Game Studio Workstation.

The purpose of this task is to establish the first durable persistence layer for the Task domain.

The system now has:

* Task identity
* Task revisions
* deterministic lifecycle transitions
* OperationId
* CorrelationId
* idempotency semantics
* optimistic concurrency semantics

These semantics currently exist only in memory.

PERSISTENCE-001 must make the Task state durable while preserving those domain guarantees.

This task establishes the persistence foundation for the later:

* Governance layer
* Execution layer
* Reliability layer
* Verification layer
* Agent Runtime
* Orchestration

Do not implement those systems yet.

---

# 1. Architectural Context

The project follows:

* Domain-Driven Design
* Onion Architecture
* Clean Architecture
* explicit dependency direction
* domain-first design
* durable state before autonomous execution
* evidence-first architecture
* immutable historical records
* transactional state changes
* optimistic concurrency
* crash/restart safety

The dependency direction must remain:

```text
Core.Domain
    ↑
Application
    ↑
Infrastructure
    ↑
Host
```

More precisely, dependencies must point inward:

```text
Host
  ↓
Infrastructure
  ↓
Application
  ↓
Core
```

Core must not reference persistence.

Persistence must adapt the domain rather than move persistence concerns into the domain.

---

# 2. Existing Implementation

Before changing anything:

1. Inspect the repository.
2. Inspect FOUNDATION-002 through FOUNDATION-005.
3. Inspect the current Task aggregate.
4. Inspect TaskId.
5. Inspect TaskRevision.
6. Inspect TaskState.
7. Inspect TaskTransitionOperation.
8. Inspect TaskOperationResult.
9. Inspect OperationId.
10. Inspect CorrelationId.
11. Inspect all Task tests.
12. Inspect existing architecture/dependency tests.
13. Inspect the existing solution/project structure.

Do not assume class names or namespaces.

Preserve the existing domain API wherever possible.

Do not rewrite the domain merely to make persistence easier.

---

# 3. Persistence Boundary

Introduce the persistence abstraction outside Core.

The domain must remain persistence-ignorant.

Conceptually:

```text
Core
    Task aggregate
    TaskId
    TaskRevision
    TaskState
    OperationId
    CorrelationId

Application
    persistence-facing abstractions

Infrastructure
    concrete persistence implementation
```

Use the existing architectural conventions if equivalent abstractions already exist.

Do not introduce a second competing repository pattern.

---

# 4. Persistence Technology

Use a relational persistence model.

For the first persistence implementation, use a local relational database suitable for:

* development
* automated tests
* deterministic local execution
* crash/restart testing
* later replacement or extension

Prefer SQLite unless the existing repository already establishes another relational provider.

The persistence abstraction must prevent the domain from becoming coupled to SQLite.

Do not build a distributed database architecture at this stage.

---

# 5. Task Persistence

Persist the complete durable Task state required to reconstruct the Task aggregate.

At minimum, persist:

```text
TaskId
Task state
Task revision
creation timestamp
last update timestamp
```

Persist any additional Task data already established by FOUNDATION-003/004/005.

Do not lose domain information during serialization.

---

# 6. Operation Identity Persistence

The persistence layer must support the idempotency semantics established in FOUNDATION-005.

Persist sufficient information to determine whether an operation has already been applied.

At minimum, the durable representation must associate:

```text
TaskId
OperationId
```

and the resulting operation state/result.

Where required by the existing domain model, preserve:

```text
CorrelationId
expected revision
resulting revision
previous state
resulting state
```

Do not duplicate domain behavior inside persistence.

Persistence stores facts.

The domain decides whether those facts represent valid behavior.

---

# 7. Operation Uniqueness

The persistence model must enforce uniqueness for the appropriate operation identity.

A duplicate operation must not be able to create a second durable mutation.

This must be enforced at the database level where appropriate.

Do not rely exclusively on application code such as:

```text
if (!exists)
{
    insert();
}
```

because concurrent requests can race.

The database must provide the final integrity guarantee.

---

# 8. Optimistic Concurrency

Preserve FOUNDATION-005 revision semantics.

The durable Task representation must support optimistic concurrency.

Conceptually:

```text
Task T1
Revision 5
```

An operation expects:

```text
ExpectedRevision = 5
```

The persistence operation may update:

```text
Revision 5 → Revision 6
```

only if the durable record is still at revision 5.

If another operation has already changed it:

```text
Revision 6
```

the update must fail.

Do not silently overwrite the newer state.

---

# 9. Atomic State Changes

Task state mutation and its corresponding operation/idempotency record must be transactionally consistent.

The following must behave as one atomic unit:

```text
Task state change
+
Task revision change
+
operation record
```

Either:

```text
all succeed
```

or:

```text
none are committed
```

There must not be a durable state where:

```text
Task says revision 6
```

but:

```text
operation record says operation was never applied
```

or vice versa.

---

# 10. Transaction Boundary

The transaction boundary must encompass the complete domain mutation.

Conceptually:

```text
BEGIN TRANSACTION

load Task
load/check operation identity
validate expected revision
perform domain transition
persist new Task revision/state
persist operation result

COMMIT
```

On failure:

```text
ROLLBACK
```

Do not allow partially persisted lifecycle transitions.

The exact implementation should follow the selected persistence technology and existing Application architecture.

---

# 11. Domain Must Remain Authoritative

Do not reimplement FOUNDATION-004 lifecycle rules inside SQL or repository code.

For example, persistence must not independently decide:

```text
IN_PROGRESS → ACCEPTED
```

is illegal.

The domain remains authoritative.

Persistence is responsible for:

* storing state
* retrieving state
* enforcing storage-level integrity
* enforcing uniqueness
* participating in transactions
* enforcing concurrency

The domain is responsible for:

* lifecycle validity
* state transitions
* domain invariants
* operation semantics

---

# 12. Repository Abstraction

Introduce the smallest appropriate persistence abstraction.

Conceptually, the application may need operations equivalent to:

```text
Load Task
Save Task
```

and operation/idempotency lookup.

Do not blindly create a large generic repository such as:

```text
IRepository<T>
IUnitOfWork<T>
IGenericStore<T>
```

unless the existing architecture already requires it.

Prefer domain-specific or application-specific abstractions.

The abstraction should represent actual required use cases rather than database mechanics.

---

# 13. No EF Leakage Into Core

If Entity Framework Core is used:

* EF types must remain outside Core.
* DbContext must remain outside Core.
* entity configurations must remain outside Core.
* persistence attributes must not be added to domain entities merely for convenience.

Do not add:

```csharp
[Key]
[Table]
[Column]
[ConcurrencyCheck]
```

to Core domain objects unless the existing architecture explicitly establishes such an approach.

Prefer Infrastructure mapping.

---

# 14. Persistence Model

Use persistence models separate from the domain aggregate where appropriate.

Conceptually:

```text
Domain:

Task
TaskId
TaskRevision
TaskState

Persistence:

TaskRecord
TaskOperationRecord
```

The exact names are implementation details.

Do not force a relational schema directly onto the domain object.

---

# 15. Task Reconstruction

Loading a Task from persistence must reconstruct a valid domain object.

The system must not:

1. load invalid raw data,
2. bypass domain invariants,
3. expose an invalid aggregate to Application code.

If the persisted representation is invalid, fail explicitly.

Do not silently "repair" corrupted domain state.

---

# 16. Corruption Handling

Persistence corruption must fail closed.

Examples:

```text
invalid TaskId
invalid TaskState
invalid TaskRevision
invalid OperationId
impossible revision
invalid timestamp relationship
```

must not result in the application quietly inventing valid values.

The failure should be explicit and diagnosable.

Do not add automatic destructive repair.

---

# 17. Immutable Historical Records

The architecture requires immutable historical records.

PERSISTENCE-001 must establish the foundation for preserving historical operation facts.

Do not simply overwrite every historical operation record.

At minimum, the operation record must preserve enough information to establish:

```text
OperationId
TaskId
requested transition/result
expected revision
resulting revision
timestamp
```

where supported by the current domain model.

Once written, historical operation records must not be casually updated to rewrite history.

---

# 18. Historical Records vs Current State

Clearly separate:

```text
Current Task State
```

from:

```text
Historical Operation Records
```

The current Task record may be updated as the aggregate evolves.

Historical operation records represent facts that occurred.

Do not confuse:

```text
current state
```

with:

```text
history
```

This distinction is foundational for later:

* verification
* auditing
* recovery
* governance
* evidence
* debugging

---

# 19. No Full Event Sourcing Yet

Do not convert the system into a full event-sourced architecture.

PERSISTENCE-001 requires durable state and immutable historical operation facts.

It does not require:

* event sourcing
* event replay
* event buses
* projections
* CQRS
* distributed event streams

Those may be evaluated later if the architecture requires them.

Do not introduce them speculatively.

---

# 20. Crash Safety

The persistence implementation must be safe against process interruption.

At minimum, design the transaction boundary so that:

```text
process crashes before commit
```

does not leave a partially applied Task mutation.

After restart, the database must represent either:

```text
old valid state
```

or:

```text
new valid state
```

but not an impossible intermediate state.

Add tests that exercise rollback behavior.

---

# 21. Restart Persistence Test

Add an integration test that conceptually performs:

```text
Create Task
Persist Task
Dispose persistence context
Create new persistence context
Load Task
```

and verifies:

* same TaskId
* same state
* same revision
* same relevant timestamps
* same operation/idempotency information

The Task must survive process/context restart.

---

# 22. Idempotency Across Restart

This test is mandatory.

Conceptually:

```text
1. Create Task
2. Execute operation O1
3. Persist
4. Dispose persistence context
5. Create a new persistence context
6. Load Task
7. Execute O1 again
```

The second execution must still be recognized as:

```text
AlreadyApplied
```

It must not mutate the Task again.

This demonstrates that idempotency is no longer merely an in-memory property.

---

# 23. Concurrency Test

Add an integration test covering two competing operations.

Conceptually:

```text
Task revision = 5

Operation A expects revision 5
Operation B expects revision 5
```

Both attempt mutation.

Exactly one must successfully advance the Task.

The other must fail because its expected revision is stale.

The final durable state must be valid.

Do not rely on test timing alone.

Use the persistence provider's actual concurrency behavior.

---

# 24. Duplicate Operation Race

Add a test where the same logical OperationId is attempted concurrently.

Conceptually:

```text
Operation O1
Operation O1
```

Both attempt the same mutation.

The durable system must guarantee that the operation cannot produce two committed mutations.

The final Task revision must advance exactly once.

This is a critical integrity test.

---

# 25. Transaction Rollback Test

Create a test where persistence of a Task mutation cannot complete successfully.

Verify that:

```text
Task state
Task revision
operation record
```

are not partially committed.

The exact mechanism for inducing failure should be deterministic and test-specific.

Do not corrupt the real development database.

---

# 26. Persistence Tests

Create a dedicated persistence/integration test suite.

At minimum test:

### Create

* Task can be persisted.
* Task can be retrieved.
* Identity survives round-trip.

### Update

* Valid transition persists.
* Revision increments correctly.
* State persists correctly.

### Rejection

* Invalid transition is not persisted.
* Revision remains unchanged.

### Idempotency

* Operation record persists.
* Duplicate operation survives restart.
* Duplicate operation does not mutate state.

### Concurrency

* stale revision is rejected.
* competing updates cannot overwrite each other.
* duplicate operations cannot commit twice.

### Transactions

* successful mutation commits atomically.
* failed mutation rolls back.

### Corruption

* invalid persisted state fails explicitly.

---

# 27. Schema Constraints

Where practical, use database constraints to protect invariants that belong to storage integrity.

Examples:

* TaskId uniqueness
* OperationId/TaskId uniqueness
* required fields
* non-null identity fields
* valid revision storage
* foreign-key relationships where appropriate

Do not encode lifecycle transition rules as database constraints.

---

# 28. Migrations / Schema Initialization

Provide a deterministic mechanism for creating the persistence schema.

Use the project's existing migration/initialization conventions.

For development and automated tests, the mechanism must be reproducible.

Do not require a manually edited database.

Do not commit a developer-specific absolute database path.

---

# 29. Database Location

For local development, keep the database location configurable outside the domain.

Do not hard-code:

```text
C:\...
D:\...
```

or any developer-specific filesystem path.

Configuration belongs outside Core.

Tests should be able to use isolated temporary databases.

---

# 30. Test Isolation

Persistence tests must not depend on:

* a developer's existing database
* test execution order
* external services
* network connectivity
* global machine state

Each test or test fixture must have deterministic isolation.

Do not allow one test's Task records to affect another test.

---

# 31. Application Boundary

If an Application project already exists, place persistence abstractions there according to the existing architecture.

If the Application layer does not yet exist or is not ready for this abstraction, establish only the minimum required structure.

Do not prematurely implement application use cases.

The task is persistence foundation, not application orchestration.

---

# 32. Dependency Direction Tests

Extend architecture tests if required.

They must continue to verify:

```text
Core
    cannot depend on Infrastructure

Core
    cannot depend on persistence provider

Application
    cannot depend on Infrastructure implementation details

Infrastructure
    may depend inward
```

No dependency reversal is acceptable merely to simplify persistence.

---

# 33. Security Requirements

Treat persistence as a trust boundary.

Untrusted content must not be able to:

* choose arbitrary database paths
* inject SQL
* manipulate schema
* bypass domain validation
* overwrite unrelated Tasks
* forge operation history
* bypass revision checks

Use parameterized database operations / ORM APIs.

Never construct SQL using untrusted Task content.

---

# 34. No Secrets in Persistence

Do not store:

* API keys
* access tokens
* passwords
* model credentials
* GitHub tokens
* cloud credentials

in Task persistence.

Secrets management is a separate concern.

---

# 35. No Autonomous Execution

PERSISTENCE-001 must NOT execute:

* Lua
* GMod commands
* shell commands
* Git
* Docker
* Blender
* Hammer
* external tools
* LLMs
* agents

Persistence stores state.

It does not perform work.

---

# 36. No Governance Yet

Do not implement:

* permissions
* capability policies
* approval policies
* tool authorization
* trust scoring
* risk scoring
* human approval workflows

Those belong to GOVERNANCE.

The persistence layer should nevertheless preserve enough identity and history for Governance to operate later.

---

# 37. No Execution Yet

Do not implement:

* task scheduling
* agent assignment
* execution queues
* tool calls
* MCP
* process management

Those belong to EXECUTION and later layers.

---

# 38. Evidence-First Preparation

The persistence model must not make future evidence impossible.

Preserve stable:

* Task identity
* operation identity
* revision
* timestamps
* lifecycle state
* correlation identity where present

Do not add the full Evidence model yet.

Evidence will be addressed in the appropriate later stage.

---

# 39. Failure Semantics

Persistence failures must be distinguishable from domain rejections.

At minimum, conceptually distinguish:

```text
Domain rejection
```

from:

```text
Concurrency conflict
```

from:

```text
Persistence failure
```

Do not translate every database failure into:

```text
Invalid Task
```

and do not swallow persistence errors.

Follow existing Result/Error conventions where available.

---

# 40. No Silent Retry

Do not automatically retry database operations in PERSISTENCE-001.

Automatic retry policies belong to the later Reliability layer.

A transient database failure should surface as a persistence failure.

The caller may later decide how it should be handled.

---

# 41. Observability

Do not introduce a full observability system.

Do not add:

* telemetry infrastructure
* distributed tracing
* metrics backend
* logging platform

However, errors must remain diagnosable.

Use existing project logging conventions only where already established.

---

# 42. Documentation

Add concise persistence documentation covering:

1. Persistence boundary.
2. Database technology.
3. Task storage model.
4. Operation/idempotency storage.
5. Revision concurrency.
6. Transaction boundaries.
7. Historical-record semantics.
8. Test strategy.

Do not document future systems as though they already exist.

---

# 43. Scope Restrictions

PERSISTENCE-001 must NOT implement:

* Governance
* Execution
* Reliability
* Verification
* Agent Runtime
* Orchestration
* MVP workflows
* LLM integration
* MCP
* Tool Gateway
* Agent assignment
* Scheduling
* queues
* event sourcing
* CQRS
* distributed databases
* distributed locking
* cloud infrastructure
* GMod integration

Those remain future phases.

---

# 44. Required Workflow

Before implementation:

1. Inspect the current repository.
2. Inspect FOUNDATION-002 through FOUNDATION-005.
3. Inspect current architecture.
4. Identify existing Application/Infrastructure projects.
5. Identify existing persistence dependencies.
6. Identify existing test infrastructure.
7. Determine whether SQLite or another relational provider is already present.
8. Reuse existing conventions wherever possible.

Then:

1. Establish persistence abstractions.
2. Establish the relational persistence implementation.
3. Create Task persistence mapping.
4. Create operation/idempotency persistence mapping.
5. Implement transactional persistence.
6. Implement optimistic concurrency.
7. Implement durable operation uniqueness.
8. Add migration/schema initialization.
9. Add persistence integration tests.
10. Add crash/rollback-oriented tests.
11. Add restart/idempotency tests.
12. Add concurrency tests.
13. Run the complete test suite.
14. Run the complete build.
15. Verify architecture boundaries.

---

# 45. Definition of Done

PERSISTENCE-001 is complete only when:

## Durable State

* [ ] Tasks can be persisted.
* [ ] Tasks can be reconstructed.
* [ ] TaskId survives round-trip.
* [ ] Task state survives round-trip.
* [ ] Task revision survives round-trip.
* [ ] Relevant timestamps survive round-trip.

## Idempotency

* [ ] Operation identity is persisted.
* [ ] Operation uniqueness is enforced.
* [ ] Duplicate operations are recognized after restart.
* [ ] Duplicate operations cannot mutate the Task twice.

## Concurrency

* [ ] Expected revision is respected.
* [ ] stale revisions are rejected.
* [ ] competing mutations cannot silently overwrite each other.
* [ ] successful mutations advance revision exactly once.

## Transactions

* [ ] Task state and operation record are atomic.
* [ ] Failed transactions roll back.
* [ ] Partial mutation cannot remain after failure.

## Historical Integrity

* [ ] Historical operation records are preserved.
* [ ] Historical facts are not silently rewritten.
* [ ] Current Task state remains distinguishable from historical records.

## Architecture

* [ ] Core remains persistence-ignorant.
* [ ] Persistence implementation remains outside Core.
* [ ] Dependency direction remains correct.
* [ ] No forbidden future systems were introduced.

## Testing

* [ ] Persistence round-trip tests pass.
* [ ] Restart tests pass.
* [ ] Idempotency-after-restart tests pass.
* [ ] optimistic concurrency tests pass.
* [ ] duplicate-operation race tests pass.
* [ ] rollback tests pass.
* [ ] corruption handling tests pass.
* [ ] architecture tests pass.
* [ ] complete solution test suite passes.
* [ ] complete solution build succeeds.

---

# 46. Required Final Report

After implementation, provide:

## Implemented

List every project, file, class, migration, and persistence component created or modified.

## Persistence Architecture

Explain:

* persistence provider
* database structure
* repository/application boundary
* domain mapping
* transaction boundary

## Concurrency

Explain exactly how optimistic concurrency is enforced.

## Idempotency

Explain exactly how duplicate operations are prevented, including across restart.

## Historical Records

Explain what is stored and how historical operation facts are protected.

## Transactions

Explain how atomic Task + operation persistence is achieved.

## Tests

Report:

* total tests
* new tests
* passing/failing count
* integration tests
* concurrency tests
* rollback tests
* build result
* warnings/errors

## Architecture

Confirm:

* Core remains persistence-ignorant
* no forbidden dependencies were introduced
* no Governance/Execution/Reliability functionality was implemented

## Deviations

List every deviation from this specification and explain why.

## Remaining Work

List only work belonging to later phases.

Do not implement future work as part of PERSISTENCE-001.
