using System.Collections.Concurrent;
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

        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.GetByIdAsync(task.Id));
        await Assert.ThrowsAsync<TaskPersistenceException>(() => repository.AddAsync(task));
        await Assert.ThrowsAsync<TaskPersistenceException>(() =>
            repository.ApplyTransitionAsync(operation, CreatedAt.AddMinutes(1)));

        await repository.InitializeAsync();
        await repository.AddAsync(task);
        await repository.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => repository.GetByIdAsync(task.Id));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => repository.AddAsync(CreateTask()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            repository.ApplyTransitionAsync(operation, CreatedAt.AddMinutes(1)));
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
        Assert.Equal(2, await database.ExecuteScalarAsync<int>("PRAGMA user_version;"));
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
        Assert.Equal(2, await database.ExecuteScalarAsync<int>(
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
        Assert.Equal([(0, 1), (1, 2)], migrationOrder.ToArray());

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

            Assert.Equal([(0, 1), (1, 2)], multipleInstanceMigrationOrder.ToArray());
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
                DROP TABLE PersistenceSchemaVersion;
                PRAGMA user_version = 1;
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
