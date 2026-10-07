using AiGameStudioWorkstation.Application.Tasks.Persistence;
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
        var repository = database.CreateRepository();
        var task = CreateTask(
            dependencies: [new TaskId("foundation-004")],
            scope: new TaskScope(["src/Core", "tests/Core.Tests"]),
            acceptanceCriteria: [new AcceptanceCriterion("AC-001", "State is durable.", true)],
            stopConditions: [new StopCondition("SC-001", "Stop on corruption.", false)],
            changeBudget: new ChangeBudget(3, 2, 5, 1, 1));

        await repository.AddAsync(task);
        var restored = await database.CreateRepository().GetByIdAsync(task.Id);

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
        await database.CreateRepository().AddAsync(initialTask);
        var operation = CreateOperation(initialTask, "operation-durable", TaskState.Ready);
        operation = new TaskTransitionOperation(
            operation.TaskId,
            operation.OperationId,
            operation.ExpectedRevision,
            operation.TargetState,
            new CorrelationId("correlation-durable"));
        var appliedAt = CreatedAt.AddMinutes(1);

        var result = await database.CreateRepository().ApplyTransitionAsync(operation, appliedAt);
        var restartedRepository = database.CreateRepository();
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
    public async System.Threading.Tasks.Task Competing_operations_cannot_overwrite_the_same_revision()
    {
        await using var database = await TestDatabase.CreateAsync();
        var task = CreateTask();
        await database.CreateRepository().AddAsync(task);
        var first = CreateOperation(task, "operation-ready", TaskState.Ready);
        var second = CreateOperation(task, "operation-cancel", TaskState.Cancelled);

        var results = await System.Threading.Tasks.Task.WhenAll(
            database.CreateRepository().ApplyTransitionAsync(first, CreatedAt.AddMinutes(1)),
            database.CreateRepository().ApplyTransitionAsync(second, CreatedAt.AddMinutes(2)));

        Assert.Single(results, result => result.Status == TaskOperationStatus.Applied);
        Assert.Single(results, result =>
            result.Status == TaskOperationStatus.Rejected
            && result.Rejection == TaskOperationRejection.StaleRevision);
        var persisted = await database.CreateRepository().GetByIdAsync(task.Id);
        Assert.NotNull(persisted);
        Assert.Equal(new TaskRevision(1), persisted.Revision);
        Assert.Single(persisted.AppliedOperations);
    }

    [Fact]
    public async System.Threading.Tasks.Task Concurrent_duplicate_operation_commits_only_once()
    {
        await using var database = await TestDatabase.CreateAsync();
        var task = CreateTask();
        await database.CreateRepository().AddAsync(task);
        var operation = CreateOperation(task, "operation-race", TaskState.Ready);

        var results = await System.Threading.Tasks.Task.WhenAll(
            database.CreateRepository().ApplyTransitionAsync(operation, CreatedAt.AddMinutes(1)),
            database.CreateRepository().ApplyTransitionAsync(operation, CreatedAt.AddMinutes(2)));

        Assert.Single(results, result => result.Status == TaskOperationStatus.Applied);
        Assert.Single(results, result => result.Status == TaskOperationStatus.AlreadyApplied);
        var persisted = await database.CreateRepository().GetByIdAsync(task.Id);
        Assert.NotNull(persisted);
        Assert.Equal(new TaskRevision(1), persisted.Revision);
        Assert.Single(persisted.AppliedOperations);
    }

    [Fact]
    public async System.Threading.Tasks.Task Rejected_transition_does_not_write_a_task_or_operation_change()
    {
        await using var database = await TestDatabase.CreateAsync();
        var task = CreateTask(state: TaskState.InProgress, revision: new TaskRevision(4));
        await database.CreateRepository().AddAsync(task);
        var invalidOperation = CreateOperation(task, "operation-invalid", TaskState.Accepted);

        var result = await database.CreateRepository().ApplyTransitionAsync(
            invalidOperation,
            CreatedAt.AddMinutes(1));
        var persisted = await database.CreateRepository().GetByIdAsync(task.Id);

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
        await database.CreateRepository().AddAsync(task);
        await database.ExecuteSqlAsync("""
            CREATE TRIGGER FailTaskOperationInsert
            BEFORE INSERT ON TaskOperations
            BEGIN
                SELECT RAISE(ABORT, 'simulated operation write failure');
            END;
            """);
        var operation = CreateOperation(task, "operation-rollback", TaskState.Ready);

        await Assert.ThrowsAsync<TaskPersistenceException>(() =>
            database.CreateRepository().ApplyTransitionAsync(operation, CreatedAt.AddMinutes(1)));

        var persisted = await database.CreateRepository().GetByIdAsync(task.Id);
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
        await database.CreateRepository().AddAsync(task);
        await database.ExecuteSqlAsync(
            "UPDATE Tasks SET DependenciesJson = 'not-json' WHERE TaskId = $taskId;",
            ("$taskId", task.Id.Value));

        await Assert.ThrowsAsync<TaskDataCorruptionException>(() =>
            database.CreateRepository().GetByIdAsync(task.Id));
    }

    [Fact]
    public async System.Threading.Tasks.Task Operation_history_rows_cannot_be_updated_or_deleted()
    {
        await using var database = await TestDatabase.CreateAsync();
        var task = CreateTask();
        await database.CreateRepository().AddAsync(task);
        var operation = CreateOperation(task, "operation-immutable", TaskState.Ready);
        await database.CreateRepository().ApplyTransitionAsync(operation, CreatedAt.AddMinutes(1));

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
        await database.CreateRepository().AddAsync(task);
        var operation = CreateOperation(task, "operation-corrupt", TaskState.Ready);
        await database.CreateRepository().ApplyTransitionAsync(operation, CreatedAt.AddMinutes(1));
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
            database.CreateRepository().GetByIdAsync(task.Id));
    }

    [Fact]
    public async System.Threading.Tasks.Task Persisted_history_cannot_bypass_domain_transition_rules()
    {
        await using var database = await TestDatabase.CreateAsync();
        var task = CreateTask();
        await database.CreateRepository().AddAsync(task);
        var operation = CreateOperation(task, "operation-invalid-history", TaskState.Ready);
        await database.CreateRepository().ApplyTransitionAsync(operation, CreatedAt.AddMinutes(1));
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
            database.CreateRepository().GetByIdAsync(task.Id));
    }

    private static DomainTask CreateTask(
        IEnumerable<TaskId>? dependencies = null,
        TaskScope? scope = null,
        IEnumerable<AcceptanceCriterion>? acceptanceCriteria = null,
        IEnumerable<StopCondition>? stopConditions = null,
        ChangeBudget? changeBudget = null,
        TaskState state = TaskState.Proposed,
        TaskRevision? revision = null) =>
        new(
            new TaskId("task-persistence"),
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

    private static TaskTransitionOperation CreateOperation(
        DomainTask task,
        string operationId,
        TaskState targetState) =>
        new(task.Id, new OperationId(operationId), task.Revision, targetState);

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

        public static async System.Threading.Tasks.Task<TestDatabase> CreateAsync()
        {
            var database = new TestDatabase();
            await database.CreateRepository().InitializeAsync();
            await database.CreateRepository().InitializeAsync();
            return database;
        }

        public SqliteTaskRepository CreateRepository() => new(DatabasePath);

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
