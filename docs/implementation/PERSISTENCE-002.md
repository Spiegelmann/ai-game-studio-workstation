# PERSISTENCE-002 — Persistence Lifecycle and Schema Evolution

SQLite persistence is Infrastructure-owned. `SqliteTaskRepository` must be explicitly initialized before any repository operation. Initialization is serialized per instance and uses SQLite's immediate transaction for schema work.

## Lifecycle

An instance begins `NotInitialized`. Successful initialization moves it to `Ready`; repeated initialization while ready is a no-op. An initialization failure moves it to `Failed`, and operations and further initialization attempts on that instance fail. Disposal is terminal: operations and initialization then throw `ObjectDisposedException`. Connections are scoped to individual initialization or repository operations and are disposed when that operation completes.

The configured database path is infrastructure input. It must identify a file, and its parent directory must already exist. Initialization does not create directories or replace an invalid, corrupt, or unsupported database.

## Schema

The current schema is version 2. The authoritative version is stored in the singleton `PersistenceSchemaVersion` table as `Version` and `AppliedAt`. The migrations are application code and have a fixed ordered path:

1. `0 → 1`: create the PERSISTENCE-001 task and immutable operation-history schema.
2. `1 → 2`: create or update the explicit schema-version record.

PERSISTENCE-001 databases carry an explicit legacy version 1 in SQLite's `user_version` field. When the new metadata table is absent, initialization accepts only legacy versions 0 and 1; version 1 is validated before migration. Once the table exists, its row is authoritative. An unknown or newer version is rejected; no downgrade or automatic recreation is attempted.

Schema changes and version updates execute in one transaction, so a failed migration leaves the prior version and its data intact. Repeated initialization does not reapply completed migrations or rewrite the `AppliedAt` value.

## Integrity and restart behavior

Before becoming ready, initialization validates required tables and columns, Task and operation uniqueness constraints, the operation-to-task foreign key, immutable-history triggers, the schema-version row, `PRAGMA integrity_check`, and `PRAGMA foreign_key_check`. It does not reconstruct every Task during startup. Integrity or schema failures leave the instance failed and preserve the database for explicit investigation.

Task snapshots, revisions, immutable operation records, and idempotency remain SQLite-backed and are reconstructed by repository operations after restart. Persistence-level initialization does not resume Tasks or execute other recovery orchestration.

The Host remains unwired; a later trusted composition root is responsible for constructing and initializing persistence.
