using System.Globalization;
using System.Text.Json;
using AiGameStudioWorkstation.Application.Tasks.Persistence;
using AiGameStudioWorkstation.Core.Domain.Tasks;
using Microsoft.Data.Sqlite;
using AsyncTask = System.Threading.Tasks.Task;
using DomainTask = AiGameStudioWorkstation.Core.Domain.Tasks.Task;

namespace AiGameStudioWorkstation.Infrastructure.Persistence.Sqlite;

public sealed class SqliteTaskRepository : ITaskRepository
{
    private const int SchemaVersion = 1;
    private const int MaximumTaskStateValue = (int)TaskState.HumanReviewRequired;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _connectionString;

    public SqliteTaskRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        if (string.Equals(databasePath, ":memory:", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A file-backed database path is required so repository operations can survive connection restarts.",
                nameof(databasePath));
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = false
        };
        _connectionString = builder.ToString();
    }

    public async AsyncTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using (var journalCommand = connection.CreateCommand())
            {
                journalCommand.CommandText = "PRAGMA journal_mode = WAL;";
                await journalCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            using var transaction = connection.BeginTransaction(deferred: false);
            var version = await ReadSchemaVersionAsync(connection, transaction, cancellationToken);
            if (version > SchemaVersion)
            {
                throw new TaskPersistenceException(
                    $"Database schema version {version} is newer than the supported version {SchemaVersion}.");
            }

            if (version == 0)
            {
                await CreateSchemaAsync(connection, transaction, cancellationToken);
                await SetSchemaVersionAsync(connection, transaction, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (SqliteException exception)
        {
            throw new TaskPersistenceException("Failed to initialize the SQLite task schema.", exception);
        }
    }

    public async AsyncTask AddAsync(DomainTask task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (task.AppliedOperations.Count != 0 || task.UpdatedAt != task.CreatedAt)
        {
            throw new ArgumentException(
                "A new persisted task must not contain transitions applied before it was added.",
                nameof(task));
        }

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            using var transaction = connection.BeginTransaction(deferred: false);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Tasks (
                    TaskId, Intent, Description, DependenciesJson, ScopeEntriesJson,
                    AcceptanceCriteriaJson, StopConditionsJson,
                    MaxProductionFilesChanged, MaxTestFilesChanged, MaxTotalFilesChanged,
                    MaxDependencyAdditions, MaxFileDeletions,
                    InitialState, InitialRevision, State, Revision, CreatedAt, UpdatedAt)
                VALUES (
                    $taskId, $intent, $description, $dependencies, $scope,
                    $acceptanceCriteria, $stopConditions,
                    $maxProductionFilesChanged, $maxTestFilesChanged, $maxTotalFilesChanged,
                    $maxDependencyAdditions, $maxFileDeletions,
                    $initialState, $initialRevision, $state, $revision, $createdAt, $updatedAt);
                """;
            AddTaskParameters(command, task);
            command.Parameters.AddWithValue("$initialState", (int)task.State);
            command.Parameters.AddWithValue("$initialRevision", task.Revision.Value);
            command.Parameters.AddWithValue("$state", (int)task.State);
            command.Parameters.AddWithValue("$revision", task.Revision.Value);
            command.Parameters.AddWithValue("$createdAt", FormatTimestamp(task.CreatedAt));
            command.Parameters.AddWithValue("$updatedAt", FormatTimestamp(task.UpdatedAt));
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (SqliteException exception)
        {
            throw new TaskPersistenceException($"Failed to add task '{task.Id.Value}'.", exception);
        }
    }

    public async System.Threading.Tasks.Task<DomainTask?> GetByIdAsync(
        TaskId taskId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taskId);

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            using var transaction = connection.BeginTransaction(deferred: true);
            var task = await LoadTaskAsync(connection, transaction, taskId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return task;
        }
        catch (SqliteException exception)
        {
            throw new TaskPersistenceException($"Failed to load task '{taskId.Value}'.", exception);
        }
    }

    public async System.Threading.Tasks.Task<TaskOperationResult> ApplyTransitionAsync(
        TaskTransitionOperation operation,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            using var transaction = connection.BeginTransaction(deferred: false);
            var task = await LoadTaskAsync(
                connection,
                transaction,
                operation.TaskId,
                cancellationToken);

            if (task is null)
            {
                throw new TaskNotFoundException(operation.TaskId.Value);
            }

            var result = task.ApplyTransition(operation, updatedAt);
            if (result.Status != TaskOperationStatus.Applied)
            {
                await transaction.CommitAsync(cancellationToken);
                return result;
            }

            if (updatedAt < task.CreatedAt || updatedAt < task.UpdatedAt)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(updatedAt),
                    updatedAt,
                    "A persisted task update cannot precede its creation or previous update.");
            }

            await UpdateTaskAsync(connection, transaction, task, result.Task, cancellationToken);
            await InsertOperationAsync(connection, transaction, task, operation, result.Task, updatedAt, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (SqliteException exception)
        {
            throw new TaskPersistenceException(
                $"Failed to apply operation '{operation.OperationId.Value}' to task '{operation.TaskId.Value}'.",
                exception);
        }
    }

    private async System.Threading.Tasks.Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async System.Threading.Tasks.Task<int> ReadSchemaVersionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA user_version;";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static async AsyncTask CreateSchemaAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            CREATE TABLE Tasks (
                TaskId TEXT NOT NULL PRIMARY KEY CHECK (length(trim(TaskId)) > 0),
                Intent TEXT NOT NULL,
                Description TEXT NOT NULL,
                DependenciesJson TEXT NOT NULL,
                ScopeEntriesJson TEXT NOT NULL,
                AcceptanceCriteriaJson TEXT NOT NULL,
                StopConditionsJson TEXT NOT NULL,
                MaxProductionFilesChanged INTEGER NOT NULL CHECK (MaxProductionFilesChanged >= 0),
                MaxTestFilesChanged INTEGER NOT NULL CHECK (MaxTestFilesChanged >= 0),
                MaxTotalFilesChanged INTEGER NOT NULL CHECK (MaxTotalFilesChanged >= 0),
                MaxDependencyAdditions INTEGER NOT NULL CHECK (MaxDependencyAdditions >= 0),
                MaxFileDeletions INTEGER NOT NULL CHECK (MaxFileDeletions >= 0),
                InitialState INTEGER NOT NULL CHECK (InitialState BETWEEN 0 AND {MaximumTaskStateValue}),
                InitialRevision INTEGER NOT NULL CHECK (InitialRevision >= 0),
                State INTEGER NOT NULL CHECK (State BETWEEN 0 AND {MaximumTaskStateValue}),
                Revision INTEGER NOT NULL CHECK (Revision >= 0),
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE TaskOperations (
                TaskId TEXT NOT NULL,
                OperationId TEXT NOT NULL CHECK (length(trim(OperationId)) > 0),
                CorrelationId TEXT NULL,
                ExpectedRevision INTEGER NOT NULL CHECK (ExpectedRevision >= 0),
                PreviousState INTEGER NOT NULL CHECK (PreviousState BETWEEN 0 AND {MaximumTaskStateValue}),
                TargetState INTEGER NOT NULL CHECK (TargetState BETWEEN 0 AND {MaximumTaskStateValue}),
                ResultingRevision INTEGER NOT NULL CHECK (ResultingRevision > 0),
                ResultingState INTEGER NOT NULL CHECK (ResultingState BETWEEN 0 AND {MaximumTaskStateValue}),
                AppliedAt TEXT NOT NULL,
                PRIMARY KEY (TaskId, OperationId),
                UNIQUE (TaskId, ResultingRevision),
                FOREIGN KEY (TaskId) REFERENCES Tasks (TaskId) ON DELETE RESTRICT,
                CHECK (ResultingRevision = ExpectedRevision + 1),
                CHECK (ResultingState = TargetState)
            );

            CREATE TRIGGER TaskOperations_NoUpdate
            BEFORE UPDATE ON TaskOperations
            BEGIN
                SELECT RAISE(ABORT, 'Task operation history is immutable.');
            END;

            CREATE TRIGGER TaskOperations_NoDelete
            BEFORE DELETE ON TaskOperations
            BEGIN
                SELECT RAISE(ABORT, 'Task operation history is immutable.');
            END;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async AsyncTask SetSchemaVersionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA user_version = {SchemaVersion};";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddTaskParameters(SqliteCommand command, DomainTask task)
    {
        command.Parameters.AddWithValue("$taskId", task.Id.Value);
        command.Parameters.AddWithValue("$intent", task.Intent);
        command.Parameters.AddWithValue("$description", task.Description);
        command.Parameters.AddWithValue(
            "$dependencies",
            JsonSerializer.Serialize(task.Dependencies.Select(dependency => dependency.Value), JsonOptions));
        command.Parameters.AddWithValue(
            "$scope",
            JsonSerializer.Serialize(task.Scope.Entries, JsonOptions));
        command.Parameters.AddWithValue(
            "$acceptanceCriteria",
            JsonSerializer.Serialize(
                task.AcceptanceCriteria.Select(criterion => new AcceptanceCriterionData(
                    criterion.Id,
                    criterion.Description,
                    criterion.IsMandatory)),
                JsonOptions));
        command.Parameters.AddWithValue(
            "$stopConditions",
            JsonSerializer.Serialize(
                task.StopConditions.Select(condition => new StopConditionData(
                    condition.Id,
                    condition.Description,
                    condition.IsMandatory)),
                JsonOptions));
        command.Parameters.AddWithValue("$maxProductionFilesChanged", task.ChangeBudget.MaxProductionFilesChanged);
        command.Parameters.AddWithValue("$maxTestFilesChanged", task.ChangeBudget.MaxTestFilesChanged);
        command.Parameters.AddWithValue("$maxTotalFilesChanged", task.ChangeBudget.MaxTotalFilesChanged);
        command.Parameters.AddWithValue("$maxDependencyAdditions", task.ChangeBudget.MaxDependencyAdditions);
        command.Parameters.AddWithValue("$maxFileDeletions", task.ChangeBudget.MaxFileDeletions);
    }

    private static async System.Threading.Tasks.Task<DomainTask?> LoadTaskAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        TaskRow? row;
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                SELECT TaskId, Intent, Description, DependenciesJson, ScopeEntriesJson,
                       AcceptanceCriteriaJson, StopConditionsJson,
                       MaxProductionFilesChanged, MaxTestFilesChanged, MaxTotalFilesChanged,
                       MaxDependencyAdditions, MaxFileDeletions,
                       InitialState, InitialRevision, State, Revision, CreatedAt, UpdatedAt
                FROM Tasks
                WHERE TaskId = $taskId;
                """;
            command.Parameters.AddWithValue("$taskId", taskId.Value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            row = await reader.ReadAsync(cancellationToken) ? ReadTaskRow(reader) : null;
        }

        if (row is null)
        {
            return null;
        }

        var operationRows = await LoadOperationsAsync(connection, transaction, taskId, cancellationToken);
        return ReconstructTask(row, operationRows);
    }

    private static TaskRow ReadTaskRow(SqliteDataReader reader)
    {
        try
        {
            return new TaskRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetInt32(7),
                reader.GetInt32(8),
                reader.GetInt32(9),
                reader.GetInt32(10),
                reader.GetInt32(11),
                reader.GetInt32(12),
                reader.GetInt32(13),
                reader.GetInt32(14),
                reader.GetInt32(15),
                reader.GetString(16),
                reader.GetString(17));
        }
        catch (Exception exception) when (
            exception is InvalidCastException
            or InvalidOperationException
            or OverflowException)
        {
            throw new TaskDataCorruptionException("The persisted task row contains invalid field values.", exception);
        }
    }

    private static async System.Threading.Tasks.Task<IReadOnlyList<OperationRow>> LoadOperationsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var operations = new List<OperationRow>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT OperationId, CorrelationId, ExpectedRevision, PreviousState, TargetState,
                   ResultingRevision, ResultingState, AppliedAt
            FROM TaskOperations
            WHERE TaskId = $taskId
            ORDER BY ResultingRevision;
            """;
        command.Parameters.AddWithValue("$taskId", taskId.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            try
            {
                operations.Add(ReadOperationRow(taskId, reader));
            }
            catch (TaskDataCorruptionException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is ArgumentException
                or FormatException
                or InvalidCastException
                or InvalidOperationException
                or OverflowException)
            {
                throw new TaskDataCorruptionException(
                    $"Task '{taskId.Value}' contains an invalid operation record.",
                    exception);
            }
        }

        return operations;
    }

    private static OperationRow ReadOperationRow(TaskId taskId, SqliteDataReader reader)
    {
        var operationId = new OperationId(reader.GetString(0));
        var correlationId = reader.IsDBNull(1) ? null : new CorrelationId(reader.GetString(1));
        var expectedRevision = new TaskRevision(reader.GetInt32(2));
        var previousState = ReadState(reader.GetInt32(3), "PreviousState");
        var targetState = ReadState(reader.GetInt32(4), "TargetState");
        var resultingRevision = reader.GetInt32(5);
        var resultingState = ReadState(reader.GetInt32(6), "ResultingState");
        var appliedAt = ParseTimestamp(reader.GetString(7), "AppliedAt");

        if (expectedRevision.Value == int.MaxValue
            || resultingRevision != expectedRevision.Value + 1
            || targetState != resultingState)
        {
            throw new TaskDataCorruptionException(
                $"Operation '{operationId.Value}' has an inconsistent result record.",
                new InvalidDataException("The stored operation result does not follow its request."));
        }

        return new OperationRow(
            new TaskTransitionOperation(taskId, operationId, expectedRevision, targetState, correlationId),
            previousState,
            new TaskRevision(resultingRevision),
            resultingState,
            appliedAt);
    }

    private static DomainTask ReconstructTask(TaskRow row, IReadOnlyList<OperationRow> operationRows)
    {
        try
        {
            var taskId = new TaskId(row.TaskId);
            var createdAt = ParseTimestamp(row.CreatedAt, "CreatedAt");
            var updatedAt = ParseTimestamp(row.UpdatedAt, "UpdatedAt");
            var initialState = ReadState(row.InitialState, "InitialState");
            var initialRevision = new TaskRevision(row.InitialRevision);
            var state = ReadState(row.State, "State");
            var revision = new TaskRevision(row.Revision);

            if (updatedAt < createdAt)
            {
                throw new TaskDataCorruptionException(
                    $"Task '{row.TaskId}' has an update timestamp earlier than its creation timestamp.",
                    new InvalidDataException("UpdatedAt precedes CreatedAt."));
            }

            var dependencies = Deserialize<string>(row.DependenciesJson, "DependenciesJson")
                .Select(value => new TaskId(value))
                .ToArray();
            var scopeEntries = Deserialize<string>(row.ScopeEntriesJson, "ScopeEntriesJson");
            var criterionData = Deserialize<AcceptanceCriterionData>(
                row.AcceptanceCriteriaJson,
                "AcceptanceCriteriaJson");
            if (criterionData.Any(value => value is null))
            {
                throw new TaskDataCorruptionException(
                    $"Task '{row.TaskId}' contains a null acceptance criterion.",
                    new InvalidDataException("Acceptance criteria cannot contain null entries."));
            }

            var stopConditionData = Deserialize<StopConditionData>(
                row.StopConditionsJson,
                "StopConditionsJson");
            if (stopConditionData.Any(value => value is null))
            {
                throw new TaskDataCorruptionException(
                    $"Task '{row.TaskId}' contains a null stop condition.",
                    new InvalidDataException("Stop conditions cannot contain null entries."));
            }

            var criteria = criterionData
                .Select(value => new AcceptanceCriterion(value.Id, value.Description, value.IsMandatory))
                .ToArray();
            var stopConditions = stopConditionData
                .Select(value => new StopCondition(value.Id, value.Description, value.IsMandatory))
                .ToArray();

            var initialTask = new DomainTask(
                taskId,
                row.Intent,
                row.Description,
                dependencies,
                new TaskScope(scopeEntries),
                criteria,
                stopConditions,
                new ChangeBudget(
                    row.MaxProductionFilesChanged,
                    row.MaxTestFilesChanged,
                    row.MaxTotalFilesChanged,
                    row.MaxDependencyAdditions,
                    row.MaxFileDeletions),
                initialState,
                createdAt,
                initialRevision);

            return ValidateHistory(
                row.TaskId,
                initialTask,
                state,
                revision,
                updatedAt,
                operationRows);
        }
        catch (TaskDataCorruptionException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or JsonException
            or FormatException
            or InvalidCastException
            or OverflowException)
        {
            throw new TaskDataCorruptionException(
                $"Task '{row.TaskId}' contains invalid persisted data.",
                exception);
        }
    }

    private static DomainTask ValidateHistory(
        string taskId,
        DomainTask task,
        TaskState currentState,
        TaskRevision currentRevision,
        DateTimeOffset currentUpdatedAt,
        IReadOnlyList<OperationRow> operations)
    {
        foreach (var operation in operations)
        {
            if (operation.Operation.ExpectedRevision != task.Revision
                || operation.PreviousState != task.State
                || operation.AppliedAt < task.UpdatedAt)
            {
                throw new TaskDataCorruptionException(
                    $"Task '{taskId}' has an inconsistent operation history.",
                    new InvalidDataException("The stored operation history does not match the task revision."));
            }

            var result = task.ApplyTransition(operation.Operation, operation.AppliedAt);
            if (result.Status != TaskOperationStatus.Applied
                || result.Task.State != operation.ResultingState
                || result.Task.Revision != operation.ResultingRevision)
            {
                throw new TaskDataCorruptionException(
                    $"Task '{taskId}' contains a transition rejected by the domain.",
                    new InvalidDataException("The stored operation is not a valid domain transition."));
            }

            task = result.Task;
        }

        if (currentState != task.State
            || currentRevision != task.Revision
            || currentUpdatedAt != task.UpdatedAt)
        {
            throw new TaskDataCorruptionException(
                $"Task '{taskId}' does not match its immutable operation history.",
                new InvalidDataException("The task snapshot differs from the final operation record."));
        }

        return task;
    }

    private static T[] Deserialize<T>(string json, string columnName)
    {
        var values = JsonSerializer.Deserialize<T[]>(json, JsonOptions);
        return values
            ?? throw new TaskDataCorruptionException(
                $"Persisted column '{columnName}' does not contain an array.",
                new JsonException("A persisted collection was null."));
    }

    private static TaskState ReadState(int value, string columnName)
    {
        if (!Enum.IsDefined(typeof(TaskState), value))
        {
            throw new TaskDataCorruptionException(
                $"Persisted column '{columnName}' contains an unknown task state.",
                new InvalidDataException($"Unknown task state value: {value}."));
        }

        return (TaskState)value;
    }

    private static DateTimeOffset ParseTimestamp(string value, string columnName)
    {
        try
        {
            return DateTimeOffset.ParseExact(
                value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None);
        }
        catch (FormatException exception)
        {
            throw new TaskDataCorruptionException(
                $"Persisted column '{columnName}' contains an invalid timestamp.",
                exception);
        }
    }

    private static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToString("O", CultureInfo.InvariantCulture);

    private static async AsyncTask UpdateTaskAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DomainTask currentTask,
        DomainTask updatedTask,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE Tasks
            SET State = $state, Revision = $revision, UpdatedAt = $updatedAt
            WHERE TaskId = $taskId AND Revision = $expectedRevision;
            """;
        command.Parameters.AddWithValue("$state", (int)updatedTask.State);
        command.Parameters.AddWithValue("$revision", updatedTask.Revision.Value);
        command.Parameters.AddWithValue("$updatedAt", FormatTimestamp(updatedTask.UpdatedAt));
        command.Parameters.AddWithValue("$taskId", currentTask.Id.Value);
        command.Parameters.AddWithValue("$expectedRevision", currentTask.Revision.Value);

        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new TaskConcurrencyException(
                $"Task '{currentTask.Id.Value}' changed while operation persistence was in progress.");
        }
    }

    private static async AsyncTask InsertOperationAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DomainTask currentTask,
        TaskTransitionOperation operation,
        DomainTask updatedTask,
        DateTimeOffset appliedAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO TaskOperations (
                TaskId, OperationId, CorrelationId, ExpectedRevision, PreviousState,
                TargetState, ResultingRevision, ResultingState, AppliedAt)
            VALUES (
                $taskId, $operationId, $correlationId, $expectedRevision, $previousState,
                $targetState, $resultingRevision, $resultingState, $appliedAt);
            """;
        command.Parameters.AddWithValue("$taskId", operation.TaskId.Value);
        command.Parameters.AddWithValue("$operationId", operation.OperationId.Value);
        command.Parameters.AddWithValue(
            "$correlationId",
            (object?)operation.CorrelationId?.Value ?? DBNull.Value);
        command.Parameters.AddWithValue("$expectedRevision", operation.ExpectedRevision.Value);
        command.Parameters.AddWithValue("$previousState", (int)currentTask.State);
        command.Parameters.AddWithValue("$targetState", (int)operation.TargetState);
        command.Parameters.AddWithValue("$resultingRevision", updatedTask.Revision.Value);
        command.Parameters.AddWithValue("$resultingState", (int)updatedTask.State);
        command.Parameters.AddWithValue("$appliedAt", FormatTimestamp(appliedAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record TaskRow(
        string TaskId,
        string Intent,
        string Description,
        string DependenciesJson,
        string ScopeEntriesJson,
        string AcceptanceCriteriaJson,
        string StopConditionsJson,
        int MaxProductionFilesChanged,
        int MaxTestFilesChanged,
        int MaxTotalFilesChanged,
        int MaxDependencyAdditions,
        int MaxFileDeletions,
        int InitialState,
        int InitialRevision,
        int State,
        int Revision,
        string CreatedAt,
        string UpdatedAt);

    private sealed record OperationRow(
        TaskTransitionOperation Operation,
        TaskState PreviousState,
        TaskRevision ResultingRevision,
        TaskState ResultingState,
        DateTimeOffset AppliedAt);

    private sealed record AcceptanceCriterionData(string Id, string Description, bool IsMandatory);

    private sealed record StopConditionData(string Id, string Description, bool IsMandatory);
}
