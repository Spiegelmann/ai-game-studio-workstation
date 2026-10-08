# PERSISTENCE-002 — Persistence Lifecycle, Schema Evolution & Recovery

## Objective

Implement PERSISTENCE-002 for the AI Game Studio Workstation.

PERSISTENCE-001 established:

* SQLite-backed durable Task state
* Application persistence contract
* Task reconstruction
* operation persistence
* durable idempotency
* optimistic concurrency
* atomic Task + operation mutation
* immutable operation history
* rollback behavior
* corruption detection
* persistence integration tests

PERSISTENCE-002 must now harden the persistence subsystem itself.

The goal is to establish:

* explicit persistence lifecycle semantics
* deterministic schema initialization
* schema versioning
* safe schema evolution foundations
* startup/restart behavior
* database integrity validation
* persistence recovery behavior
* lifecycle ownership boundaries
* deterministic failure behavior

This task must NOT yet wire the persistence system into the Host runtime.

---

# 1. Architectural Context

The architecture remains:

```text
Host
  ↓
Infrastructure
  ↓
Application
  ↓
Core
```

Persistence remains an Infrastructure concern.

Core remains persistence-ignorant.

Application owns persistence contracts where appropriate.

Infrastructure owns:

* SQLite
* schema
* migrations
* connection management
* initialization
* persistence lifecycle
* storage integrity

---

# 2. Inspect Existing Implementation

Before making changes:

1. Inspect PERSISTENCE-001.
2. Inspect FOUNDATION-002 through FOUNDATION-005.
3. Inspect `ITaskRepository`.
4. Inspect `SqliteTaskRepository`.
5. Inspect all persistence models.
6. Inspect schema creation.
7. Inspect existing schema/version handling.
8. Inspect persistence tests.
9. Inspect architecture tests.
10. Inspect existing Infrastructure configuration conventions.

Do not rewrite working PERSISTENCE-001 functionality unnecessarily.

Preserve all passing behavior.

---

# 3. Persistence Lifecycle

Define an explicit persistence lifecycle.

The persistence subsystem must have clearly distinguishable states/concepts such as:

```text
NotInitialized
Initializing
Ready
Failed
Disposed
```

The exact implementation may use a smaller representation if appropriate.

The important invariant is that callers cannot accidentally use an uninitialized persistence subsystem.

---

# 4. Initialization

Persistence initialization must be explicit and deterministic.

Initialization must:

1. Validate the configured database location.
2. Establish the database connection.
3. Ensure required schema exists.
4. Determine schema version.
5. Apply required migrations.
6. Validate resulting schema.
7. Verify database integrity where practical.
8. Transition to a usable state.

If initialization fails:

```text
Ready
```

must NOT be reported.

Do not silently continue with a partially initialized database.

---

# 5. Initialization Idempotency

Calling initialization more than once must be safe.

For example:

```text
Initialize()
Initialize()
Initialize()
```

must not:

* recreate data
* duplicate schema
* duplicate indexes
* duplicate operation records
* reset revisions
* destroy history

The operation must either:

* safely return an already-ready result, or
* explicitly reject repeated initialization according to the chosen lifecycle design.

Choose one deterministic behavior and document it.

---

# 6. Initialization Concurrency

Where multiple callers can initialize the same persistence instance concurrently, initialization must not race.

For example:

```text
Caller A → Initialize
Caller B → Initialize
```

must not result in:

* competing schema creation
* partial migration
* corrupted schema
* duplicate migration application

Use appropriate synchronization.

Do not introduce distributed locks.

This task concerns a local persistence instance.

---

# 7. Schema Versioning

PERSISTENCE-001 currently establishes schema version 1.

PERSISTENCE-002 must introduce explicit schema version tracking.

The database must have an authoritative schema-version representation.

Do not infer schema version from:

* table existence
* column existence
* SQLite metadata
* arbitrary heuristics

Use an explicit schema metadata mechanism.

---

# 8. Schema Metadata

Create a dedicated schema metadata representation.

Conceptually:

```text
SchemaVersion
--------------
Version
AppliedAt
```

The exact schema is implementation-defined.

The metadata must provide enough information to determine:

```text
What schema version is this database?
```

The schema version must be deterministic.

---

# 9. Migration Model

Introduce a migration mechanism suitable for the current SQLite persistence implementation.

A migration should conceptually define:

```text
FromVersion
ToVersion
Migration
```

Migrations must be ordered.

For example:

```text
v0 → v1
v1 → v2
v2 → v3
```

Do not support arbitrary jumps by executing unknown migrations.

The migration system must know the valid upgrade path.

---

# 10. Migration Atomicity

Each migration must be atomic.

If migration:

```text
v1 → v2
```

fails:

* the database must not claim to be v2
* partial schema changes must not remain where the provider supports transactional DDL
* initialization must fail explicitly

Do not mark a migration as applied before it succeeds.

---

# 11. Migration Ordering

Migrations must execute in deterministic order.

For example:

```text
Current = 1
Available = 2, 3, 4

Apply:
1 → 2
2 → 3
3 → 4
```

Do not execute migrations based on:

* filesystem enumeration order
* reflection order
* hash ordering
* nondeterministic collection order

Use explicit ordering.

---

# 12. Unsupported Future Schema

If the database contains a schema version newer than the application understands:

```text
Database = v5
Application supports = v4
```

initialization must fail safely.

Do not downgrade.

Do not delete the database.

Do not attempt automatic repair.

Do not silently ignore the version mismatch.

This is a critical safety invariant.

---

# 13. Downgrade Prohibition

Do not implement automatic schema downgrade.

A database at:

```text
v3
```

must not be automatically transformed into:

```text
v2
```

by an older application.

The system should fail clearly instead.

---

# 14. Migration Failure

If migration fails:

* report the failure explicitly
* preserve the database where possible
* do not silently recreate it
* do not delete user data
* do not fall back to an empty database
* do not continue as Ready

The failure must be recoverable through an explicit administrative/development action.

---

# 15. Existing v1 Database

The PERSISTENCE-002 implementation must support existing PERSISTENCE-001 databases.

An existing valid v1 database must:

```text
open
→ detect v1
→ perform no unnecessary migration
→ validate
→ become Ready
```

No Task data may be lost.

No operation history may be rewritten.

---

# 16. Database Integrity Validation

After initialization/migration, perform appropriate integrity validation.

At minimum verify:

* required tables exist
* required columns exist
* required indexes/constraints exist
* schema version is valid
* database integrity check succeeds where supported

Do not attempt full semantic validation of every Task at startup.

That could become prohibitively expensive later.

---

# 17. Corruption Handling

If database integrity validation fails:

```text
Failed
```

must be the resulting persistence state.

Do not:

* silently rebuild
* delete the database
* reset the schema
* discard Tasks
* discard operation history

The system must fail closed.

---

# 18. Database Path Validation

The persistence layer must validate its configured database path.

Reject clearly invalid configurations such as:

* empty path
* whitespace-only path
* invalid path
* path that resolves to an unsupported object
* directory where a file is expected
* inaccessible location where failure can be detected deterministically

Do not accept database paths originating from Task content.

The database location remains trusted Infrastructure configuration.

---

# 19. Parent Directory

If the chosen architecture permits automatic directory creation, it may create the required parent directory.

However:

* do not create arbitrary directories based on untrusted input
* do not create directories outside the configured persistence boundary
* do not recursively create unexpected paths

Keep filesystem behavior minimal and deterministic.

---

# 20. Connection Lifecycle

Define clear ownership of database connections.

Avoid:

```text
one permanently open connection for the lifetime of the process
```

unless the existing implementation specifically requires it.

Prefer scoped connections/transactions consistent with the repository implementation.

The persistence subsystem must not leak connections.

---

# 21. Resource Disposal

Ensure persistence resources are correctly disposed.

Test:

```text
Initialize
Use
Dispose
```

After disposal, repository operations must not silently continue.

They should fail deterministically.

Do not allow use-after-dispose behavior to corrupt state.

---

# 22. Repository Lifecycle Contract

Update the persistence contract only if necessary.

The Application layer should not need to know:

* SQLite
* connection strings
* migration SQL
* schema tables
* filesystem paths

The Application layer should interact with domain-oriented persistence abstractions.

---

# 23. Separation of Initialization and Repository Operations

A repository must not implicitly create or migrate the database on every operation.

Avoid:

```text
LoadTask()
    → secretly InitializeDatabase()
```

Initialization should be an explicit lifecycle operation.

This makes startup/recovery behavior deterministic.

---

# 24. Use Before Initialization

If repository operations are attempted before initialization:

```text
LoadTask
SaveTask
ApplyTransition
```

they must fail deterministically.

Do not:

* auto-initialize
* create a new database
* silently return empty results

This prevents accidental operation against the wrong storage.

---

# 25. Use After Failed Initialization

If initialization fails, repository operations must remain unavailable.

For example:

```text
Initialize → Failed

LoadTask → rejected
SaveTask → rejected
```

The system must not continue operating against a partially initialized store.

---

# 26. Use After Disposal

After disposal:

```text
LoadTask → rejected
SaveTask → rejected
Initialize → deterministic behavior
```

Choose and document the appropriate lifecycle semantics.

Do not allow undefined behavior.

---

# 27. Existing PERSISTENCE-001 Guarantees

All PERSISTENCE-001 guarantees must remain intact.

In particular:

* durable Task state
* TaskId preservation
* Task revision
* operation identity
* operation uniqueness
* idempotency
* optimistic concurrency
* atomic writes
* rollback
* immutable history
* corruption handling

PERSISTENCE-002 must not weaken any of these.

---

# 28. Persistence Recovery

Implement persistence-level recovery behavior only.

This means:

```text
process starts
    ↓
persistence initializes
    ↓
database validated
    ↓
system can safely resume
```

Do NOT implement Task-level recovery.

Do not:

* retry Tasks
* resume agents
* restart tools
* restore checkpoints
* requeue work

Those belong to the later Reliability/Execution layers.

---

# 29. Crash Recovery Test

Add integration tests covering:

```text
valid database
→ process/context terminates
→ new persistence instance
→ initialization
→ Task remains available
```

Verify:

* Task state
* revision
* operation history
* idempotency
* schema version

remain correct.

---

# 30. Interrupted Migration Test

Create a deterministic test for migration failure.

The test should demonstrate that a failed migration:

* does not claim the target schema version
* does not silently continue
* does not delete existing Task data
* leaves the failure explicit

Use a test-only migration/failure mechanism.

Do not intentionally corrupt the production database.

---

# 31. Schema Version Tests

Test:

### Current version

```text
database = supported current version
```

Initialization succeeds.

### Older version

```text
database = supported older version
```

Initialization applies migrations and succeeds.

### Newer version

```text
database = unsupported future version
```

Initialization fails safely.

### Invalid version

```text
database = malformed metadata
```

Initialization fails safely.

---

# 32. Migration Idempotency

Running initialization repeatedly must not reapply completed migrations.

For example:

```text
Initialize
→ v2

Initialize
→ remains v2
```

Do not execute:

```text
v1 → v2
```

again.

---

# 33. Concurrent Initialization Test

Test multiple initialization attempts against the same persistence instance.

Verify:

* exactly one effective initialization
* no duplicate schema
* no duplicate migration
* final Ready state is valid

Where appropriate, also test multiple persistence instances targeting the same database.

Do not introduce distributed locking merely to satisfy this test.

SQLite's own locking/transaction behavior may be used where appropriate.

---

# 34. Multiple Repository Instances

Test:

```text
Repository A → database
Repository B → same database
```

Both must observe the same durable state.

This is important because the persistence abstraction must not accidentally hide process-local state.

---

# 35. No In-Memory Source of Truth

Do not maintain an independent in-memory Task store that becomes the authoritative state.

Memory may be used temporarily for:

* domain reconstruction
* request processing
* caching where explicitly justified later

but SQLite remains authoritative for durable Task state.

Do not add caching as part of this task.

---

# 36. Schema Documentation

Document:

* current schema version
* migration mechanism
* migration ordering
* unsupported future schema behavior
* initialization lifecycle
* database integrity behavior
* disposal behavior

Keep it concise.

---

# 37. Test Isolation

Persistence tests must continue to use isolated test databases.

Do not allow schema migrations to modify:

* developer databases
* committed databases
* shared test databases

Each test suite should be deterministic.

---

# 38. Security

Persistence lifecycle must fail closed.

Never:

* downgrade unknown databases
* delete databases automatically
* recreate corrupt databases automatically
* execute arbitrary migration text from database contents
* accept schema instructions from Task data

Migration definitions are trusted application code.

Database contents are data, not instructions.

---

# 39. No Host Wiring

Do NOT modify the Host to instantiate the repository as the normal application runtime.

The previous report explicitly identified:

> The host does not instantiate the repository yet.

That is acceptable and should remain so for this task.

Host composition belongs to the later application/runtime integration stage.

PERSISTENCE-002 should make the persistence component ready to be composed later.

---

# 40. No Governance

Do not implement:

* authorization
* capabilities
* policies
* trust
* approval
* human review
* tool permissions

Those belong to GOVERNANCE.

---

# 41. No Execution

Do not implement:

* queues
* scheduling
* agents
* processes
* tools
* MCP
* Docker
* Git
* GMod
* Blender
* Hammer

Those belong to later layers.

---

# 42. No Reliability Engine

Do not implement:

* retry policies
* backoff
* checkpoints
* recovery orchestration
* task resumption
* failure classification

PERSISTENCE-002 only ensures that the persistence layer itself can safely initialize, validate, and recover after process/database lifecycle events.

---

# 43. Required Tests

Add tests covering at least:

### Lifecycle

* use before initialization
* successful initialization
* repeated initialization
* failed initialization
* use after failed initialization
* disposal
* use after disposal

### Schema

* current schema
* older supported schema
* future unsupported schema
* invalid schema version
* migration ordering
* migration atomicity
* migration idempotency

### Integrity

* required schema objects
* database integrity check
* corruption failure behavior

### Recovery

* restart
* persistence reconstruction
* idempotency after restart
* immutable operation history after restart

### Concurrency

* concurrent initialization
* multiple repository instances
* existing optimistic concurrency behavior remains intact

---

# 44. Definition of Done

PERSISTENCE-002 is complete only when:

## Lifecycle

* [ ] Initialization is explicit.
* [ ] Use before initialization fails deterministically.
* [ ] Failed initialization prevents normal use.
* [ ] Disposal is deterministic.
* [ ] Use after disposal fails safely.
* [ ] Initialization is idempotent.

## Schema

* [ ] Schema version is explicitly tracked.
* [ ] Existing v1 databases remain supported.
* [ ] Migration mechanism exists.
* [ ] Migration ordering is deterministic.
* [ ] Migration application is atomic.
* [ ] Unsupported future schema fails safely.
* [ ] Downgrades are not attempted.

## Integrity

* [ ] Schema integrity is validated.
* [ ] Database corruption fails closed.
* [ ] Data is never silently discarded.
* [ ] Database recreation is never automatic after corruption.

## Recovery

* [ ] Valid databases survive process/context restart.
* [ ] Task state survives restart.
* [ ] Operation history survives restart.
* [ ] Idempotency survives restart.
* [ ] Schema version survives restart.

## Concurrency

* [ ] Initialization does not race.
* [ ] Multiple repository instances see durable shared state.
* [ ] Existing optimistic concurrency remains intact.
* [ ] Existing duplicate-operation guarantees remain intact.

## Architecture

* [ ] Core remains persistence-ignorant.
* [ ] Application remains provider-independent.
* [ ] SQLite remains Infrastructure-only.
* [ ] Host is not prematurely wired.
* [ ] No Governance/Execution/Reliability functionality was introduced.

## Quality

* [ ] Complete test suite passes.
* [ ] Build succeeds.
* [ ] No new warnings.
* [ ] `git diff --check` passes.
* [ ] No unrelated files are modified.

---

# 45. Required Final Report

After implementation, provide:

## Implemented

List all files/projects/classes/migrations changed or created.

## Lifecycle

Explain initialization, readiness, failure, and disposal behavior.

## Schema

Report:

* current schema version
* migration mechanism
* migration ordering
* future-schema behavior

## Integrity

Explain database validation and corruption behavior.

## Recovery

Explain restart and persistence recovery behavior.

## Concurrency

Explain initialization and multi-instance behavior.

## Tests

Report:

* total tests
* new tests
* passing/failing count
* migration tests
* corruption tests
* restart tests
* concurrency tests
* build result
* warnings/errors

## Architecture

Confirm:

* Core remains persistence-ignorant
* Application remains provider-independent
* SQLite remains Infrastructure-only
* Host was not prematurely wired

## Deviations

List every deviation from this specification and explain why.

## Remaining Work

List only work belonging to later phases.

Do not implement future work as part of PERSISTENCE-002.
