using System.Collections.Concurrent;
using AiGameStudioWorkstation.Application.Tasks.Persistence;
using AiGameStudioWorkstation.Core.Domain.Execution;
using AiGameStudioWorkstation.Core.Domain.Tasks;
using AiGameStudioWorkstation.Infrastructure.Persistence.Sqlite;
using Microsoft.Data.Sqlite;
using DomainTask = AiGameStudioWorkstation.Core.Domain.Tasks.Task;

namespace AiGameStudioWorkstation.Infrastructure.Tests;

public sealed class SqliteTaskRepositoryTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async System.Threading.Tasks.Task Task_round_trip_preserves_all_domain_data()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = await database.CreateInitializedRepositoryAsync();
        var task = CreateTask(
            dependencies: [new TaskId("foundation-004")],
            scope: new TaskScope(["src/Core", "tests/Core.Tests"]),
            acceptanceCriteria: [new AcceptanceCriterion("AC-001", "State is durable.", true)],
            stopConditions: [new StopCondition("SC-001", "Stop on corruption.", false)],
            changeBudget: new ChangeBudget(3, 2, 5, 1, 1));

        await repository.AddAsync(task);
        var restored = await (await database.CreateInitializedRepositoryAsync()).GetByIdAsync(task.Id);

        Assert.NotNull(restored);
        Assert.Equal(task.Id, restored.Id);
        Assert.Equal(task.Intent, restored.Intent);
        Assert.Equal(task.Description, restored.Description);
        Assert.Equal(task.Dependencies, restored.Dependencies);
        Assert.Equal(task.Scope, restored.Scope);
        Assert.Equal(task.AcceptanceCriteria, restored.AcceptanceCriteria);
        Assert.Equal(task.StopConditions, restored.StopConditions);
        Assert.Equal(task.ChangeBudget, restored.ChangeBudget);
        Assert.Equal(task.State, restored.State);
        Assert.Equal(task.CreatedAt, restored.CreatedAt);
        Assert.Equal(task.UpdatedAt, restored.UpdatedAt);
        Assert.Equal(task.Revision, restored.Revision);
        Assert.Empty(restored.AppliedOperations);
    }

    [Fact]
    public async System.Threading.Tasks.Task Operation_is_atomic_and_idempotent_after_repository_restart()
    {
        await using var database = await TestDatabase.CreateAsync();
        var initialTask = CreateTask();
        var initialRepository = await database.CreateInitializedRepositoryAsync();
        await initialRepository.AddAsync(initialTask);
        var operation = CreateOperation(initialTask, "operation-durable", TaskState.Ready);
        operation = new TaskTransitionOperation(
            operation.TaskId,
            operation.OperationId,
            operation.ExpectedRevision,
            operation.TargetState,
            new CorrelationId("correlation-durable"));
        var appliedAt = CreatedAt.AddMinutes(1);

        var result = await initialRepository.ApplyTransitionAsync(operation, appliedAt);
        await initialRepository.DisposeAsync();
        var restartedRepository = await database.CreateInitializedRepositoryAsync();
        var restored = await restartedRepository.GetByIdAsync(initialTask.Id);
        var replay = await restartedRepository.ApplyTransitionAsync(operation, CreatedAt);
        var conflictingReplay = await restartedRepository.ApplyTransitionAsync(
            new TaskTransitionOperation(
                operation.TaskId,
                operation.OperationId,
                operation.ExpectedRevision,
                TaskState.Cancelled,
                operation.CorrelationId),
            CreatedAt.AddMinutes(3));

        Assert.Equal(TaskOperationStatus.Applied, result.Status);
        Assert.NotNull(restored);
        Assert.Equal(TaskState.Ready, restored.State);
        Assert.Equal(new TaskRevision(1), restored.Revision);
        Assert.Equal(appliedAt, restored.UpdatedAt);
        Assert.Equal([operation], restored.AppliedOperations);
        Assert.Equal(operation, restored.AppliedOperations[0]);
        Assert.Equal(TaskOperationStatus.AlreadyApplied, replay.Status);
        Assert.Equal(new TaskRevision(1), replay.Task.Revision);
        Assert.Equal(appliedAt, replay.Task.UpdatedAt);
        Assert.Equal(TaskOperationStatus.Rejected, conflictingReplay.Status);
        Assert.Equal(TaskOperationRejection.OperationIdConflict, conflictingReplay.Rejection);
        Assert.Equal(new TaskRevision(1), conflictingReplay.Task.Revision);
    }

    [Fact]
    public async System.Threading.Tasks.Task Attempt_snapshots_are_append_only_and_survive_repository_restart()
    {
        await using var database = await TestDatabase.CreateAsync();
        var task = CreateTask();
        var repository = await database.CreateInitializedRepositoryAsync();
        await repository.AddAsync(task);

        var attemptOne = new TaskAttempt(
            new AttemptId("attempt-one"),
            task.Id,
            1,
            TaskAttemptState.Created,
            CreatedAt.AddMinutes(1));
        var running = attemptOne.TransitionTo(TaskAttemptState.Running);
        var failed = running.TransitionTo(TaskAttemptState.Failed, CreatedAt.AddMinutes(4));
        var attemptTwo = new TaskAttempt(
            new AttemptId("attempt-two"),
            task.Id,
            2,
            TaskAttemptState.Completed,
            CreatedAt.AddMinutes(5),
            CreatedAt.AddMinutes(8));

        await repository.AppendAttemptAsync(attemptOne);
        await repository.AppendAttemptAsync(running);
        await repository.AppendAttemptAsync(failed);
        await repository.AppendAttemptAsync(attemptTwo);
        await repository.AppendAttemptAsync(failed);

        var restartedRepository = await database.CreateInitializedRepositoryAsync();
        var snapshots = await restartedRepository.GetAttemptsAsync(task.Id);

        Assert.Equal([attemptOne, running, failed, attemptTwo], snapshots);
        Assert.Equal(4, await database.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM TaskAttemptSnapshots;"));
        await Assert.ThrowsAsync<SqliteException>(() => database.ExecuteSqlAsync(
            "UPDATE TaskAttemptSnapshots SET State = $state WHERE AttemptId = $attemptId AND SnapshotNumber = 1;",
            ("$state", (int)TaskAttemptState.Failed),
            ("$attemptId", attemptOne.Id.Value)));
        await Assert.ThrowsAsync<SqliteException>(() => database.ExecuteSqlAsync(
            "DELETE FROM TaskAttemptSnapshots WHERE AttemptId = $attempt-one AND SnapshotNumber = 1;",
            ("$attempt-one", attemptOne.Id.Value)));
    }

    [Fact]
    public async System.Threading.Tasks.Task Attempts_reject_missing_tasks_and_conflicting_or_invalid_snapshots()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = await database.CreateInitializedRepositoryAsync();
        var missingTaskAttempt = new TaskAttempt(
            new AttemptId("attempt-missing-task"),
            new TaskId("missing-task"),
            1,
            TaskAttemptState.Created,
            CreatedAt);

        await Assert.ThrowsAsync<TaskNotFoundException>(() => repository.AppendAttemptAsync(missingTaskAttempt));

        var task = CreateTask();
        await repository.AddAsync(task);
        var created = new TaskAttempt(
            new AttemptId("attempt-conflict"),
            task.Id,
            1,
            TaskAttemptState.Created,
            CreatedAt.AddMinutes(1));
        await repository.AppendAttemptAsync(created);

        var reusedAttemptId = new TaskAttempt(
            created.Id,
            task.Id,
            2,
            TaskAttemptState.Created,
            created.StartedAt);
        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.AppendAttemptAsync(reusedAttemptId));

        var completed = created.TransitionTo(TaskAttemptState.Completed, CreatedAt.AddMinutes(2));
        await repository.AppendAttemptAsync(completed);
        var resumed = new TaskAttempt(
            completed.Id,
            completed.TaskId,
            completed.Number,
            TaskAttemptState.Running,
            completed.StartedAt);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.AppendAttemptAsync(resumed));
        Assert.Equal([created, completed], await repository.GetAttemptsAsync(task.Id));
    }

    [Fact]
    public async System.Threading.Tasks.Task Concurrent_duplicate_attempt_snapshots_are_idempotent()
    {
        await using var database = await TestDatabase.CreateAsync();
        var initializer = await database.CreateInitializedRepositoryAsync();
        var task = CreateTask();
        await initializer.AddAsync(task);
        var attempt = new TaskAttempt(
            new AttemptId("attempt-concurrent-duplicate"),
            task.Id,
            1,
            TaskAttemptState.Created,
            CreatedAt.AddMinutes(1));
        var repositoryA = await database.CreateInitializedRepositoryAsync();
        var repositoryB = await database.CreateInitializedRepositoryAsync();

        await System.Threading.Tasks.Task.WhenAll(
            repositoryA.AppendAttemptAsync(attempt),
            repositoryB.AppendAttemptAsync(attempt));

        Assert.Equal([attempt], await (await database.CreateInitializedRepositoryAsync())
            .GetAttemptsAsync(task.Id));
        Assert.Equal(1, await database.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM TaskAttemptSnapshots;"));
    }

    [Fact]
    public async System.Threading.Tasks.Task Evidence_is_append_only_ordered_and_survives_repository_restart()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = await database.CreateInitializedRepositoryAsync();
        var task = CreateTask();
        await repository.AddAsync(task);
        var attempt = new TaskAttempt(
            new AttemptId("attempt-evidence"),
            task.Id,
            1,
            TaskAttemptState.Created,
            CreatedAt.AddMinutes(1));
        await repository.AppendAttemptAsync(attempt);

        var first = CreateEvidence("evidence-first", task.Id);
        var second = CreateEvidence("evidence-second", task.Id, attempt.Id);
        var third = CreateEvidence("evidence-third", task.Id);
        await repository.AddEvidenceAsync(first);
        await repository.AddEvidenceAsync(second);
        await repository.AddEvidenceAsync(first);
        await repository.AddEvidenceAsync(third);

        var restartedRepository = await database.CreateInitializedRepositoryAsync();
        Assert.Equal([first, second, third], await restartedRepository.GetEvidenceAsync(task.Id));
        Assert.Equal(
            [second],
            await restartedRepository.GetEvidenceForAttemptAsync(task.Id, attempt.Id));
        Assert.Equal(3, await database.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM EvidenceRecords;"));

        await Assert.ThrowsAsync<SqliteException>(() => database.ExecuteSqlAsync(
            "UPDATE EvidenceRecords SET Summary = 'rewritten' WHERE EvidenceId = $evidenceId;",
            ("$evidenceId", first.Id.Value)));
        await Assert.ThrowsAsync<SqliteException>(() => database.ExecuteSqlAsync(
            "DELETE FROM EvidenceRecords WHERE EvidenceId = $evidenceId;",
            ("$evidenceId", first.Id.Value)));
    }

    [Fact]
    public async System.Threading.Tasks.Task Evidence_rejects_missing_task_or_attempt_and_conflicting_identity_reuse()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = await database.CreateInitializedRepositoryAsync();
        var task = CreateTask();
        var missingTaskEvidence = CreateEvidence("evidence-missing-task", new TaskId("missing-task"));

        await Assert.ThrowsAsync<TaskNotFoundException>(() => repository.AddEvidenceAsync(missingTaskEvidence));
        await repository.AddAsync(task);

        var missingAttemptEvidence = CreateEvidence(
            "evidence-missing-attempt",
            task.Id,
            new AttemptId("missing-attempt"));
        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.AddEvidenceAsync(missingAttemptEvidence));

        var otherTask = CreateTask(taskId: new TaskId("task-other"));
        await repository.AddAsync(otherTask);
        var otherAttempt = new TaskAttempt(
            new AttemptId("attempt-other-task"),
            otherTask.Id,
            1,
            TaskAttemptState.Created,
            CreatedAt.AddMinutes(1));
        await repository.AppendAttemptAsync(otherAttempt);
        var crossTaskEvidence = CreateEvidence(
            "evidence-cross-task-attempt",
            task.Id,
            otherAttempt.Id);
        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.AddEvidenceAsync(crossTaskEvidence));

        var original = CreateEvidence("evidence-identity-conflict", task.Id);
        await repository.AddEvidenceAsync(original);
        var conflicting = CreateEvidence(
            "evidence-identity-conflict",
            task.Id,
            summary: "Different evidence.");
        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.AddEvidenceAsync(conflicting));
        Assert.Equal([original], await repository.GetEvidenceAsync(task.Id));
    }

    [Fact]
    public async System.Threading.Tasks.Task Concurrent_evidence_appends_preserve_distinct_records_and_deduplicate_exact_replays()
    {
        await using var database = await TestDatabase.CreateAsync();
        var initializer = await database.CreateInitializedRepositoryAsync();
        var task = CreateTask();
        await initializer.AddAsync(task);
        var repositoryA = await database.CreateInitializedRepositoryAsync();
        var repositoryB = await database.CreateInitializedRepositoryAsync();
        var duplicate = CreateEvidence("evidence-concurrent-duplicate", task.Id);
        var distinctA = CreateEvidence("evidence-concurrent-a", task.Id, summary: "Same content.");
        var distinctB = CreateEvidence("evidence-concurrent-b", task.Id, summary: "Same content.");

        await System.Threading.Tasks.Task.WhenAll(
            repositoryA.AddEvidenceAsync(duplicate),
            repositoryB.AddEvidenceAsync(duplicate),
            repositoryA.AddEvidenceAsync(distinctA),
            repositoryB.AddEvidenceAsync(distinctB));

        var evidence = await (await database.CreateInitializedRepositoryAsync()).GetEvidenceAsync(task.Id);
        Assert.Equal(3, evidence.Count);
        Assert.Contains(duplicate, evidence);
        Assert.Contains(distinctA, evidence);
        Assert.Contains(distinctB, evidence);
        Assert.Equal(3, await database.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM EvidenceRecords;"));
    }

    [Fact]
    public async System.Threading.Tasks.Task Evidence_append_is_consistent_with_concurrent_attempt_snapshot_changes()
    {
        await using var database = await TestDatabase.CreateAsync();
        var initializer = await database.CreateInitializedRepositoryAsync();
        var task = CreateTask();
        await initializer.AddAsync(task);
        var attempt = new TaskAttempt(
            new AttemptId("attempt-evidence-race"),
            task.Id,
            1,
            TaskAttemptState.Created,
            CreatedAt.AddMinutes(1));
        await initializer.AppendAttemptAsync(attempt);
        var runningAttempt = attempt.TransitionTo(TaskAttemptState.Running);
        var evidence = CreateEvidence("evidence-attempt-race", task.Id, attempt.Id);
        var repositoryA = await database.CreateInitializedRepositoryAsync();
        var repositoryB = await database.CreateInitializedRepositoryAsync();

        await System.Threading.Tasks.Task.WhenAll(
            repositoryA.AppendAttemptAsync(runningAttempt),
            repositoryB.AddEvidenceAsync(evidence));

        Assert.Equal(
            [attempt, runningAttempt],
            await (await database.CreateInitializedRepositoryAsync()).GetAttemptsAsync(task.Id));
        Assert.Equal([evidence], await repositoryA.GetEvidenceForAttemptAsync(task.Id, attempt.Id));
    }

    [Fact]
    public async System.Threading.Tasks.Task Failed_evidence_insert_does_not_leave_partial_history()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = await database.CreateInitializedRepositoryAsync();
        var task = CreateTask();
        await repository.AddAsync(task);
        var evidence = CreateEvidence("evidence-insert-rollback", task.Id);
        await database.ExecuteSqlAsync("""
            CREATE TRIGGER FailEvidenceInsert
            BEFORE INSERT ON EvidenceRecords
            BEGIN
                SELECT RAISE(ABORT, 'simulated evidence write failure');
            END;
            """);

        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.AddEvidenceAsync(evidence));
        Assert.Empty(await repository.GetEvidenceAsync(task.Id));
        Assert.Equal(0, await database.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM EvidenceRecords;"));
    }

    [Fact]
    public async System.Threading.Tasks.Task Version_three_migration_preserves_existing_execution_history()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = await database.CreateInitializedRepositoryAsync();
        var task = CreateTask();
        await repository.AddAsync(task);
        var operation = CreateOperation(task, "operation-v3-migration", TaskState.Ready);
        var transition = CreateStateTransition(
            task.Id,
            "transition-v3-migration",
            TaskState.Proposed,
            TaskState.Ready,
            CreatedAt.AddMinutes(1),
            "Task is ready.");
        var attempt = new TaskAttempt(
            new AttemptId("attempt-v3-migration"),
            task.Id,
            1,
            TaskAttemptState.Created,
            CreatedAt.AddMinutes(1));
        await repository.ApplyTransitionWithHistoryAsync(operation, transition, attempt);
        await repository.DisposeAsync();
        await database.MarkAsLegacyVersionThreeAsync();

        var upgradedRepository = await database.CreateInitializedRepositoryAsync();
        var restoredTask = await upgradedRepository.GetByIdAsync(task.Id);

        Assert.NotNull(restoredTask);
        Assert.Equal([operation], restoredTask.AppliedOperations);
        Assert.Equal([attempt], await upgradedRepository.GetAttemptsAsync(task.Id));
        Assert.Collection(
            await upgradedRepository.GetStateTransitionsAsync(task.Id),
            record =>
            {
                Assert.Equal(transition, record.Transition);
                Assert.Equal(operation.OperationId, record.OperationId);
                Assert.Equal(attempt.Id, record.AssociatedAttemptId);
            });
        Assert.Equal(4, await database.ExecuteScalarAsync<int>(
            "SELECT Version FROM PersistenceSchemaVersion WHERE Id = 1;"));
    }

    [Fact]
    public async System.Threading.Tasks.Task Failed_evidence_migration_rolls_back_schema_version_and_existing_data()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = await database.CreateInitializedRepositoryAsync();
        var task = CreateTask();
        await repository.AddAsync(task);
        var operation = CreateOperation(task, "operation-evidence-migration-rollback", TaskState.Ready);
        await repository.ApplyTransitionAsync(operation, CreatedAt.AddMinutes(1));
        await repository.DisposeAsync();
        await database.MarkAsLegacyVersionThreeAsync();

        var failingRepository = new SqliteTaskRepository(
            database.DatabasePath,
            (fromVersion, toVersion) =>
            {
                if (fromVersion == 3 && toVersion == 4)
                {
                    throw new InvalidOperationException("Simulated evidence migration failure.");
                }
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() => failingRepository.InitializeAsync());
        Assert.Equal(3, await database.ExecuteScalarAsync<int>("PRAGMA user_version;"));
        Assert.Equal(3, await database.ExecuteScalarAsync<int>(
            "SELECT Version FROM PersistenceSchemaVersion WHERE Id = 1;"));
        Assert.Equal(0, await database.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_schema WHERE type = 'table' AND name = 'EvidenceRecords';"));
        Assert.Equal(1, await database.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM TaskOperations;"));

        var recoveredRepository = await database.CreateInitializedRepositoryAsync();
        var restoredTask = await recoveredRepository.GetByIdAsync(task.Id);
        Assert.NotNull(restoredTask);
        Assert.Equal([operation], restoredTask.AppliedOperations);
        Assert.Equal(4, await database.ExecuteScalarAsync<int>("PRAGMA user_version;"));
    }

    [Fact]
    public async System.Threading.Tasks.Task Concurrent_attempt_creation_preserves_unique_task_attempt_numbers()
    {
        await using var database = await TestDatabase.CreateAsync();
        var task = CreateTask();
        var initializer = await database.CreateInitializedRepositoryAsync();
        await initializer.AddAsync(task);
        var repositoryA = await database.CreateInitializedRepositoryAsync();
        var repositoryB = await database.CreateInitializedRepositoryAsync();
        var attemptA = new TaskAttempt(
            new AttemptId("concurrent-attempt-a"),
            task.Id,
            1,
            TaskAttemptState.Created,
            CreatedAt.AddMinutes(1));
        var attemptB = new TaskAttempt(
            new AttemptId("concurrent-attempt-b"),
            task.Id,
            1,
            TaskAttemptState.Created,
            CreatedAt.AddMinutes(2));

        var outcomes = await System.Threading.Tasks.Task.WhenAll(
            CaptureAsync(() => repositoryA.AppendAttemptAsync(attemptA)),
            CaptureAsync(() => repositoryB.AppendAttemptAsync(attemptB)));

        Assert.Single(outcomes, exception => exception is null);
        Assert.Single(outcomes, exception =>
            exception is InvalidOperationException or TaskPersistenceException);
        Assert.Single(await (await database.CreateInitializedRepositoryAsync()).GetAttemptsAsync(task.Id));
    }

    [Fact]
    public async System.Threading.Tasks.Task Task_transition_and_attempt_are_atomic_and_reconstructable_after_restart()
    {
        await using var database = await TestDatabase.CreateAsync();
        var task = CreateTask();
        var repository = await database.CreateInitializedRepositoryAsync();
        await repository.AddAsync(task);
        var operation = CreateOperation(task, "operation-with-history", TaskState.Ready);
        var transition = CreateStateTransition(
            task.Id,
            "transition-ready",
            TaskState.Proposed,
            TaskState.Ready,
            CreatedAt.AddMinutes(1),
            "Task is ready.");
        var attempt = new TaskAttempt(
            new AttemptId("attempt-with-transition"),
            task.Id,
            1,
            TaskAttemptState.Created,
            CreatedAt.AddMinutes(1));
        var evidence = CreateEvidence("evidence-restart", task.Id, attempt.Id);

        var result = await repository.ApplyTransitionWithHistoryAsync(operation, transition, attempt);
        await repository.AddEvidenceAsync(evidence);
        await repository.DisposeAsync();
        var restartedRepository = await database.CreateInitializedRepositoryAsync();
        var restoredTask = await restartedRepository.GetByIdAsync(task.Id);
        var transitions = await restartedRepository.GetStateTransitionsAsync(task.Id);
        var attempts = await restartedRepository.GetAttemptsAsync(task.Id);
        var restoredEvidence = await restartedRepository.GetEvidenceAsync(task.Id);
        var replay = await restartedRepository.ApplyTransitionWithHistoryAsync(operation, transition, attempt);
        var replayWithoutAttempt = await CaptureAsync(
            () => restartedRepository.ApplyTransitionWithHistoryAsync(operation, transition));

        Assert.Equal(TaskOperationStatus.Applied, result.Status);
        Assert.NotNull(restoredTask);
        Assert.Equal(TaskState.Ready, restoredTask.State);
        Assert.Equal(new TaskRevision(1), restoredTask.Revision);
        Assert.Collection(
            transitions,
            record =>
            {
                Assert.Equal(transition, record.Transition);
                Assert.Equal(operation.OperationId, record.OperationId);
                Assert.Equal(new TaskRevision(0), record.ExpectedRevision);
                Assert.Equal(new TaskRevision(1), record.ResultingRevision);
                Assert.Equal(attempt.Id, record.AssociatedAttemptId);
                Assert.Equal(1, record.AssociatedAttemptSnapshotNumber);
            });
        Assert.Equal([attempt], attempts);
        Assert.Equal([evidence], restoredEvidence);
        Assert.Equal(TaskOperationStatus.AlreadyApplied, replay.Status);
        Assert.IsType<TaskPersistenceException>(replayWithoutAttempt);
        Assert.Equal(1, await database.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM StateTransitions;"));
        Assert.Equal(1, await database.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM TaskAttemptSnapshots;"));
        await Assert.ThrowsAsync<SqliteException>(() => database.ExecuteSqlAsync(
            "UPDATE StateTransitions SET Reason = 'rewritten' WHERE TaskId = $taskId AND TransitionId = $transitionId;",
            ("$taskId", task.Id.Value),
            ("$transitionId", transition.Id.Value)));
        await Assert.ThrowsAsync<SqliteException>(() => database.ExecuteSqlAsync(
            "DELETE FROM StateTransitions WHERE TaskId = $taskId AND TransitionId = $transitionId;",
            ("$taskId", task.Id.Value),
            ("$transitionId", transition.Id.Value)));
    }

    [Fact]
    public async System.Threading.Tasks.Task Failed_state_transition_insert_rolls_back_task_operation_and_attempt()
    {
        await using var database = await TestDatabase.CreateAsync();
        var task = CreateTask();
        var repository = await database.CreateInitializedRepositoryAsync();
        await repository.AddAsync(task);
        await database.ExecuteSqlAsync("""
            CREATE TRIGGER FailStateTransitionInsert
            BEFORE INSERT ON StateTransitions
            BEGIN
                SELECT RAISE(ABORT, 'simulated state-transition write failure');
            END;
            """);
        var operation = CreateOperation(task, "operation-history-rollback", TaskState.Ready);
        var transition = CreateStateTransition(
            task.Id,
            "transition-rollback",
            TaskState.Proposed,
            TaskState.Ready,
            CreatedAt.AddMinutes(1),
            "Task is ready.");
        var attempt = new TaskAttempt(
            new AttemptId("attempt-rollback"),
            task.Id,
            1,
            TaskAttemptState.Created,
            CreatedAt.AddMinutes(1));

        await Assert.ThrowsAsync<TaskPersistenceException>(() =>
            repository.ApplyTransitionWithHistoryAsync(operation, transition, attempt));

        var persistedTask = await repository.GetByIdAsync(task.Id);
        Assert.NotNull(persistedTask);
        Assert.Equal(TaskState.Proposed, persistedTask.State);
        Assert.Equal(new TaskRevision(0), persistedTask.Revision);
        Assert.Empty(persistedTask.AppliedOperations);
        Assert.Empty(await repository.GetAttemptsAsync(task.Id));
        Assert.Empty(await repository.GetStateTransitionsAsync(task.Id));

        await database.ExecuteSqlAsync("DROP TRIGGER FailStateTransitionInsert;");
        var retry = await repository.ApplyTransitionWithHistoryAsync(operation, transition, attempt);
        var retriedTask = await repository.GetByIdAsync(task.Id);

        Assert.Equal(TaskOperationStatus.Applied, retry.Status);
        Assert.NotNull(retriedTask);
        Assert.Equal(TaskState.Ready, retriedTask.State);
        Assert.Equal(new TaskRevision(1), retriedTask.Revision);
        Assert.Equal([attempt], await repository.GetAttemptsAsync(task.Id));
        Assert.Collection(
            await repository.GetStateTransitionsAsync(task.Id),
            record => Assert.Equal(transition, record.Transition));
    }

    [Fact]
    public async System.Threading.Tasks.Task Concurrent_task_transitions_preserve_history_and_reject_stale_revision()
    {
        await using var database = await TestDatabase.CreateAsync();
        var task = CreateTask();
        var initializer = await database.CreateInitializedRepositoryAsync();
        await initializer.AddAsync(task);
        var repositoryA = await database.CreateInitializedRepositoryAsync();
        var repositoryB = await database.CreateInitializedRepositoryAsync();
        var operationA = CreateOperation(task, "operation-race-ready", TaskState.Ready);
        var operationB = CreateOperation(task, "operation-race-cancel", TaskState.Cancelled);
        var transitionA = CreateStateTransition(
            task.Id,
            "transition-race-ready",
            TaskState.Proposed,
            TaskState.Ready,
            CreatedAt.AddMinutes(1),
            "Task is ready.");
        var transitionB = CreateStateTransition(
            task.Id,
            "transition-race-cancel",
            TaskState.Proposed,
            TaskState.Cancelled,
            CreatedAt.AddMinutes(2),
            "Task was cancelled.");

        var results = await System.Threading.Tasks.Task.WhenAll(
            repositoryA.ApplyTransitionWithHistoryAsync(operationA, transitionA),
            repositoryB.ApplyTransitionWithHistoryAsync(operationB, transitionB));

        Assert.Single(results, result => result.Status == TaskOperationStatus.Applied);
        Assert.Single(results, result =>
            result.Status == TaskOperationStatus.Rejected
            && result.Rejection == TaskOperationRejection.StaleRevision);
        Assert.Single(await (await database.CreateInitializedRepositoryAsync())
            .GetStateTransitionsAsync(task.Id));
    }

    [Fact]
    public async System.Threading.Tasks.Task Competing_operations_cannot_overwrite_the_same_revision()
    {
        await using var database = await TestDatabase.CreateAsync();
        var task = CreateTask();
        var initialRepository = await database.CreateInitializedRepositoryAsync();
        await initialRepository.AddAsync(task);
        var first = CreateOperation(task, "operation-ready", TaskState.Ready);
        var second = CreateOperation(task, "operation-cancel", TaskState.Cancelled);
        var repositoryA = await database.CreateInitializedRepositoryAsync();
        var repositoryB = await database.CreateInitializedRepositoryAsync();

        var results = await System.Threading.Tasks.Task.WhenAll(
            repositoryA.ApplyTransitionAsync(first, CreatedAt.AddMinutes(1)),
            repositoryB.ApplyTransitionAsync(second, CreatedAt.AddMinutes(2)));

        Assert.Single(results, result => result.Status == TaskOperationStatus.Applied);
        Assert.Single(results, result =>
            result.Status == TaskOperationStatus.Rejected
            && result.Rejection == TaskOperationRejection.StaleRevision);
        var persisted = await (await database.CreateInitializedRepositoryAsync()).GetByIdAsync(task.Id);
        Assert.NotNull(persisted);
        Assert.Equal(new TaskRevision(1), persisted.Revision);
        Assert.Single(persisted.AppliedOperations);
    }

    [Fact]
    public async System.Threading.Tasks.Task Concurrent_duplicate_operation_commits_only_once()
    {
        await using var database = await TestDatabase.CreateAsync();
        var task = CreateTask();
        var repository = await database.CreateInitializedRepositoryAsync();
        await repository.AddAsync(task);
        var operation = CreateOperation(task, "operation-race", TaskState.Ready);
        var repositoryA = await database.CreateInitializedRepositoryAsync();
        var repositoryB = await database.CreateInitializedRepositoryAsync();

        var results = await System.Threading.Tasks.Task.WhenAll(
            repositoryA.ApplyTransitionAsync(operation, CreatedAt.AddMinutes(1)),
            repositoryB.ApplyTransitionAsync(operation, CreatedAt.AddMinutes(2)));

        Assert.Single(results, result => result.Status == TaskOperationStatus.Applied);
        Assert.Single(results, result => result.Status == TaskOperationStatus.AlreadyApplied);
        var persisted = await (await database.CreateInitializedRepositoryAsync()).GetByIdAsync(task.Id);
        Assert.NotNull(persisted);
        Assert.Equal(new TaskRevision(1), persisted.Revision);
        Assert.Single(persisted.AppliedOperations);
    }

    [Fact]
    public async System.Threading.Tasks.Task Rejected_transition_does_not_write_a_task_or_operation_change()
    {
        await using var database = await TestDatabase.CreateAsync();
        var task = CreateTask(state: TaskState.InProgress, revision: new TaskRevision(4));
        var repository = await database.CreateInitializedRepositoryAsync();
        await repository.AddAsync(task);
        var invalidOperation = CreateOperation(task, "operation-invalid", TaskState.Accepted);

        var result = await repository.ApplyTransitionAsync(
            invalidOperation,
            CreatedAt.AddMinutes(1));
        var persisted = await repository.GetByIdAsync(task.Id);

        Assert.Equal(TaskOperationStatus.Rejected, result.Status);
        Assert.Equal(TaskOperationRejection.InvalidTransition, result.Rejection);
        Assert.NotNull(persisted);
        Assert.Equal(TaskState.InProgress, persisted.State);
        Assert.Equal(new TaskRevision(4), persisted.Revision);
        Assert.Empty(persisted.AppliedOperations);
    }

    [Fact]
    public async System.Threading.Tasks.Task Failed_operation_insert_rolls_back_task_revision_and_state()
    {
        await using var database = await TestDatabase.CreateAsync();
        var task = CreateTask();
        var repository = await database.CreateInitializedRepositoryAsync();
        await repository.AddAsync(task);
        await database.ExecuteSqlAsync("""
            CREATE TRIGGER FailTaskOperationInsert
            BEFORE INSERT ON TaskOperations
            BEGIN
                SELECT RAISE(ABORT, 'simulated operation write failure');
            END;
            """);
        var operation = CreateOperation(task, "operation-rollback", TaskState.Ready);
        await Assert.ThrowsAsync<TaskPersistenceException>(() =>
            repository.ApplyTransitionAsync(operation, CreatedAt.AddMinutes(1)));

        var persisted = await repository.GetByIdAsync(task.Id);
        Assert.NotNull(persisted);
        Assert.Equal(TaskState.Proposed, persisted.State);
        Assert.Equal(new TaskRevision(0), persisted.Revision);
        Assert.Equal(CreatedAt, persisted.UpdatedAt);
        Assert.Empty(persisted.AppliedOperations);
    }

    [Fact]
    public async System.Threading.Tasks.Task Invalid_persisted_json_fails_as_data_corruption()
    {
        await using var database = await TestDatabase.CreateAsync();
        var task = CreateTask();
        var repository = await database.CreateInitializedRepositoryAsync();
        await repository.AddAsync(task);
        await database.ExecuteSqlAsync(
            "UPDATE Tasks SET DependenciesJson = 'not-json' WHERE TaskId = $taskId;",
            ("$taskId", task.Id.Value));

        await Assert.ThrowsAsync<TaskDataCorruptionException>(() =>
            repository.GetByIdAsync(task.Id));
    }

    [Fact]
    public async System.Threading.Tasks.Task Operation_history_rows_cannot_be_updated_or_deleted()
    {
        await using var database = await TestDatabase.CreateAsync();
        var task = CreateTask();
        var repository = await database.CreateInitializedRepositoryAsync();
        await repository.AddAsync(task);
        var operation = CreateOperation(task, "operation-immutable", TaskState.Ready);
        await repository.ApplyTransitionAsync(operation, CreatedAt.AddMinutes(1));

        await Assert.ThrowsAsync<SqliteException>(() => database.ExecuteSqlAsync(
            "UPDATE TaskOperations SET TargetState = $target WHERE TaskId = $taskId AND OperationId = $operationId;",
            ("$target", (int)TaskState.Cancelled),
            ("$taskId", task.Id.Value),
            ("$operationId", operation.OperationId.Value)));
        await Assert.ThrowsAsync<SqliteException>(() => database.ExecuteSqlAsync(
            "DELETE FROM TaskOperations WHERE TaskId = $taskId AND OperationId = $operationId;",
            ("$taskId", task.Id.Value),
            ("$operationId", operation.OperationId.Value)));
    }

    [Fact]
    public async System.Threading.Tasks.Task Invalid_operation_identity_fails_as_data_corruption()
    {
        await using var database = await TestDatabase.CreateAsync();
        var task = CreateTask();
        var repository = await database.CreateInitializedRepositoryAsync();
        await repository.AddAsync(task);
        var operation = CreateOperation(task, "operation-corrupt", TaskState.Ready);
        await repository.ApplyTransitionAsync(operation, CreatedAt.AddMinutes(1));
        await database.ExecuteSqlAsync(
            """
            DROP TRIGGER TaskOperations_NoUpdate;
            UPDATE TaskOperations
            SET CorrelationId = ' '
            WHERE TaskId = $taskId AND OperationId = $operationId;
            """,
            ("$taskId", task.Id.Value),
            ("$operationId", operation.OperationId.Value));

        await Assert.ThrowsAsync<TaskDataCorruptionException>(() =>
            repository.GetByIdAsync(task.Id));
    }

    [Fact]
    public async System.Threading.Tasks.Task Persisted_history_cannot_bypass_domain_transition_rules()
    {
        await using var database = await TestDatabase.CreateAsync();
        var task = CreateTask();
        var repository = await database.CreateInitializedRepositoryAsync();
        await repository.AddAsync(task);
        var operation = CreateOperation(task, "operation-invalid-history", TaskState.Ready);
        await repository.ApplyTransitionAsync(operation, CreatedAt.AddMinutes(1));
        await database.ExecuteSqlAsync(
            """
            DROP TRIGGER TaskOperations_NoUpdate;
            UPDATE TaskOperations
            SET TargetState = $state, ResultingState = $state
            WHERE TaskId = $taskId AND OperationId = $operationId;
            UPDATE Tasks SET State = $state WHERE TaskId = $taskId;
            """,
            ("$state", (int)TaskState.Accepted),
            ("$taskId", task.Id.Value),
            ("$operationId", operation.OperationId.Value));

        await Assert.ThrowsAsync<TaskDataCorruptionException>(() =>
            repository.GetByIdAsync(task.Id));
    }

    [Fact]
    public async System.Threading.Tasks.Task Repository_operations_require_successful_initialization_and_reject_disposal()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = database.CreateRepository();
        var task = CreateTask();
        var operation = CreateOperation(task, "operation-before-init", TaskState.Ready);
        var transition = CreateStateTransition(
            task.Id,
            "transition-before-init",
            TaskState.Proposed,
            TaskState.Ready,
            CreatedAt.AddMinutes(1),
            "Task is ready.");
        var attempt = new TaskAttempt(
            new AttemptId("attempt-before-init"),
            task.Id,
            1,
            TaskAttemptState.Created,
            CreatedAt.AddMinutes(1));
        var evidence = CreateEvidence("evidence-before-init", task.Id);

        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.GetByIdAsync(task.Id));
        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.AddAsync(task));
        await Assert.ThrowsAsync<TaskPersistenceException>(() =>
            repository.ApplyTransitionAsync(operation, CreatedAt.AddMinutes(1)));
        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.AppendAttemptAsync(attempt));
        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.GetAttemptsAsync(task.Id));
        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.GetStateTransitionsAsync(task.Id));
        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.AddEvidenceAsync(evidence));
        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.GetEvidenceAsync(task.Id));
        await Assert.ThrowsAsync<TaskPersistenceException>(() =>
            repository.GetEvidenceForAttemptAsync(task.Id, attempt.Id));
        await Assert.ThrowsAsync<TaskPersistenceException>(() =>
            repository.ApplyTransitionWithHistoryAsync(operation, transition, attempt));

        await repository.InitializeAsync();
        await repository.AddAsync(task);
        await repository.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => repository.GetByIdAsync(task.Id));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => repository.AddAsync(CreateTask()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            repository.ApplyTransitionAsync(operation, CreatedAt.AddMinutes(1)));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => repository.AppendAttemptAsync(attempt));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => repository.GetAttemptsAsync(task.Id));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => repository.GetStateTransitionsAsync(task.Id));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => repository.AddEvidenceAsync(evidence));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => repository.GetEvidenceAsync(task.Id));
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            repository.GetEvidenceForAttemptAsync(task.Id, attempt.Id));
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            repository.ApplyTransitionWithHistoryAsync(operation, transition, attempt));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => repository.InitializeAsync());
    }

    [Fact]
    public async System.Threading.Tasks.Task Repeated_initialization_preserves_current_schema_and_data()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = await database.CreateInitializedRepositoryAsync();
        var task = CreateTask();
        await repository.AddAsync(task);
        var operation = CreateOperation(task, "operation-repeat-init", TaskState.Ready);
        await repository.ApplyTransitionAsync(operation, CreatedAt.AddMinutes(1));
        var appliedAt = await database.ExecuteScalarAsync<string>(
            "SELECT AppliedAt FROM PersistenceSchemaVersion WHERE Id = 1;");

        await repository.InitializeAsync();
        await repository.InitializeAsync();

        var restored = await repository.GetByIdAsync(task.Id);
        Assert.NotNull(restored);
        Assert.Equal(new TaskRevision(1), restored.Revision);
        Assert.Single(restored.AppliedOperations);
        Assert.Equal(appliedAt, await database.ExecuteScalarAsync<string>(
            "SELECT AppliedAt FROM PersistenceSchemaVersion WHERE Id = 1;"));
        Assert.Equal(4, await database.ExecuteScalarAsync<int>("PRAGMA user_version;"));
    }

    [Fact]
    public async System.Threading.Tasks.Task Legacy_v1_database_migrates_without_rewriting_task_history()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = await database.CreateInitializedRepositoryAsync();
        var task = CreateTask();
        await repository.AddAsync(task);
        var operation = CreateOperation(task, "operation-legacy-v1", TaskState.Ready);
        await repository.ApplyTransitionAsync(operation, CreatedAt.AddMinutes(1));
        await repository.DisposeAsync();
        await database.MarkAsLegacyVersionOneAsync();

        var upgradedRepository = await database.CreateInitializedRepositoryAsync();
        var restored = await upgradedRepository.GetByIdAsync(task.Id);

        Assert.NotNull(restored);
        Assert.Equal(TaskState.Ready, restored.State);
        Assert.Equal(new TaskRevision(1), restored.Revision);
        Assert.Equal([operation], restored.AppliedOperations);
        Assert.Equal(4, await database.ExecuteScalarAsync<int>(
            "SELECT Version FROM PersistenceSchemaVersion WHERE Id = 1;"));
    }

    [Fact]
    public async System.Threading.Tasks.Task Failed_migration_rolls_back_and_keeps_legacy_task_data()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = await database.CreateInitializedRepositoryAsync();
        var task = CreateTask();
        await repository.AddAsync(task);
        var operation = CreateOperation(task, "operation-migration-rollback", TaskState.Ready);
        await repository.ApplyTransitionAsync(operation, CreatedAt.AddMinutes(1));
        await repository.DisposeAsync();
        await database.MarkAsLegacyVersionOneAsync();

        var failingRepository = new SqliteTaskRepository(
            database.DatabasePath,
            (fromVersion, toVersion) =>
            {
                if (fromVersion == 1 && toVersion == 2)
                {
                    throw new InvalidOperationException("Simulated migration failure.");
                }
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() => failingRepository.InitializeAsync());
        await Assert.ThrowsAsync<TaskPersistenceException>(() => failingRepository.GetByIdAsync(task.Id));
        await Assert.ThrowsAsync<TaskPersistenceException>(() => failingRepository.InitializeAsync());
        Assert.Equal(1, await database.ExecuteScalarAsync<int>("PRAGMA user_version;"));
        Assert.Equal(0, await database.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_schema WHERE type = 'table' AND name = 'PersistenceSchemaVersion';"));
        Assert.Equal(1, await database.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Tasks;"));

        var recoveredRepository = await database.CreateInitializedRepositoryAsync();
        var restored = await recoveredRepository.GetByIdAsync(task.Id);
        Assert.NotNull(restored);
        Assert.Equal([operation], restored.AppliedOperations);
    }

    [Fact]
    public async System.Threading.Tasks.Task Failed_attempt_history_migration_rolls_back_without_changing_v2_data()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = await database.CreateInitializedRepositoryAsync();
        var task = CreateTask();
        await repository.AddAsync(task);
        var operation = CreateOperation(task, "operation-v2-migration", TaskState.Ready);
        await repository.ApplyTransitionAsync(operation, CreatedAt.AddMinutes(1));
        await repository.DisposeAsync();
        await database.MarkAsLegacyVersionTwoAsync();

        var failingRepository = new SqliteTaskRepository(
            database.DatabasePath,
            (fromVersion, toVersion) =>
            {
                if (fromVersion == 2 && toVersion == 3)
                {
                    throw new InvalidOperationException("Simulated attempt-history migration failure.");
                }
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() => failingRepository.InitializeAsync());
        Assert.Equal(2, await database.ExecuteScalarAsync<int>("PRAGMA user_version;"));
        Assert.Equal(2, await database.ExecuteScalarAsync<int>(
            "SELECT Version FROM PersistenceSchemaVersion WHERE Id = 1;"));
        Assert.Equal(0, await database.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_schema WHERE type = 'table' AND name = 'TaskAttempts';"));
        Assert.Equal(1, await database.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM TaskOperations;"));

        var recoveredRepository = await database.CreateInitializedRepositoryAsync();
        var restoredTask = await recoveredRepository.GetByIdAsync(task.Id);
        Assert.NotNull(restoredTask);
        Assert.Equal([operation], restoredTask.AppliedOperations);
        Assert.Equal(4, await database.ExecuteScalarAsync<int>("PRAGMA user_version;"));
    }

    [Fact]
    public async System.Threading.Tasks.Task Unsupported_future_schema_fails_closed_without_modifying_data()
    {
        await using var database = await TestDatabase.CreateAsync();
        var initializedRepository = await database.CreateInitializedRepositoryAsync();
        var task = CreateTask();
        await initializedRepository.AddAsync(task);
        await initializedRepository.DisposeAsync();
        await database.ExecuteSqlAsync(
            "UPDATE PersistenceSchemaVersion SET Version = 5 WHERE Id = 1;");
        var repository = database.CreateRepository();

        var exception = await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.InitializeAsync());

        Assert.Contains("newer", exception.Message, StringComparison.OrdinalIgnoreCase);
        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.GetByIdAsync(task.Id));
        Assert.Equal(5, await database.ExecuteScalarAsync<int>(
            "SELECT Version FROM PersistenceSchemaVersion WHERE Id = 1;"));
        Assert.Equal(1, await database.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Tasks;"));
    }

    [Fact]
    public async System.Threading.Tasks.Task Malformed_schema_version_fails_closed()
    {
        await using var database = await TestDatabase.CreateAsync();
        var initializedRepository = await database.CreateInitializedRepositoryAsync();
        await initializedRepository.DisposeAsync();
        await database.ExecuteSqlAsync(
            "UPDATE PersistenceSchemaVersion SET Version = 'invalid' WHERE Id = 1;");
        var repository = database.CreateRepository();

        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.InitializeAsync());
        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.GetByIdAsync(new TaskId("missing")));
        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.GetEvidenceAsync(new TaskId("missing")));
        Assert.Equal(1, await database.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM PersistenceSchemaVersion WHERE Version = 'invalid';"));
    }

    [Fact]
    public async System.Threading.Tasks.Task Concurrent_initialization_is_serialized_for_one_and_multiple_instances()
    {
        await using var database = await TestDatabase.CreateAsync();
        var migrationOrder = new ConcurrentQueue<(int FromVersion, int ToVersion)>();
        var repository = new SqliteTaskRepository(
            database.DatabasePath,
            (fromVersion, toVersion) => migrationOrder.Enqueue((fromVersion, toVersion)));

        await System.Threading.Tasks.Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => repository.InitializeAsync()));
        Assert.Equal([(0, 1), (1, 2), (2, 3), (3, 4)], migrationOrder.ToArray());

        var secondDatabase = await TestDatabase.CreateAsync();
        await using (secondDatabase)
        {
            var multipleInstanceMigrationOrder = new ConcurrentQueue<(int FromVersion, int ToVersion)>();
            var repositoryA = new SqliteTaskRepository(
                secondDatabase.DatabasePath,
                (fromVersion, toVersion) =>
                    multipleInstanceMigrationOrder.Enqueue((fromVersion, toVersion)));
            var repositoryB = new SqliteTaskRepository(
                secondDatabase.DatabasePath,
                (fromVersion, toVersion) =>
                    multipleInstanceMigrationOrder.Enqueue((fromVersion, toVersion)));

            await System.Threading.Tasks.Task.WhenAll(
                repositoryA.InitializeAsync(),
                repositoryB.InitializeAsync());

            Assert.Equal([(0, 1), (1, 2), (2, 3), (3, 4)], multipleInstanceMigrationOrder.ToArray());
            var task = CreateTask();
            await repositoryA.AddAsync(task);
            var sharedTask = await repositoryB.GetByIdAsync(task.Id);
            Assert.NotNull(sharedTask);
            Assert.Equal(task.Id, sharedTask.Id);
            Assert.Equal(task.State, sharedTask.State);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task Schema_validation_failure_prevents_repository_use()
    {
        await using var database = await TestDatabase.CreateAsync();
        var initializedRepository = await database.CreateInitializedRepositoryAsync();
        await initializedRepository.DisposeAsync();
        await database.ExecuteSqlAsync("DROP TRIGGER TaskOperations_NoDelete;");
        var repository = database.CreateRepository();

        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.InitializeAsync());
        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.GetByIdAsync(new TaskId("missing")));
    }

    [Fact]
    public async System.Threading.Tasks.Task Evidence_schema_validation_requires_immutable_history_triggers()
    {
        await using var database = await TestDatabase.CreateAsync();
        var initializedRepository = await database.CreateInitializedRepositoryAsync();
        await initializedRepository.AddAsync(CreateTask());
        await initializedRepository.DisposeAsync();
        await database.ExecuteSqlAsync("DROP TRIGGER EvidenceRecords_NoDelete;");
        var repository = database.CreateRepository();

        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.InitializeAsync());
        await Assert.ThrowsAsync<TaskPersistenceException>(() =>
            repository.GetEvidenceAsync(new TaskId("missing")));
        Assert.Equal(1, await database.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Tasks;"));
    }

    [Fact]
    public async System.Threading.Tasks.Task Corrupt_database_fails_integrity_validation_without_recreation()
    {
        await using var database = await TestDatabase.CreateAsync();
        var initializedRepository = await database.CreateInitializedRepositoryAsync();
        await initializedRepository.DisposeAsync();
        const string corruptedContent = "not a SQLite database";
        await File.WriteAllTextAsync(database.DatabasePath, corruptedContent);
        var repository = database.CreateRepository();

        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.InitializeAsync());
        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.GetByIdAsync(new TaskId("missing")));
        Assert.Equal(corruptedContent, await File.ReadAllTextAsync(database.DatabasePath));
    }

    [Fact]
    public async System.Threading.Tasks.Task Invalid_database_location_fails_without_creating_directories()
    {
        await using var database = await TestDatabase.CreateAsync();
        var repository = new SqliteTaskRepository(database.DirectoryPath);

        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.InitializeAsync());
        Assert.True(Directory.Exists(database.DirectoryPath));
        Assert.False(File.Exists(database.DatabasePath));
    }

    private static DomainTask CreateTask(
        IEnumerable<TaskId>? dependencies = null,
        TaskScope? scope = null,
        IEnumerable<AcceptanceCriterion>? acceptanceCriteria = null,
        IEnumerable<StopCondition>? stopConditions = null,
        ChangeBudget? changeBudget = null,
        TaskState state = TaskState.Proposed,
        TaskRevision? revision = null,
        TaskId? taskId = null) =>
        new(
            taskId ?? new TaskId("task-persistence"),
            "Persist task state",
            "Store all domain data and lifecycle history.",
            dependencies ?? [],
            scope ?? new TaskScope(["src"]),
            acceptanceCriteria ?? [],
            stopConditions ?? [],
            changeBudget ?? new ChangeBudget(1, 2, 3, 4, 5),
            state,
            CreatedAt,
            revision ?? new TaskRevision(0));

    private static EvidenceRecord CreateEvidence(
        string evidenceId,
        TaskId taskId,
        AttemptId? attemptId = null,
        string summary = "Build completed successfully.") =>
        new(
            new EvidenceId(evidenceId),
            taskId,
            EvidenceType.Build,
            EvidenceSource.Verifier,
            summary,
            CreatedAt.AddMinutes(2),
            attemptId,
            new EvidenceReference("build-result"));

    private static TaskTransitionOperation CreateOperation(
        DomainTask task,
        string operationId,
        TaskState targetState) =>
        new(task.Id, new OperationId(operationId), task.Revision, targetState);

    private static StateTransition CreateStateTransition(
        TaskId taskId,
        string transitionId,
        TaskState previousState,
        TaskState nextState,
        DateTimeOffset timestamp,
        string reason) =>
        new(new TransitionId(transitionId), taskId, previousState, nextState, timestamp, reason);

    private static async System.Threading.Tasks.Task<Exception?> CaptureAsync(
        Func<System.Threading.Tasks.Task> operation)
    {
        try
        {
            await operation();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string _directoryPath;

        private TestDatabase()
        {
            _directoryPath = Path.Combine(
                Path.GetTempPath(),
                "AiGameStudioWorkstation.Infrastructure.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directoryPath);
            DatabasePath = Path.Combine(_directoryPath, "tasks.db");
        }

        public string DatabasePath { get; }
        public string DirectoryPath => _directoryPath;

        public static System.Threading.Tasks.Task<TestDatabase> CreateAsync() =>
            System.Threading.Tasks.Task.FromResult(new TestDatabase());

        public SqliteTaskRepository CreateRepository() => new(DatabasePath);

        public async System.Threading.Tasks.Task<SqliteTaskRepository> CreateInitializedRepositoryAsync()
        {
            var repository = CreateRepository();
            await repository.InitializeAsync();
            return repository;
        }

        public async System.Threading.Tasks.Task MarkAsLegacyVersionOneAsync() =>
            await ExecuteSqlAsync("""
                DROP TRIGGER EvidenceRecords_NoUpdate;
                DROP TRIGGER EvidenceRecords_NoDelete;
                DROP TABLE EvidenceRecords;
                DROP TABLE StateTransitions;
                DROP TABLE TaskAttemptSnapshots;
                DROP TABLE TaskAttempts;
                DROP TABLE PersistenceSchemaVersion;
                PRAGMA user_version = 1;
                """);

        public async System.Threading.Tasks.Task MarkAsLegacyVersionTwoAsync() =>
            await ExecuteSqlAsync("""
                DROP TRIGGER EvidenceRecords_NoUpdate;
                DROP TRIGGER EvidenceRecords_NoDelete;
                DROP TABLE EvidenceRecords;
                DROP TABLE StateTransitions;
                DROP TABLE TaskAttemptSnapshots;
                DROP TABLE TaskAttempts;
                UPDATE PersistenceSchemaVersion SET Version = 2 WHERE Id = 1;
                PRAGMA user_version = 2;
                """);

        public async System.Threading.Tasks.Task MarkAsLegacyVersionThreeAsync() =>
            await ExecuteSqlAsync("""
                DROP TRIGGER EvidenceRecords_NoUpdate;
                DROP TRIGGER EvidenceRecords_NoDelete;
                DROP TABLE EvidenceRecords;
                UPDATE PersistenceSchemaVersion SET Version = 3 WHERE Id = 1;
                PRAGMA user_version = 3;
                """);

        public async System.Threading.Tasks.Task<T> ExecuteScalarAsync<T>(string sql)
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                ForeignKeys = true,
                Pooling = false
            }.ToString();
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var value = await command.ExecuteScalarAsync();
            return (T)Convert.ChangeType(value!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
        }

        public async System.Threading.Tasks.Task ExecuteSqlAsync(
            string sql,
            params (string Name, object Value)[] parameters)
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                ForeignKeys = true,
                Pooling = false
            }.ToString();
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }

            await command.ExecuteNonQueryAsync();
        }

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(_directoryPath))
            {
                Directory.Delete(_directoryPath, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }
}
