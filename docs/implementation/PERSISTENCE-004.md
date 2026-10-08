# PERSISTENCE-004 — Durable Evidence Persistence

## Objective

Implement **PERSISTENCE-004 — Durable Evidence Persistence** for the AI Game Studio Workstation.

Continue from the completed:

* FOUNDATION-002 and subsequent Foundation tasks
* PERSISTENCE-001 — Durable Task State Foundation
* PERSISTENCE-002 — Persistence Lifecycle, Schema Evolution & Recovery
* PERSISTENCE-003 — Persistence Continuation

The goal of this phase is to introduce durable, append-oriented persistence for **EvidenceRecord**.

Evidence must be persisted as a first-class historical record rather than being dependent on application logs or transient process state.

The implementation must allow the system to reconstruct the evidence associated with completed or failed work after process restart.

---

# Core objective

The persistence layer must support answering, from durable state:

1. **What happened?**
2. **What evidence supports that it happened?**
3. **Which Task/Attempt/operation produced the evidence?**
4. **When was the evidence produced?**
5. **What was the evidence associated with?**
6. **Can the evidence history be reconstructed after restart?**

Evidence is historical information.

It must therefore be **append-oriented and immutable**.

Do not implement evidence as a mutable "current evidence" field on Task or Attempt.

---

# Mandatory pre-flight

Before changing code:

1. Inspect the repository structure.
2. Read the existing PERSISTENCE-004 specification/documentation if present.
3. Inspect the current Core domain model.
4. Inspect Application persistence contracts.
5. Inspect the complete `SqliteTaskRepository` implementation.
6. Inspect the PERSISTENCE-001, PERSISTENCE-002, and PERSISTENCE-003 documentation.
7. Inspect existing persistence tests.
8. Locate the existing `EvidenceRecord` domain contract, if already present.
9. Determine the exact existing identifiers and relationships available for Task, TaskAttempt, TaskOperation, and StateTransition.
10. Reuse existing contracts rather than creating competing representations.

The existing repository and domain contracts are authoritative.

Do not invent fields that are not supported by the existing domain model.

---

# Scope

PERSISTENCE-004 introduces durable persistence and retrieval for:

* `EvidenceRecord`

The implementation must establish the persistence relationship between evidence and the existing durable execution history.

Where supported by existing domain contracts, evidence should be linkable to:

* Task
* TaskAttempt
* TaskOperation
* relevant invocation/operation identity

Do not fabricate relationships that do not exist in the current domain model.

---

# Evidence semantics

An `EvidenceRecord` represents a durable historical fact or artifact supporting an action, result, decision, validation, or other significant event.

Evidence must remain available independently of transient logs.

Examples of evidence conceptually include:

```text
Task completed
    ↓
Evidence:
    build succeeded
    test suite passed
    generated artifact exists
    validation result recorded
```

The exact evidence fields must come from the existing domain contract.

Do not introduce game-specific evidence semantics at this stage.

---

# Immutability

Evidence records are immutable historical records.

Once persisted:

* they must not be overwritten
* they must not be silently modified
* normal repository operations must not delete them
* later evidence must create additional records
* the original evidence must remain retrievable

For example:

```text
Task T1
│
├── Evidence E1
│   └── Build succeeded
│
├── Evidence E2
│   └── Tests passed
│
└── Evidence E3
    └── Artifact validated
```

All three records remain independently available.

Do not implement evidence as:

```text
Task.CurrentEvidence
```

where each new record replaces the previous one.

---

# Durable retrieval

The repository must provide explicit operations for retrieving evidence.

At minimum, support retrieval by the durable identity/relationship required by the existing domain contract.

The API should allow callers to reconstruct the evidence associated with a Task.

Where the domain already provides Attempt/Operation identifiers, support appropriate scoped retrieval without duplicating domain concepts.

Do not expose SQLite-specific types through Application contracts.

---

# Relationship integrity

Evidence must not become orphaned historical data.

Where an EvidenceRecord references:

* a Task
* an Attempt
* an Operation

the repository must enforce the appropriate referential integrity.

Invalid references must fail deterministically.

Do not silently create missing parent records.

Do not automatically repair orphaned evidence.

---

# Transactional behavior

When evidence is part of a larger atomic persistence operation, it must participate in the same transaction boundary.

In particular, if an operation logically requires:

```text
Task update
+
TaskOperation
+
StateTransition
+
Attempt snapshot
+
Evidence
```

then evidence must not be committed independently if the surrounding domain operation requires all records to succeed together.

A failed transaction must not leave partial evidence behind.

However, do not change existing transaction semantics merely to force every future operation to include EvidenceRecord.

Only extend atomic operations where required by the existing application/domain contract and PERSISTENCE-004 specification.

---

# Idempotency

Evidence persistence must respect the existing idempotency model.

Repeated processing of the same logical operation must not unintentionally create duplicate evidence.

Where the domain provides a stable evidence identifier or operation identifier, enforce the appropriate uniqueness constraint.

Do not invent an arbitrary deduplication algorithm based on evidence content.

Two genuinely distinct evidence records must remain distinct even if their content happens to be identical.

---

# Concurrency

Preserve the concurrency guarantees established by PERSISTENCE-001 through PERSISTENCE-003.

Test relevant concurrent scenarios, including:

* concurrent evidence insertion
* duplicate evidence insertion
* concurrent repository instances
* evidence associated with concurrently changing Tasks/Attempts
* stale revision behavior where applicable

Concurrency must not corrupt evidence history or create invalid relationships.

---

# Schema evolution

Extend the existing schema versioning system.

The current schema is version 3.

Introduce the next ordered schema version required by PERSISTENCE-004.

The migration must:

* be ordered
* be transactional
* preserve all existing Task data
* preserve all existing operation history
* preserve all existing attempt snapshots
* preserve all existing StateTransition history
* preserve existing integrity guarantees
* reject unsupported future versions
* never downgrade automatically
* never delete/recreate a malformed database

Do not rewrite historical data merely to introduce EvidenceRecord persistence.

---

# Integrity

Extend initialization validation to cover the EvidenceRecord schema.

Validate the appropriate:

* table/schema objects
* primary keys
* uniqueness constraints
* foreign keys
* immutable-history protections
* SQLite integrity
* foreign-key integrity

Use the existing PERSISTENCE-002 fail-closed approach.

A malformed or unsupported database must be rejected rather than silently repaired.

---

# Lifecycle

Evidence operations must respect the repository lifecycle already established.

Therefore:

* use before initialization → deterministic failure
* use after failed initialization → deterministic failure
* use after disposal → deterministic failure
* successful reinitialization → existing idempotent behavior
* concurrent initialization → existing serialized behavior

Do not introduce a separate lifecycle model for EvidenceRecord.

---

# Restart and recovery

Evidence must survive process restart.

Test:

```text
Repository instance A
        │
        ├── create Task
        ├── persist attempt/transition
        ├── persist EvidenceRecord
        │
        ▼
     dispose
        │
        ▼
Repository instance B
        │
        └── retrieve Task + historical evidence
```

The complete evidence history must remain available.

No evidence may depend on:

* process memory
* static state
* application logs
* LLM conversation state
* transient execution state

---

# Logs are NOT evidence

Do not implement EvidenceRecord as a wrapper around application logging.

Logs may describe system activity.

Evidence represents durable information that later components can rely upon when reconstructing or validating work.

Do not introduce a logging framework as part of PERSISTENCE-004.

Do not persist arbitrary log output as evidence merely to satisfy this task.

---

# Explicitly OUT OF SCOPE

Do NOT implement persistence for:

* ToolInvocation
* Checkpoint
* RecoveryDecision
* VerificationResult
* HumanDecision

unless an existing domain contract makes a minimal relationship to EvidenceRecord unavoidable.

Do not implement:

* MCP
* tool execution
* agent execution
* orchestration
* autonomous planning
* recovery workers
* retry policies
* governance
* approval workflows
* Host composition
* game-specific validation
* artistic evaluation
* subjective quality judgement

Those belong to later phases.

---

# Tests

Add focused tests for all new behavior.

At minimum, cover:

### Basic persistence

* create EvidenceRecord
* retrieve EvidenceRecord
* retrieve evidence associated with a Task
* retrieve multiple evidence records
* preserve insertion/history ordering where defined

### Immutability

* evidence cannot be silently overwritten
* existing evidence remains available after additional evidence is added
* normal operations cannot mutate historical evidence
* normal operations cannot delete historical evidence

### Relationships

* valid Task relationship succeeds
* invalid Task relationship fails
* valid Attempt relationship succeeds where supported
* invalid Attempt relationship fails where supported
* valid Operation relationship succeeds where supported
* invalid Operation relationship fails where supported

### Idempotency

* repeated logical persistence is safe
* duplicate identifiers behave deterministically
* genuinely distinct evidence records remain distinct

### Transactions

Where EvidenceRecord participates in an atomic operation:

* successful operation commits all required records
* failure rolls back EvidenceRecord
* failure does not leave orphaned evidence

### Concurrency

* concurrent evidence insertion
* duplicate concurrent insertion
* multiple repository instances
* relevant stale-revision behavior

### Migration

* v3 database migrates successfully
* existing Task data survives
* existing operation history survives
* existing attempt history survives
* existing transition history survives
* migration failure rolls back
* future schema versions remain rejected

### Restart

* evidence survives repository disposal
* evidence survives process/repository restart
* complete historical evidence remains retrievable

---

# Architectural constraints

Maintain:

```text
Core
  ↓
Application
  ↓
Infrastructure
```

Core remains:

* persistence-independent
* SQLite-independent
* provider-independent

Application may expose persistence abstractions but must not reference SQLite.

Infrastructure owns:

* SQLite schema
* migrations
* SQL
* database integrity validation
* persistence-specific implementation details

Host remains unwired unless explicitly required by the existing PERSISTENCE-004 specification.

No dependencies should be added unless strictly necessary.

---

# Verification

Run:

```powershell
dotnet test AiGameStudioWorkstation.sln --verbosity minimal
```

Then:

```powershell
dotnet build AiGameStudioWorkstation.sln
```

Then:

```powershell
git diff --check
```

Inspect available diagnostics/problems.

All existing tests must continue to pass.

There must be:

* 0 failed tests
* 0 skipped tests unless an existing test policy explicitly permits them
* 0 build warnings introduced
* 0 build errors
* clean diff check

---

# Final review

Before completion:

1. Inspect the complete diff.
2. Confirm only PERSISTENCE-004-related files changed.
3. Confirm no later-phase functionality was introduced.
4. Confirm EvidenceRecord is immutable.
5. Confirm historical evidence is durable.
6. Confirm relationships are integrity-protected.
7. Confirm migration is transactional.
8. Confirm restart recovery works.
9. Confirm concurrency behavior.
10. Confirm idempotency.
11. Confirm Core/Application boundaries.
12. Confirm SQLite remains Infrastructure-only.

---

# Completion report

Produce a report with:

## Implementation

List files changed and their purpose.

## Behavior

Describe EvidenceRecord persistence and retrieval.

## Immutability

Explain how historical evidence is protected.

## Relationships

Describe Task/Attempt/Operation relationships actually implemented.

## Transactions

Describe atomicity and rollback behavior.

## Schema

Describe the schema version and migration.

## Tests and Verification

Report:

* tests added
* total tests
* failures/skips
* build result
* warnings/errors
* diff check
* diagnostics

## Restart / Recovery

Explain evidence durability across repository restart.

## Architecture

Confirm Core/Application/Infrastructure boundaries.

## Security / Integrity

Describe foreign-key, uniqueness, immutable-history, and fail-closed behavior.

## Scope / Deviations

Explicitly state any deviations from PERSISTENCE-004.

## Acceptance

Provide PASS/FAIL for every acceptance category.

## Remaining Work

Identify the next persistence task and any intentionally deferred functionality.

---

## PERSISTENCE-004 implementation record

The SQLite schema is now version 4. The ordered, transactional `3 → 4` migration adds `EvidenceRecords` without rewriting Tasks, TaskOperations, TaskAttemptSnapshots, or StateTransitions. The migration advances both the authoritative schema metadata and SQLite `user_version` within the existing migration transaction.

`ITaskRepository` now supports appending `EvidenceRecord`, retrieving all evidence for a Task in insertion order, and retrieving evidence scoped to a TaskAttempt. The persisted fields match the existing Core contract; evidence is linked to its Task and, when present, its TaskAttempt. The domain has no OperationId on EvidenceRecord, so no operation relationship is fabricated.

Evidence IDs are unique. Replaying the same ID with identical data is idempotent; reusing it with different data fails. Different IDs remain distinct even when their evidence data is otherwise identical. SQLite uniqueness constraints, restricted Task/TaskAttempt foreign keys, and update/delete triggers protect the durable history. Initialization validates the evidence table, primary/unique keys, foreign keys, and immutable-history triggers and retains the existing fail-closed integrity checks.

Each evidence append is committed atomically. Existing transition operations do not include EvidenceRecord in their Application/Core contracts, so no transition transaction was expanded to write evidence implicitly. Insert failures roll back without leaving evidence behind. Immediate SQLite transactions serialize competing writers; exact concurrent replays produce one record.

The Infrastructure tests cover append/retrieval, insertion ordering, attempt scoping, invalid relationships, duplicate IDs, concurrent repository instances, append alongside an attempt snapshot change, immutable rows, insert rollback, schema validation, restart retrieval, v3 migration preservation, and migration rollback. No logging, verification, orchestration, recovery, Host wiring, or later persistence entities were added.

Verification completed:

* `dotnet test AiGameStudioWorkstation.sln --verbosity minimal` — passed; 184 passed, 0 failed, 0 skipped.
* `dotnet build AiGameStudioWorkstation.sln` — passed; 0 warnings, 0 errors.
* `git diff --check` — passed.

The next persistence task in the implementation sequence is PERSISTENCE-005.
