# PERSISTENCE-001 — Durable Task State

The Application layer defines `ITaskRepository`; Infrastructure implements it with `SqliteTaskRepository`. Core remains independent of persistence providers. The host or another trusted composition root supplies the database path and calls `InitializeAsync` before repository use.

The SQLite schema stores the current task snapshot in `Tasks` and one immutable fact per successfully applied transition in `TaskOperations`. Task collections are encoded as JSON columns; scalar budget and lifecycle values remain relational columns. Operation rows retain task and operation identity, correlation identity, expected and resulting revisions, previous and resulting states, and application time. A composite primary key enforces `(TaskId, OperationId)` uniqueness, and triggers prevent history updates or deletion.

Each transition uses a SQLite immediate transaction. The repository loads and validates the task and its operation history, delegates lifecycle validation and idempotency to the Core aggregate, conditionally updates the current row by expected revision, inserts the operation fact, and commits both writes together. A stale domain request is returned as a rejected operation; a failed conditional update is reported as a concurrency exception; SQLite failures and corrupt persisted state are surfaced as persistence exceptions. There are no automatic retries.

`InitializeAsync` creates schema version 1 deterministically. The configured database path is infrastructure input, never task content. Tests use unique temporary database files and cover aggregate round trips, restart idempotency, concurrent operations, transaction rollback, immutable history, and explicit corruption failures.
