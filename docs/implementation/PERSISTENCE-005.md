# PERSISTENCE-005 — Persistence Integrity, Recovery & Finalization

## Objective

Complete the persistence phase by performing a focused integrity, recovery, and architectural finalization pass over the persistence implementation produced by:

* PERSISTENCE-001 — Durable Task State Foundation
* PERSISTENCE-002
* PERSISTENCE-003 — Durable TaskAttempt Snapshots & StateTransition History
* PERSISTENCE-004 — Durable Evidence Persistence

The goal is **not** to introduce new domain capabilities or redesign existing contracts.

The goal is to establish that the persistence subsystem is coherent, restart-safe, failure-safe, deterministic, and ready for the next architectural phase.

This is a verification and hardening task first, with implementation changes only where a concrete persistence defect or missing invariant is identified.

---

## 1. Read Before Changing Anything

Inspect the current repository and establish the actual state of the persistence implementation.

Read:

* `AGENTS.md`
* relevant `.github/rules/*`
* all existing persistence task specifications
* `PERSISTENCE-001`
* `PERSISTENCE-002`
* `PERSISTENCE-003`
* `PERSISTENCE-004`
* `ITaskRepository`
* `SqliteTaskRepository`
* all persistence/domain/application contracts involved in persistence
* all existing persistence tests
* current SQLite schema and migration code
* current Core/Application/Infrastructure project boundaries

Do not assume the task specifications perfectly describe the current implementation. Treat the repository as authoritative.

---

# 2. Persistence Invariant Audit

Perform a systematic audit of the complete persistence subsystem.

Verify that the following invariants hold.

### Task identity

* Task IDs are immutable.
* Task IDs uniquely identify a task.
* Loading a nonexistent task behaves deterministically.
* Duplicate task creation cannot silently create conflicting state.

### Task lifecycle

* Persisted lifecycle state can be reconstructed through the domain rules.
* Invalid state transitions cannot be persisted.
* Revision/state consistency is maintained.
* Competing updates cannot silently overwrite one another.
* Transition history remains consistent with current state.

### TaskAttempt

* Attempts belong to exactly one Task.
* Attempt identity is stable.
* Attempt snapshots cannot become detached from their owning Task.
* Attempt persistence survives process restart.
* Invalid or conflicting attempt state cannot silently overwrite persisted state.

### Evidence

* Evidence belongs to the appropriate Task/Attempt relationship.
* Evidence identity is immutable.
* Duplicate identical evidence is idempotent.
* Reuse of an Evidence ID with different data fails.
* Retrieval ordering is deterministic.
* Evidence cannot become orphaned through Task/Attempt deletion or corruption.
* Existing foreign-key and uniqueness constraints remain effective.

### Operation/history records

* Historical records remain immutable.
* History survives restart.
* Revision/concurrency checks prevent conflicting updates.
* Replay/reconstruction behavior remains deterministic.
* Historical records cannot silently diverge from the current aggregate state.

---

# 3. Failure and Recovery Audit

Explicitly test failure behavior around the persistence boundary.

Add tests where justified for scenarios such as:

* process/application restart after successful writes
* loading state after restart
* failed transaction followed by a successful retry
* duplicate operation/retry
* duplicate evidence/retry
* concurrent writes against the same Task
* concurrent writes against the same TaskAttempt
* interrupted/failed persistence operation
* invalid persisted state
* corrupted or inconsistent relational references
* migration failure/rollback where practical within the existing test infrastructure

The important requirement is:

> A persistence failure must not leave the system in a partially committed logical state.

Do not introduce artificial abstractions solely to manufacture testability.

Use the existing architecture and test patterns.

---

# 4. Idempotency Audit

Review every persistence operation that is expected to be safely retryable.

Document and test the intended behavior for:

* task creation
* lifecycle transitions
* attempt persistence
* evidence persistence
* history persistence where applicable

Distinguish carefully between:

1. **safe duplicate replay**
2. **conflicting duplicate**
3. **concurrent conflicting update**

These are different failure modes and must not be conflated.

For example:

* identical record + identical identity → safe/idempotent where the contract permits
* identical identity + different payload → deterministic conflict
* stale revision → concurrency/conflict failure

Do not broaden idempotency beyond what the existing contracts logically permit.

---

# 5. Migration Integrity

Audit the complete SQLite migration chain currently present.

Verify:

* migrations execute in the correct order
* schema versions advance atomically
* migration failure does not leave a partially upgraded schema
* previously persisted data survives migration
* foreign keys and uniqueness constraints are present after migration
* reopening an already migrated database does not reapply migrations incorrectly
* the final schema version is deterministic

Add missing tests where appropriate.

Do not rewrite the migration system if the existing implementation is already sound.

---

# 6. Restart Durability

Create or strengthen integration tests demonstrating the following lifecycle:

```text
Create Task
    ↓
Persist lifecycle state
    ↓
Persist Attempt
    ↓
Persist Evidence
    ↓
Persist history/state
    ↓
Dispose repository/process
    ↓
Create a new repository instance
    ↓
Reload persisted state
    ↓
Verify logical state is equivalent
```

The test must verify actual durable storage rather than merely reusing an in-memory object graph.

If the existing test infrastructure already demonstrates these guarantees individually, add only the missing end-to-end coverage.

---

# 7. Repository Contract Audit

Review `ITaskRepository` as the Application-facing persistence boundary.

Verify:

* no SQLite types leak into Application
* no Infrastructure implementation details leak into Application
* contracts represent domain/application concepts
* methods have deterministic behavior
* exceptions/results communicate persistence failures appropriately
* transaction semantics are not falsely implied where the contract does not guarantee them

Do **not** redesign the repository abstraction merely for aesthetic reasons.

If a concern is architectural but not a concrete defect, document it rather than expanding scope.

---

# 8. Evidence and Operation Linkage

Explicitly review the limitation identified in PERSISTENCE-004:

> `EvidenceRecord` currently has no `OperationId`, and the existing contracts do not support combined transition/evidence writes.

Determine whether this is:

1. intentionally deferred functionality,
2. a persistence defect,
3. or a requirement that belongs to a later application/orchestration phase.

Do not invent an `OperationId`, event relationship, or transaction API merely to close the acceptance table.

If it belongs later, document the boundary clearly.

The expected default is to **preserve the existing domain/application model** and defer the concern unless repository evidence proves it is required now.

---

# 9. Security and Integrity Review

Perform a focused persistence security review.

Check for:

* unsafe SQL construction
* uncontrolled database paths
* accidental acceptance of arbitrary schema/table identifiers
* unsafe deserialization
* sensitive information leaking through persistence exceptions
* migration behavior that could execute unintended SQL
* missing foreign-key enforcement
* insufficient uniqueness constraints
* update/delete paths that could bypass immutability
* race conditions around revision checks
* transaction boundaries that permit partial logical writes

Do not introduce security infrastructure unrelated to persistence.

Fix concrete findings that belong within this task.

Document findings that belong to a later security phase.

---

# 10. Determinism Audit

Verify that persistence behavior is deterministic with respect to:

* task reconstruction
* attempt reconstruction
* transition history ordering
* evidence ordering
* duplicate handling
* conflict handling
* migration state
* repository restart

Avoid relying on unspecified SQLite ordering.

Where ordering is part of the contract, explicitly enforce it.

---

# 11. Test Quality

Review existing persistence tests for:

* meaningful assertions
* isolation
* deterministic setup
* proper disposal
* actual database usage where required
* absence of tests that pass only because implementation details are mocked
* coverage of both success and failure paths

Do not chase arbitrary coverage percentages.

Prioritize tests that protect architectural and persistence invariants.

---

# 12. Implementation Constraints

Follow these constraints strictly:

* Do not add new external dependencies unless absolutely necessary.
* Do not introduce new domain concepts.
* Do not redesign the architecture.
* Do not introduce CQRS, event sourcing, messaging, background workers, or orchestration.
* Do not introduce MCP/tooling functionality.
* Do not add application services merely to make persistence tests easier.
* Do not expand the repository into a generic data-access abstraction.
* Do not introduce speculative abstractions.
* Do not change public contracts unless a concrete defect requires it.
* Preserve Core's provider independence.
* Keep SQLite-specific concerns inside Infrastructure.
* Preserve existing fail-closed behavior.
* Prefer the smallest correct change.

---

# 13. Required Deliverables

Produce:

### A. Persistence Integrity Audit

A concise written assessment covering:

* invariants verified
* failure/recovery behavior
* idempotency
* migrations
* restart durability
* determinism
* security
* architectural boundaries

### B. Implementation

Only implement concrete defects or missing safeguards discovered by the audit.

### C. Tests

Add focused tests for any newly identified gaps.

All existing tests must continue to pass.

### D. Implementation Record

Create or update:

`PERSISTENCE-005.md`

Include:

* task ID
* objective
* audit performed
* findings
* changes made
* tests added/updated
* architectural decisions
* intentionally deferred concerns
* verification results
* remaining persistence risks, if any

---

# 14. Verification Requirements

Before declaring completion, run:

```text
dotnet test AiGameStudioWorkstation.sln --verbosity minimal
dotnet build AiGameStudioWorkstation.sln
git diff --check
```

Also inspect diagnostics for changed files.

Expected result:

* zero build errors
* zero test failures
* zero new warnings unless explicitly justified
* no formatting/diff-check errors

Report exact test counts.

---

# 15. Completion Report

Return a structured completion report containing:

## Task

* Task ID
* Task title

## Audit

* invariants
* failure/recovery
* idempotency
* migrations
* restart durability
* determinism
* security
* architecture

## Implementation

List every changed file and why it changed.

## Tests

List:

* tests added
* tests modified
* important scenarios covered

## Deferred Concerns

Explicitly identify concerns that remain intentionally outside persistence scope.

In particular address:

* Evidence ↔ Operation linkage
* combined transition + evidence transactions
* orchestration-level recovery
* future event/messaging concerns

## Verification

Report exact results of:

* `dotnet test`
* `dotnet build`
* `git diff --check`
* diagnostics

## Final Assessment

Classify the persistence phase as one of:

* **READY**
* **READY WITH DOCUMENTED DEFERMENTS**
* **NOT READY**

Use **READY WITH DOCUMENTED DEFERMENTS** when the remaining limitations are intentional architectural boundaries rather than defects.

Do not claim READY if a concrete persistence integrity defect remains unresolved.

---

## Important Final Instruction

PERSISTENCE-005 is the **final hardening/finalization task of the current persistence phase**.

Do not automatically begin the next architectural phase after completing this task.

Stop after producing the completion report.

The next phase will be selected separately based on the resulting persistence audit.

---

## PERSISTENCE-005 Implementation Record

### Task

* **Task ID:** PERSISTENCE-005
* **Task title:** Persistence Integrity, Recovery & Finalization
* **Objective:** Audit and harden the existing SQLite task persistence without expanding the domain model or persistence scope.

### Audit

* **Task identity:** `Tasks.TaskId` is the primary key. Duplicate creation fails instead of replacing stored data. `GetByIdAsync` returns `null` for a missing task. The repository does not update task identity.
* **Task lifecycle and history:** Task state is reconstructed by replaying operation rows in resulting-revision order through Core transition rules. The reconstructed state, revision, and update timestamp must match the task snapshot. Paired StateTransition rows are checked against their TaskOperation rows when loaded.
* **Attempts:** Attempt identity and per-task number constraints, foreign keys, and immutable-history triggers are enforced. Snapshot reads require contiguous sequence numbers and valid domain transitions. Exact snapshot replays are idempotent; conflicting state snapshots fail.
* **Evidence:** Evidence IDs are unique; evidence references a Task and, when present, an Attempt belonging to that same Task. Evidence is immutable, exact duplicate writes are idempotent, ID reuse with different data fails, and retrieval follows insertion sequence.
* **Failure and recovery:** Task transition/operation writes, paired transition history, and optional attempt snapshots share a transaction. Evidence append is transactionally isolated according to its existing contract. A failed state-history write rolls back all writes in that transaction, and a retry after removing the injected failure succeeds.
* **Idempotency:** Task creation does not silently overwrite; duplicate operation IDs replay only identical operations; exact Attempt snapshot and Evidence replays do not append duplicates; conflicting payloads and stale revisions fail/reject distinctly.
* **Migrations:** Ordered migrations advance schema versions 0→1→2→3→4 transactionally. Migration rollback tests preserve prior version and data. Existing validation rejects unsupported/malformed schemas, missing constraints/triggers, integrity failures, and foreign-key violations. Reinitialization leaves the version-4 database unchanged.
* **Restart durability and determinism:** A file-backed end-to-end test disposes a repository after persisting Task state, operation and StateTransition history, an Attempt snapshot, and Evidence, then reconstructs these using a new repository. Histories are explicitly ordered by revision, attempt/snapshot number, and evidence sequence.
* **Security:** SQL values are parameterized; schema/table names are fixed implementation constants. Database paths are canonicalized and require an existing parent directory. SQLite foreign keys are enabled, schema objects and immutability triggers are checked, and JSON/domain reconstruction fails closed on invalid persisted values.
* **Architecture:** `ITaskRepository` and persistence records in Application contain domain/application types only. SQLite, schema, migrations, and database-specific validation remain in Infrastructure; Core remains persistence-provider-independent.

### Finding and Implementation

An idempotent `ApplyTransitionWithHistoryAsync` replay previously verified an associated Attempt only when one was supplied on replay. Omitting an Attempt that had been recorded with the original operation therefore returned `AlreadyApplied`, conflating a different replay payload with an exact duplicate. Replay now compares both the Attempt ID and snapshot number, including the null/no-Attempt case, and rejects a mismatch.

### Tests Added or Updated

* Added concurrent exact Attempt snapshot replay coverage; both writes complete while only one snapshot is stored.
* Expanded the restart integration test to include Attempt-linked Evidence and to verify that omitting the originally associated Attempt is rejected on replay.
* Expanded the transaction failure test to remove the injected database failure, retry the exact operation, and verify durable Task, Attempt, and StateTransition state.

### Deferred Concerns

* Evidence has no `OperationId`; the current domain/application contracts do not support Evidence↔Operation linkage. This is intentionally deferred rather than represented by a fabricated relationship.
* Transition, StateTransition, Attempt, and Evidence are not one combined transaction. The existing contracts expose separate transition/history and evidence operations, so introducing a combined transaction API belongs to a later application/orchestration task.
* Orchestration-level recovery, event/messaging concerns, and persistence for Checkpoint, RecoveryDecision, ToolInvocation, VerificationResult, and HumanDecision remain outside this persistence phase.
* PERSISTENCE-001 operations written through `ApplyTransitionAsync` do not acquire fabricated PERSISTENCE-003 StateTransition IDs/reasons; the original immutable TaskOperation history remains authoritative for those transitions.

### Verification

* `dotnet test AiGameStudioWorkstation.sln --verbosity minimal` — **PASS**, 185 passed, 0 failed, 0 skipped.
* `dotnet build AiGameStudioWorkstation.sln` — **PASS**, 0 warnings, 0 errors.
* `git diff --check` — **PASS**.
* Diagnostics for the changed C# files — **PASS**, no errors reported.

### Final Assessment

**READY WITH DOCUMENTED DEFERMENTS.** The identified replay inconsistency is fixed and covered. Remaining limitations are architectural boundaries documented above, not unresolved persistence integrity defects.

### Acceptance

* Persistence invariants audited: **PASS**
* Failure/rollback and retry behavior: **PASS**
* Idempotency and conflicting replay behavior: **PASS**
* Migration integrity and restart durability: **PASS**
* Deterministic history/evidence ordering: **PASS**
* Security and Core/Application/Infrastructure boundaries: **PASS**
* Required deferred concerns documented: **PASS**
* Full solution tests/build, diff check, and diagnostics: **PASS**

### Next Task

The next implementation-sequence task is **POLICY-001**. It has not been started.
