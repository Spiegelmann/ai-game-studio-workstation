# PERSISTENCE-003 — Persistence Continuation

## Objective

Implement **PERSISTENCE-003** for the AI Game Studio Workstation repository.

Continue from the completed:

* FOUNDATION-002 and subsequent Foundation tasks
* PERSISTENCE-001 — Durable Task State Foundation
* PERSISTENCE-002 — Persistence Lifecycle, Schema Evolution & Recovery

Treat the existing repository implementation, tests, architecture, and the PERSISTENCE-003 task specification already present in the repository as the source of truth.

The objective is to complete PERSISTENCE-003 without prematurely introducing orchestration, execution, governance, reliability, tool execution, autonomous behavior, or Host composition.

---

## Mandatory pre-flight

Before modifying code:

1. Inspect the current repository structure.
2. Read the existing PERSISTENCE-003 specification/documentation in the repository.
3. Inspect the current Core, Application, and Infrastructure persistence implementations.
4. Inspect the existing persistence tests.
5. Inspect the PERSISTENCE-001 and PERSISTENCE-002 implementation/documentation.
6. Determine the exact contracts and invariants already established.
7. Do not duplicate functionality that already exists.
8. Do not weaken or bypass existing persistence invariants merely to satisfy PERSISTENCE-003.

If the repository's PERSISTENCE-003 specification differs from assumptions made in this prompt, **the repository specification wins**.

---

# Architectural constraints

Maintain the existing dependency direction:

```text
Core
  ↑
Application
  ↑
Infrastructure
```

More precisely, dependencies must continue to point inward toward the domain/application abstractions.

### Core

Core must remain:

* provider-independent
* persistence-independent
* infrastructure-independent
* free of SQLite concerns
* free of filesystem/database implementation details

### Application

Application may define persistence-facing abstractions where appropriate, but must not contain SQLite implementation details.

### Infrastructure

Infrastructure owns:

* SQLite implementation
* database connections
* schema management
* migrations
* persistence-specific exceptions
* persistence-specific validation
* database-specific concurrency handling

### Host

Do not wire Host unless PERSISTENCE-003 explicitly requires it.

---

# Preserve existing guarantees

PERSISTENCE-003 must not regress any guarantees established by PERSISTENCE-001 and PERSISTENCE-002.

In particular, preserve:

### Lifecycle

* explicit initialization
* deterministic use-before-initialization failure
* deterministic use-after-disposal failure
* deterministic failed-initialization behavior
* idempotent initialization
* serialized initialization

### Schema

* authoritative schema version metadata
* ordered migrations
* transactional migrations
* future-version rejection
* no automatic downgrade
* no destructive recreation
* no silent database replacement

### Integrity

Continue validating the required database invariants.

Do not introduce a recovery mechanism that silently repairs or destroys an invalid database.

### Durability

Preserve:

* Task state
* Task revision
* operation history
* idempotency
* restart behavior

### Concurrency

Do not regress the existing concurrency guarantees.

Any new persistence operation must be considered under:

* concurrent repository instances
* concurrent operations against the same Task
* duplicate operation identifiers
* stale revisions
* restart after persistence
* failure during transaction processing

---

# Scope discipline

Implement only the functionality required by PERSISTENCE-003.

Do NOT introduce:

* autonomous agents
* LLM integration
* MCP infrastructure
* tool execution
* game-specific logic
* Blender integration
* Hammer integration
* Git automation
* orchestration
* governance
* approval workflows
* execution workers
* scheduling
* distributed infrastructure
* cloud services
* speculative abstractions for future phases

Do not solve later phases early.

Prefer a small, explicit implementation over a generalized framework.

---

# Testing requirements

Add focused tests for every new behavior introduced by PERSISTENCE-003.

Tests should cover, where applicable:

* successful behavior
* invalid input
* lifecycle violations
* persistence failure
* transaction rollback
* duplicate operations
* stale revisions
* concurrent access
* restart behavior
* interaction with existing schema/versioning
* preservation of existing operation history
* preservation of existing Task state

Use the existing test style and naming conventions.

Do not weaken existing tests.

---

# Verification

At completion, run:

```powershell
dotnet test AiGameStudioWorkstation.sln --verbosity minimal
```

Also run:

```powershell
dotnet build AiGameStudioWorkstation.sln
```

And:

```powershell
git diff --check
```

Inspect diagnostics/problems if available.

The implementation is not complete merely because the new tests pass. Existing tests must continue passing.

---

# Documentation

Update or create the PERSISTENCE-003 documentation required by the repository specification.

Document:

* what was introduced
* why it exists
* relevant invariants
* failure behavior
* concurrency behavior
* persistence behavior
* architectural boundaries
* recovery implications
* explicit non-goals

Keep documentation factual and implementation-aligned.

---

# Final review

Before declaring completion, inspect the complete diff.

Verify:

1. No unrelated files were modified.
2. No dependency was introduced without necessity.
3. Core remains provider-independent.
4. Application remains SQLite-independent.
5. Infrastructure owns persistence implementation.
6. Existing PERSISTENCE-001 guarantees remain intact.
7. Existing PERSISTENCE-002 guarantees remain intact.
8. No future-phase functionality was accidentally introduced.
9. Tests cover the new behavior.
10. Full solution tests pass.
11. Full solution build passes.
12. `git diff --check` passes.

Then produce a completion report containing:

## Implementation

Files added/modified and their purpose.

## Behavior

What PERSISTENCE-003 now guarantees.

## Tests

Number of tests added and total solution test result.

## Verification

* tests
* build
* diff check
* diagnostics

## Architecture

Explain why the dependency boundaries remain intact.

## Security / Integrity

Describe any relevant failure-closed behavior and persistence safety.

## Scope / Deviations

State whether any deviations from the PERSISTENCE-003 specification occurred.

## Acceptance

Explicit PASS/FAIL assessment for each acceptance criterion.

## Remaining Work

Identify what remains after PERSISTENCE-003.

Do not mark later roadmap phases as complete merely because their underlying abstractions could theoretically be supported by the implementation.

---

## PERSISTENCE-003 implementation record

The SQLite schema is now version 3. The ordered `2 → 3` migration adds `TaskAttempts`, `TaskAttemptSnapshots`, and `StateTransitions` without rewriting the Task or `TaskOperations` rows. The migration updates the authoritative schema-version row in the same transaction as the new schema objects. A failed migration leaves schema version 2 and existing Task/operation data intact.

`ITaskRepository.AppendAttemptAsync` persists a TaskAttempt identity and the first immutable snapshot. Later states for the same attempt are appended under the stable attempt ID with a monotonically increasing snapshot number. Attempt numbers are contiguous per Task, snapshots must follow the domain's `TaskAttempt.TransitionTo` behavior, and exact snapshot replays are idempotent. `GetAttemptsAsync` returns every snapshot in attempt-number and snapshot order so callers can reconstruct retry history.

`ApplyTransitionWithHistoryAsync` pairs a caller-supplied `StateTransition` with its `TaskTransitionOperation`. It validates the Task ID, target state, previous state, revision, and timestamp; then commits the Task snapshot, immutable operation fact, StateTransition record, and optional associated attempt snapshot together. The StateTransition record retains the operation ID and expected/resulting revisions alongside the domain transition data. `GetStateTransitionsAsync` returns these records in resulting-revision order. Replaying the same paired operation is idempotent; conflicting or missing paired history is surfaced rather than silently repaired.

Attempt identities, attempt snapshots, and StateTransition rows have restricted foreign keys and immutable update/delete triggers. SQLite immediate transactions serialize competing writers, while unique attempt numbers, snapshot sequence keys, operation IDs, transition IDs, and revisions prevent conflicting history. Persistence errors fail the operation and roll back its transaction. Use-before-initialization, failed initialization, disposal, schema validation, integrity checks, and future-version rejection retain the PERSISTENCE-002 behavior.

PERSISTENCE-001 `TaskOperations` remain the immutable record for transitions written through the original `ApplyTransitionAsync` contract. PERSISTENCE-003 does not fabricate StateTransition IDs or reasons when migrating those older rows; only paired writes made through `ApplyTransitionWithHistoryAsync` create the richer StateTransition records. Existing operation history remains available through the Task aggregate.

The current `TaskAttempt` domain contract has state and timestamps but no failure-classification or failure-reference fields. PERSISTENCE-003 persists all fields currently defined by that contract and does not add execution/recovery data or alter Core. ToolInvocation, EvidenceRecord, Checkpoint, RecoveryDecision, VerificationResult, HumanDecision, orchestration, and Host composition remain out of scope.
