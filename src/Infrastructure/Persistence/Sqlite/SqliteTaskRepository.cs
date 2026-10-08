using System.Globalization;
using System.Text.Json;
using AiGameStudioWorkstation.Application.Tasks.Persistence;
using AiGameStudioWorkstation.Core.Domain.Execution;
using AiGameStudioWorkstation.Core.Domain.Tasks;
using Microsoft.Data.Sqlite;
using AsyncTask = System.Threading.Tasks.Task;
using DomainTask = AiGameStudioWorkstation.Core.Domain.Tasks.Task;

namespace AiGameStudioWorkstation.Infrastructure.Persistence.Sqlite;

public sealed class SqliteTaskRepository : ITaskRepository, IDisposable, IAsyncDisposable
{
    private const int InitialSchemaVersion = 1;
    private const int SchemaVersionTwo = 2;
    private const int SchemaVersionThree = 3;
    private const int SchemaVersion = 4;
    private const string SchemaVersionTable = "PersistenceSchemaVersion";
    private const int MaximumTaskStateValue = (int)TaskState.HumanReviewRequired;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SchemaMigration[] Migrations =
    [
        new(0, 1, MigrateVersionZeroToOneAsync),
        new(1, 2, ApplySchemaVersionTwoAsync),
        new(2, 3, ApplySchemaVersionThreeAsync),
        new(3, 4, ApplySchemaVersionFourAsync)
    ];
    private readonly string _connectionString;
    private readonly string _databasePath;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly object _lifecycleLock = new();
    private readonly Action<int, int>? _migrationTestHook;
    private PersistenceState _state;

    public SqliteTaskRepository(string databasePath)
        : this(databasePath, migrationTestHook: null)
    {
    }

    internal SqliteTaskRepository(string databasePath, Action<int, int>? migrationTestHook)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        if (string.Equals(databasePath, ":memory:", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A file-backed database path is required so repository operations can survive connection restarts.",
                nameof(databasePath));
        }

        _databasePath = Path.GetFullPath(databasePath);
        _migrationTestHook = migrationTestHook;
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = false
        };
        _connectionString = builder.ToString();
    }

    public async AsyncTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            lock (_lifecycleLock)
            {
                if (_state == PersistenceState.Disposed)
                {
                    throw new ObjectDisposedException(nameof(SqliteTaskRepository));
                }

                if (_state == PersistenceState.Ready)
                {
                    return;
                }

                if (_state == PersistenceState.Failed)
                {
                    throw new TaskPersistenceException(
                        "Persistence initialization previously failed; this repository instance is unavailable.");
                }

                _state = PersistenceState.Initializing;
            }

            await ValidateDatabaseLocationAsync();
            await using var connection = await OpenConnectionAsync(cancellationToken);
            using var transaction = connection.BeginTransaction(deferred: false);
            var version = await ReadSchemaVersionAsync(connection, transaction, cancellationToken);
            if (version > SchemaVersion)
            {
                throw new TaskPersistenceException(
                    $"Database schema version {version} is newer than the supported version {SchemaVersion}.");
            }

            while (version < SchemaVersion)
            {
                var migration = Migrations.SingleOrDefault(candidate => candidate.FromVersion == version);
                if (migration is null || migration.ToVersion != version + 1)
                {
                    throw new TaskPersistenceException(
                        $"No ordered migration is defined from schema version {version}.");
                }

                if (version == InitialSchemaVersion)
                {
                    await ValidateVersionOneSchemaAsync(connection, transaction, cancellationToken);
                }

                await migration.Apply(connection, transaction, cancellationToken);
                _migrationTestHook?.Invoke(version, migration.ToVersion);
                version = migration.ToVersion;
            }

            await transaction.CommitAsync(cancellationToken);
            await ValidateCurrentSchemaAsync(connection, cancellationToken);

            await using (var journalCommand = connection.CreateCommand())
            {
                journalCommand.CommandText = "PRAGMA journal_mode = WAL;";
                await journalCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            lock (_lifecycleLock)
            {
                if (_state != PersistenceState.Disposed)
                {
                    _state = PersistenceState.Ready;
                }
            }
        }
        catch (SqliteException exception)
        {
            SetFailedUnlessDisposed();
            throw new TaskPersistenceException("Failed to initialize the SQLite task schema.", exception);
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException)
        {
            SetFailedUnlessDisposed();
            throw new TaskPersistenceException(
                "Failed to access the configured SQLite database location.",
                exception);
        }
        catch
        {
            SetFailedUnlessDisposed();
            throw;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async AsyncTask AddAsync(DomainTask task, CancellationToken cancellationToken = default)
    {
        EnsureReady();
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
        EnsureReady();
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
        EnsureReady();
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

    public async System.Threading.Tasks.Task<TaskOperationResult> ApplyTransitionWithHistoryAsync(
        TaskTransitionOperation operation,
        StateTransition transition,
        TaskAttempt? attempt = null,
        CancellationToken cancellationToken = default)
    {
        EnsureReady();
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(transition);

        if (transition.TaskId != operation.TaskId)
        {
            throw new ArgumentException(
                "The state transition must belong to the task being updated.",
                nameof(transition));
        }

        if (transition.NextState != operation.TargetState)
        {
            throw new ArgumentException(
                "The state transition target must match the task operation target.",
                nameof(transition));
        }

        if (attempt is not null && attempt.TaskId != operation.TaskId)
        {
            throw new ArgumentException(
                "An associated attempt must belong to the task being updated.",
                nameof(attempt));
        }

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            using var transaction = connection.BeginTransaction(deferred: false);
            var task = await LoadTaskAsync(connection, transaction, operation.TaskId, cancellationToken);
            if (task is null)
            {
                throw new TaskNotFoundException(operation.TaskId.Value);
            }

            var result = task.ApplyTransition(operation, transition.Timestamp);
            if (result.Status == TaskOperationStatus.AlreadyApplied)
            {
                var persistedTransition = await LoadStateTransitionForOperationAsync(
                    connection,
                    transaction,
                    operation.TaskId,
                    operation.OperationId,
                    cancellationToken);
                if (persistedTransition is null
                    || persistedTransition.Transition != transition
                    || persistedTransition.ExpectedRevision != operation.ExpectedRevision)
                {
                    throw new TaskPersistenceException(
                        $"Operation '{operation.OperationId.Value}' was previously applied without matching state-transition history.");
                }

                var existingSnapshotNumber = attempt is null
                    ? null
                    : await FindAttemptSnapshotAsync(
                        connection,
                        transaction,
                        attempt,
                        cancellationToken);
                if (persistedTransition.AssociatedAttemptId != attempt?.Id
                    || persistedTransition.AssociatedAttemptSnapshotNumber != existingSnapshotNumber)
                {
                    throw new TaskPersistenceException(
                        $"Operation '{operation.OperationId.Value}' was replayed with a different associated attempt snapshot.");
                }

                await transaction.CommitAsync(cancellationToken);
                return result;
            }

            if (result.Status == TaskOperationStatus.Rejected)
            {
                await transaction.CommitAsync(cancellationToken);
                return result;
            }

            if (transition.PreviousState != task.State)
            {
                throw new ArgumentException(
                    "The state transition previous state must match the persisted task state.",
                    nameof(transition));
            }

            if (transition.Timestamp < task.CreatedAt || transition.Timestamp < task.UpdatedAt)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(transition),
                    transition.Timestamp,
                    "A persisted task update cannot precede its creation or previous update.");
            }

            await UpdateTaskAsync(connection, transaction, task, result.Task, cancellationToken);
            await InsertOperationAsync(
                connection,
                transaction,
                task,
                operation,
                result.Task,
                transition.Timestamp,
                cancellationToken);

            var attemptSnapshotNumber = attempt is null
                ? (int?)null
                : await AppendAttemptSnapshotAsync(connection, transaction, attempt, cancellationToken);
            await InsertStateTransitionAsync(
                connection,
                transaction,
                operation,
                transition,
                result.Task.Revision,
                attempt,
                attemptSnapshotNumber,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (SqliteException exception)
        {
            throw new TaskPersistenceException(
                $"Failed to apply operation '{operation.OperationId.Value}' with state-transition history to task '{operation.TaskId.Value}'.",
                exception);
        }
    }

    public async System.Threading.Tasks.Task AppendAttemptAsync(
        TaskAttempt attempt,
        CancellationToken cancellationToken = default)
    {
        EnsureReady();
        ArgumentNullException.ThrowIfNull(attempt);

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            using var transaction = connection.BeginTransaction(deferred: false);
            await AppendAttemptSnapshotAsync(connection, transaction, attempt, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (SqliteException exception)
        {
            throw new TaskPersistenceException(
                $"Failed to append attempt '{attempt.Id.Value}' for task '{attempt.TaskId.Value}'.",
                exception);
        }
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskAttempt>> GetAttemptsAsync(
        TaskId taskId,
        CancellationToken cancellationToken = default)
    {
        EnsureReady();
        ArgumentNullException.ThrowIfNull(taskId);

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            using var transaction = connection.BeginTransaction(deferred: true);
            var attempts = await LoadAttemptSnapshotsAsync(
                connection,
                transaction,
                taskId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return attempts;
        }
        catch (SqliteException exception)
        {
            throw new TaskPersistenceException($"Failed to load attempts for task '{taskId.Value}'.", exception);
        }
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<TaskStateTransitionRecord>> GetStateTransitionsAsync(
        TaskId taskId,
        CancellationToken cancellationToken = default)
    {
        EnsureReady();
        ArgumentNullException.ThrowIfNull(taskId);

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            using var transaction = connection.BeginTransaction(deferred: true);
            var transitions = await LoadStateTransitionsAsync(
                connection,
                transaction,
                taskId,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return transitions;
        }
        catch (SqliteException exception)
        {
            throw new TaskPersistenceException(
                $"Failed to load state transitions for task '{taskId.Value}'.",
                exception);
        }
    }

    public async AsyncTask AddEvidenceAsync(
        EvidenceRecord evidence,
        CancellationToken cancellationToken = default)
    {
        EnsureReady();
        ArgumentNullException.ThrowIfNull(evidence);

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            using var transaction = connection.BeginTransaction(deferred: false);

            await using (var taskCommand = connection.CreateCommand())
            {
                taskCommand.Transaction = transaction;
                taskCommand.CommandText = "SELECT 1 FROM Tasks WHERE TaskId = $taskId;";
                taskCommand.Parameters.AddWithValue("$taskId", evidence.TaskId.Value);
                if (await taskCommand.ExecuteScalarAsync(cancellationToken) is null)
                {
                    throw new TaskNotFoundException(evidence.TaskId.Value);
                }
            }

            if (evidence.AttemptId is not null)
            {
                await using var attemptCommand = connection.CreateCommand();
                attemptCommand.Transaction = transaction;
                attemptCommand.CommandText = """
                    SELECT 1
                    FROM TaskAttempts
                    WHERE TaskId = $taskId AND AttemptId = $attemptId;
                    """;
                attemptCommand.Parameters.AddWithValue("$taskId", evidence.TaskId.Value);
                attemptCommand.Parameters.AddWithValue("$attemptId", evidence.AttemptId.Value);
                if (await attemptCommand.ExecuteScalarAsync(cancellationToken) is null)
                {
                    throw new TaskPersistenceException(
                        $"Attempt '{evidence.AttemptId.Value}' does not exist for task '{evidence.TaskId.Value}'.");
                }
            }

            var existing = await LoadEvidenceByIdAsync(
                connection,
                transaction,
                evidence.Id,
                cancellationToken);
            if (existing is not null)
            {
                if (existing != evidence)
                {
                    throw new TaskPersistenceException(
                        $"Evidence identifier '{evidence.Id.Value}' was reused with different evidence data.");
                }

                await transaction.CommitAsync(cancellationToken);
                return;
            }

            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = """
                INSERT INTO EvidenceRecords (
                    EvidenceId, TaskId, AttemptId, Type, Source, Summary, Timestamp, Reference)
                VALUES (
                    $evidenceId, $taskId, $attemptId, $type, $source, $summary, $timestamp, $reference);
                """;
            insertCommand.Parameters.AddWithValue("$evidenceId", evidence.Id.Value);
            insertCommand.Parameters.AddWithValue("$taskId", evidence.TaskId.Value);
            insertCommand.Parameters.AddWithValue(
                "$attemptId",
                (object?)(evidence.AttemptId?.Value) ?? DBNull.Value);
            insertCommand.Parameters.AddWithValue("$type", (int)evidence.Type);
            insertCommand.Parameters.AddWithValue("$source", (int)evidence.Source);
            insertCommand.Parameters.AddWithValue("$summary", evidence.Summary);
            insertCommand.Parameters.AddWithValue("$timestamp", FormatTimestamp(evidence.Timestamp));
            insertCommand.Parameters.AddWithValue(
                "$reference",
                (object?)(evidence.Reference?.Value) ?? DBNull.Value);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (SqliteException exception)
        {
            throw new TaskPersistenceException(
                $"Failed to append evidence '{evidence.Id.Value}' for task '{evidence.TaskId.Value}'.",
                exception);
        }
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<EvidenceRecord>> GetEvidenceAsync(
        TaskId taskId,
        CancellationToken cancellationToken = default)
    {
        EnsureReady();
        ArgumentNullException.ThrowIfNull(taskId);

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            using var transaction = connection.BeginTransaction(deferred: true);
            var evidence = await LoadEvidenceAsync(
                connection,
                transaction,
                taskId,
                attemptId: null,
                filterByAttempt: false,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return evidence;
        }
        catch (SqliteException exception)
        {
            throw new TaskPersistenceException(
                $"Failed to load evidence for task '{taskId.Value}'.",
                exception);
        }
    }

    public async System.Threading.Tasks.Task<IReadOnlyList<EvidenceRecord>> GetEvidenceForAttemptAsync(
        TaskId taskId,
        AttemptId attemptId,
        CancellationToken cancellationToken = default)
    {
        EnsureReady();
        ArgumentNullException.ThrowIfNull(taskId);
        ArgumentNullException.ThrowIfNull(attemptId);

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            using var transaction = connection.BeginTransaction(deferred: true);
            var evidence = await LoadEvidenceAsync(
                connection,
                transaction,
                taskId,
                attemptId,
                filterByAttempt: true,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return evidence;
        }
        catch (SqliteException exception)
        {
            throw new TaskPersistenceException(
                $"Failed to load evidence for attempt '{attemptId.Value}' on task '{taskId.Value}'.",
                exception);
        }
    }

    public void Dispose()
    {
        _initializationLock.Wait();
        try
        {
            lock (_lifecycleLock)
            {
                _state = PersistenceState.Disposed;
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _initializationLock.WaitAsync();
        try
        {
            lock (_lifecycleLock)
            {
                _state = PersistenceState.Disposed;
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private void EnsureReady()
    {
        lock (_lifecycleLock)
        {
            if (_state == PersistenceState.Disposed)
            {
                throw new ObjectDisposedException(nameof(SqliteTaskRepository));
            }

            if (_state != PersistenceState.Ready)
            {
                throw new TaskPersistenceException(
                    $"Persistence is unavailable while its lifecycle state is '{_state}'.");
            }
        }
    }

    private void SetFailedUnlessDisposed()
    {
        lock (_lifecycleLock)
        {
            if (_state != PersistenceState.Disposed)
            {
                _state = PersistenceState.Failed;
            }
        }
    }

    private AsyncTask ValidateDatabaseLocationAsync()
    {
        if (Directory.Exists(_databasePath))
        {
            throw new TaskPersistenceException(
                "The configured SQLite database path refers to a directory, not a database file.");
        }

        var parentDirectory = Path.GetDirectoryName(_databasePath);
        if (string.IsNullOrWhiteSpace(parentDirectory) || !Directory.Exists(parentDirectory))
        {
            throw new TaskPersistenceException(
                "The parent directory for the configured SQLite database does not exist.");
        }

        return AsyncTask.CompletedTask;
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
        await using (var metadataCommand = connection.CreateCommand())
        {
            metadataCommand.Transaction = transaction;
            metadataCommand.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_schema
                WHERE type = 'table' AND name = $tableName;
                """;
            metadataCommand.Parameters.AddWithValue("$tableName", SchemaVersionTable);
            var metadataTableExists = Convert.ToInt32(
                await metadataCommand.ExecuteScalarAsync(cancellationToken),
                CultureInfo.InvariantCulture) == 1;

            if (metadataTableExists)
            {
                await using var versionCommand = connection.CreateCommand();
                versionCommand.Transaction = transaction;
                versionCommand.CommandText = $"""
                    SELECT Id, Version, AppliedAt
                    FROM {SchemaVersionTable}
                    ORDER BY Id;
                    """;
                await using var reader = await versionCommand.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken)
                    || reader.GetInt32(0) != 1)
                {
                    throw new TaskPersistenceException(
                        "The SQLite schema-version metadata must contain exactly one row with Id 1.");
                }

                int schemaVersion;
                try
                {
                    schemaVersion = reader.GetInt32(1);
                    _ = DateTimeOffset.ParseExact(
                        reader.GetString(2),
                        "O",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None);
                }
                catch (Exception exception) when (
                    exception is FormatException
                    or InvalidCastException
                    or InvalidOperationException
                    or OverflowException)
                {
                    throw new TaskPersistenceException(
                        "The SQLite schema-version metadata contains invalid values.",
                        exception);
                }

                if (schemaVersion < 1 || await reader.ReadAsync(cancellationToken))
                {
                    throw new TaskPersistenceException(
                        "The SQLite schema-version metadata is malformed.");
                }

                return schemaVersion;
            }
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA user_version;";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        var legacyVersion = Convert.ToInt32(value, CultureInfo.InvariantCulture);
        if (legacyVersion is 0 or 1)
        {
            return legacyVersion;
        }

        throw new TaskPersistenceException(
            $"The database has unrecognized legacy schema version {legacyVersion}; it will not be downgraded or recreated.");
    }

    private static AsyncTask ValidateVersionOneSchemaAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken) =>
        ValidateTaskSchemaObjectsAsync(
            connection,
            transaction,
            requireVersionMetadata: false,
            cancellationToken);

    private static async AsyncTask ValidateCurrentSchemaAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await ValidateTaskSchemaObjectsAsync(
            connection,
            transaction: null,
            requireVersionMetadata: true,
            cancellationToken);
        await ValidateExecutionHistorySchemaAsync(connection, cancellationToken);
        await ValidateEvidenceSchemaAsync(connection, cancellationToken);

        await using (var integrityCommand = connection.CreateCommand())
        {
            integrityCommand.CommandText = "PRAGMA integrity_check;";
            await using var integrityReader = await integrityCommand.ExecuteReaderAsync(cancellationToken);
            if (!await integrityReader.ReadAsync(cancellationToken)
                || !string.Equals(integrityReader.GetString(0), "ok", StringComparison.OrdinalIgnoreCase)
                || await integrityReader.ReadAsync(cancellationToken))
            {
                throw new TaskPersistenceException("The SQLite database integrity check failed.");
            }
        }

        await using var foreignKeyCommand = connection.CreateCommand();
        foreignKeyCommand.CommandText = "PRAGMA foreign_key_check;";
        await using var foreignKeyReader = await foreignKeyCommand.ExecuteReaderAsync(cancellationToken);
        if (await foreignKeyReader.ReadAsync(cancellationToken))
        {
            throw new TaskPersistenceException("The SQLite database contains a foreign-key integrity violation.");
        }
    }

    private static async AsyncTask ValidateTaskSchemaObjectsAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        bool requireVersionMetadata,
        CancellationToken cancellationToken)
    {
        var tasksColumns = await ReadTableColumnsAsync(connection, transaction, "Tasks", cancellationToken);
        RequireColumns(
            "Tasks",
            tasksColumns,
            [
                "TaskId", "Intent", "Description", "DependenciesJson", "ScopeEntriesJson",
                "AcceptanceCriteriaJson", "StopConditionsJson",
                "MaxProductionFilesChanged", "MaxTestFilesChanged", "MaxTotalFilesChanged",
                "MaxDependencyAdditions", "MaxFileDeletions", "InitialState", "InitialRevision",
                "State", "Revision", "CreatedAt", "UpdatedAt"
            ]);
        if (!tasksColumns.Any(column =>
                string.Equals(column.Name, "TaskId", StringComparison.OrdinalIgnoreCase)
                && column.PrimaryKeyPosition == 1))
        {
            throw new TaskPersistenceException("The Tasks table is missing its TaskId primary key.");
        }

        var operationColumns = await ReadTableColumnsAsync(
            connection,
            transaction,
            "TaskOperations",
            cancellationToken);
        RequireColumns(
            "TaskOperations",
            operationColumns,
            [
                "TaskId", "OperationId", "CorrelationId", "ExpectedRevision", "PreviousState",
                "TargetState", "ResultingRevision", "ResultingState", "AppliedAt"
            ]);

        var uniqueIndexes = await ReadUniqueIndexesAsync(
            connection,
            transaction,
            "TaskOperations",
            cancellationToken);
        if (!uniqueIndexes.Any(index => index.Columns.SequenceEqual(
                ["TaskId", "OperationId"],
                StringComparer.OrdinalIgnoreCase))
            || !uniqueIndexes.Any(index => index.Columns.SequenceEqual(
                ["TaskId", "ResultingRevision"],
                StringComparer.OrdinalIgnoreCase)))
        {
            throw new TaskPersistenceException(
                "The TaskOperations table is missing its required unique operation and revision constraints.");
        }

        await ValidateForeignKeyAsync(connection, transaction, cancellationToken);
        await ValidateImmutableHistoryTriggersAsync(connection, transaction, cancellationToken);

        if (!requireVersionMetadata)
        {
            return;
        }

        var versionColumns = await ReadTableColumnsAsync(
            connection,
            transaction,
            SchemaVersionTable,
            cancellationToken);
        RequireColumns(SchemaVersionTable, versionColumns, ["Id", "Version", "AppliedAt"]);
        if (!versionColumns.Any(column =>
                string.Equals(column.Name, "Id", StringComparison.OrdinalIgnoreCase)
                && column.PrimaryKeyPosition == 1))
        {
            throw new TaskPersistenceException(
                "The schema-version metadata is missing its singleton primary key.");
        }
    }

    private static async AsyncTask ValidateExecutionHistorySchemaAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var attemptColumns = await ReadTableColumnsAsync(
            connection,
            transaction: null,
            "TaskAttempts",
            cancellationToken);
        RequireColumns("TaskAttempts", attemptColumns, ["AttemptId", "TaskId", "Number", "StartedAt"]);
        if (!attemptColumns.Any(column =>
                string.Equals(column.Name, "AttemptId", StringComparison.OrdinalIgnoreCase)
                && column.PrimaryKeyPosition == 1))
        {
            throw new TaskPersistenceException("The TaskAttempts table is missing its AttemptId primary key.");
        }

        var attemptIndexes = await ReadUniqueIndexesAsync(
            connection,
            transaction: null,
            "TaskAttempts",
            cancellationToken);
        if (!attemptIndexes.Any(index => index.Columns.SequenceEqual(
                ["TaskId", "Number"],
                StringComparer.OrdinalIgnoreCase))
            || !attemptIndexes.Any(index => index.Columns.SequenceEqual(
                ["TaskId", "AttemptId"],
                StringComparer.OrdinalIgnoreCase)))
        {
            throw new TaskPersistenceException(
                "The TaskAttempts table is missing its task-number or task-identity uniqueness constraint.");
        }

        var snapshotColumns = await ReadTableColumnsAsync(
            connection,
            transaction: null,
            "TaskAttemptSnapshots",
            cancellationToken);
        RequireColumns(
            "TaskAttemptSnapshots",
            snapshotColumns,
            ["AttemptId", "SnapshotNumber", "State", "CompletedAt"]);
        if (!snapshotColumns.Any(column =>
                string.Equals(column.Name, "AttemptId", StringComparison.OrdinalIgnoreCase)
                && column.PrimaryKeyPosition == 1)
            || !snapshotColumns.Any(column =>
                string.Equals(column.Name, "SnapshotNumber", StringComparison.OrdinalIgnoreCase)
                && column.PrimaryKeyPosition == 2))
        {
            throw new TaskPersistenceException(
                "The TaskAttemptSnapshots table is missing its composite snapshot primary key.");
        }

        var transitionColumns = await ReadTableColumnsAsync(
            connection,
            transaction: null,
            "StateTransitions",
            cancellationToken);
        RequireColumns(
            "StateTransitions",
            transitionColumns,
            [
                "TaskId", "TransitionId", "OperationId", "ExpectedRevision", "ResultingRevision",
                "PreviousState", "NextState", "Timestamp", "Reason", "AttemptId",
                "AttemptSnapshotNumber"
            ]);
        if (!transitionColumns.Any(column =>
                string.Equals(column.Name, "TaskId", StringComparison.OrdinalIgnoreCase)
                && column.PrimaryKeyPosition == 1)
            || !transitionColumns.Any(column =>
                string.Equals(column.Name, "TransitionId", StringComparison.OrdinalIgnoreCase)
                && column.PrimaryKeyPosition == 2))
        {
            throw new TaskPersistenceException(
                "The StateTransitions table is missing its task/transition primary key.");
        }

        var transitionIndexes = await ReadUniqueIndexesAsync(
            connection,
            transaction: null,
            "StateTransitions",
            cancellationToken);
        if (!transitionIndexes.Any(index => index.Columns.SequenceEqual(
                ["TaskId", "OperationId"],
                StringComparer.OrdinalIgnoreCase))
            || !transitionIndexes.Any(index => index.Columns.SequenceEqual(
                ["TaskId", "ResultingRevision"],
                StringComparer.OrdinalIgnoreCase)))
        {
            throw new TaskPersistenceException(
                "The StateTransitions table is missing its operation or revision uniqueness constraint.");
        }

        await ValidateRequiredForeignKeyAsync(
            connection,
            "TaskAttempts",
            "Tasks",
            [("TaskId", "TaskId")],
            cancellationToken);
        await ValidateRequiredForeignKeyAsync(
            connection,
            "TaskAttemptSnapshots",
            "TaskAttempts",
            [("AttemptId", "AttemptId")],
            cancellationToken);
        await ValidateRequiredForeignKeyAsync(
            connection,
            "StateTransitions",
            "TaskOperations",
            [("TaskId", "TaskId"), ("OperationId", "OperationId")],
            cancellationToken);
        await ValidateRequiredForeignKeyAsync(
            connection,
            "StateTransitions",
            "TaskAttemptSnapshots",
            [("AttemptId", "AttemptId"), ("AttemptSnapshotNumber", "SnapshotNumber")],
            cancellationToken);
        await ValidateRequiredForeignKeyAsync(
            connection,
            "StateTransitions",
            "TaskAttempts",
            [("TaskId", "TaskId"), ("AttemptId", "AttemptId")],
            cancellationToken);
        await ValidateExecutionHistoryTriggersAsync(connection, cancellationToken);
    }

    private static async AsyncTask ValidateEvidenceSchemaAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var columns = await ReadTableColumnsAsync(
            connection,
            transaction: null,
            "EvidenceRecords",
            cancellationToken);
        RequireColumns(
            "EvidenceRecords",
            columns,
            [
                "EvidenceSequence", "EvidenceId", "TaskId", "AttemptId", "Type", "Source",
                "Summary", "Timestamp", "Reference"
            ]);
        if (!columns.Any(column =>
                string.Equals(column.Name, "EvidenceSequence", StringComparison.OrdinalIgnoreCase)
                && column.PrimaryKeyPosition == 1))
        {
            throw new TaskPersistenceException(
                "The EvidenceRecords table is missing its insertion-sequence primary key.");
        }

        var uniqueIndexes = await ReadUniqueIndexesAsync(
            connection,
            transaction: null,
            "EvidenceRecords",
            cancellationToken);
        if (!uniqueIndexes.Any(index => index.Columns.SequenceEqual(
                ["EvidenceId"],
                StringComparer.OrdinalIgnoreCase)))
        {
            throw new TaskPersistenceException(
                "The EvidenceRecords table is missing its evidence-identity uniqueness constraint.");
        }

        await ValidateRequiredForeignKeyAsync(
            connection,
            "EvidenceRecords",
            "Tasks",
            [("TaskId", "TaskId")],
            cancellationToken);
        await ValidateRequiredForeignKeyAsync(
            connection,
            "EvidenceRecords",
            "TaskAttempts",
            [("TaskId", "TaskId"), ("AttemptId", "AttemptId")],
            cancellationToken);
    }

    private static async System.Threading.Tasks.Task<IReadOnlyList<TableColumn>> ReadTableColumnsAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = new List<TableColumn>();
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(new TableColumn(reader.GetString(1), reader.GetInt32(5)));
        }

        if (columns.Count == 0)
        {
            throw new TaskPersistenceException($"Required SQLite table '{tableName}' does not exist.");
        }

        return columns;
    }

    private static void RequireColumns(
        string tableName,
        IReadOnlyList<TableColumn> actual,
        IReadOnlyList<string> required)
    {
        var actualNames = actual.Select(column => column.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (required.Any(column => !actualNames.Contains(column)))
        {
            throw new TaskPersistenceException(
                $"SQLite table '{tableName}' is missing one or more required columns.");
        }
    }

    private static async System.Threading.Tasks.Task<IReadOnlyList<UniqueIndex>> ReadUniqueIndexesAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string tableName,
        CancellationToken cancellationToken)
    {
        var indexes = new List<UniqueIndex>();
        var indexNames = new List<string>();
        await using var listCommand = connection.CreateCommand();
        listCommand.Transaction = transaction;
        listCommand.CommandText = $"PRAGMA index_list(\"{tableName}\");";
        await using (var listReader = await listCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await listReader.ReadAsync(cancellationToken))
            {
                if (listReader.GetInt32(2) == 1)
                {
                    indexNames.Add(listReader.GetString(1));
                }
            }
        }

        foreach (var indexName in indexNames)
        {
            var escapedName = indexName.Replace("\"", "\"\"", StringComparison.Ordinal);
            await using var indexCommand = connection.CreateCommand();
            indexCommand.Transaction = transaction;
            indexCommand.CommandText = $"PRAGMA index_info(\"{escapedName}\");";
            await using var indexReader = await indexCommand.ExecuteReaderAsync(cancellationToken);
            var columns = new List<string>();
            while (await indexReader.ReadAsync(cancellationToken))
            {
                columns.Add(indexReader.GetString(2));
            }

            indexes.Add(new UniqueIndex(columns));
        }

        return indexes;
    }

    private static async AsyncTask ValidateForeignKeyAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA foreign_key_list(\"TaskOperations\");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(2), "Tasks", StringComparison.OrdinalIgnoreCase)
                && string.Equals(reader.GetString(3), "TaskId", StringComparison.OrdinalIgnoreCase)
                && string.Equals(reader.GetString(4), "TaskId", StringComparison.OrdinalIgnoreCase)
                && string.Equals(reader.GetString(6), "RESTRICT", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        throw new TaskPersistenceException(
            "The TaskOperations table is missing its required restricted TaskId foreign key.");
    }

    private static async AsyncTask ValidateRequiredForeignKeyAsync(
        SqliteConnection connection,
        string tableName,
        string referencedTable,
        IReadOnlyList<(string FromColumn, string ReferencedColumn)> columns,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA foreign_key_list(\"{tableName}\");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var mappings = new List<(int Id, int Sequence, string Table, string From, string To, string OnDelete)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            mappings.Add((
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(6)));
        }

        var isPresent = mappings
            .GroupBy(mapping => mapping.Id)
            .Any(group =>
                group.Count() == columns.Count
                && group.All(mapping =>
                    string.Equals(mapping.Table, referencedTable, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(mapping.OnDelete, "RESTRICT", StringComparison.OrdinalIgnoreCase))
                && group.OrderBy(mapping => mapping.Sequence)
                    .Select(mapping => (mapping.From, mapping.To))
                    .Zip(columns)
                    .All(pair =>
                        string.Equals(pair.First.From, pair.Second.FromColumn, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(
                            pair.First.To,
                            pair.Second.ReferencedColumn,
                            StringComparison.OrdinalIgnoreCase)));
        if (!isPresent)
        {
            throw new TaskPersistenceException(
                $"The {tableName} table is missing its restricted foreign key to {referencedTable}.");
        }
    }

    private static async AsyncTask ValidateExecutionHistoryTriggersAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var requiredTriggers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TaskAttempts_NoUpdate"] = "BEFORE UPDATE ON TaskAttempts",
            ["TaskAttempts_NoDelete"] = "BEFORE DELETE ON TaskAttempts",
            ["TaskAttemptSnapshots_NoUpdate"] = "BEFORE UPDATE ON TaskAttemptSnapshots",
            ["TaskAttemptSnapshots_NoDelete"] = "BEFORE DELETE ON TaskAttemptSnapshots",
            ["StateTransitions_NoUpdate"] = "BEFORE UPDATE ON StateTransitions",
            ["StateTransitions_NoDelete"] = "BEFORE DELETE ON StateTransitions",
            ["EvidenceRecords_NoUpdate"] = "BEFORE UPDATE ON EvidenceRecords",
            ["EvidenceRecords_NoDelete"] = "BEFORE DELETE ON EvidenceRecords"
        };

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name, sql
            FROM sqlite_schema
            WHERE type = 'trigger';
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(0);
            if (requiredTriggers.TryGetValue(name, out var requiredSql)
                && reader.GetString(1).Contains(requiredSql, StringComparison.OrdinalIgnoreCase))
            {
                requiredTriggers.Remove(name);
            }
        }

        if (requiredTriggers.Count != 0)
        {
            throw new TaskPersistenceException(
                "One or more immutable attempt, state-transition, or evidence history triggers are missing or invalid.");
        }
    }

    private static async AsyncTask ValidateImmutableHistoryTriggersAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT name, sql
            FROM sqlite_schema
            WHERE type = 'trigger'
              AND name IN ('TaskOperations_NoUpdate', 'TaskOperations_NoDelete');
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var verifiedUpdateTrigger = false;
        var verifiedDeleteTrigger = false;
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(0);
            var sql = reader.GetString(1);
            if (name == "TaskOperations_NoUpdate"
                && sql.Contains("BEFORE UPDATE ON TaskOperations", StringComparison.OrdinalIgnoreCase))
            {
                verifiedUpdateTrigger = true;
            }

            if (name == "TaskOperations_NoDelete"
                && sql.Contains("BEFORE DELETE ON TaskOperations", StringComparison.OrdinalIgnoreCase))
            {
                verifiedDeleteTrigger = true;
            }
        }

        if (!verifiedUpdateTrigger || !verifiedDeleteTrigger)
        {
            throw new TaskPersistenceException(
                "The TaskOperations immutable-history triggers are missing or invalid.");
        }
    }

    private static async AsyncTask EnsureDatabaseIsEmptyAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_schema
            WHERE name NOT LIKE 'sqlite_%'
              AND type IN ('table', 'index', 'view', 'trigger');
            """;
        var objectCount = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
        if (objectCount != 0)
        {
            throw new TaskPersistenceException(
                "A database without an explicit schema version must be empty before initialization.");
        }
    }

    private static async AsyncTask MigrateVersionZeroToOneAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await EnsureDatabaseIsEmptyAsync(connection, transaction, cancellationToken);
        await CreateSchemaAsync(connection, transaction, cancellationToken);
        await SetLegacySchemaVersionAsync(
            connection,
            transaction,
            InitialSchemaVersion,
            cancellationToken);
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

    private static async AsyncTask ApplySchemaVersionTwoAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {SchemaVersionTable} (
                Id INTEGER NOT NULL PRIMARY KEY CHECK (Id = 1),
                Version INTEGER NOT NULL CHECK (Version > 0),
                AppliedAt TEXT NOT NULL
            );

            INSERT INTO {SchemaVersionTable} (Id, Version, AppliedAt)
            VALUES (1, $version, $appliedAt)
            ON CONFLICT(Id) DO UPDATE SET
                Version = excluded.Version,
                AppliedAt = excluded.AppliedAt;

            PRAGMA user_version = {SchemaVersionTwo};
            """;
        command.Parameters.AddWithValue("$version", SchemaVersionTwo);
        command.Parameters.AddWithValue("$appliedAt", FormatTimestamp(DateTimeOffset.UtcNow));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async AsyncTask ApplySchemaVersionThreeAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            CREATE TABLE TaskAttempts (
                AttemptId TEXT NOT NULL PRIMARY KEY CHECK (length(trim(AttemptId)) > 0),
                TaskId TEXT NOT NULL,
                Number INTEGER NOT NULL CHECK (Number > 0),
                StartedAt TEXT NOT NULL,
                UNIQUE (TaskId, Number),
                UNIQUE (TaskId, AttemptId),
                FOREIGN KEY (TaskId) REFERENCES Tasks (TaskId) ON DELETE RESTRICT
            );

            CREATE TABLE TaskAttemptSnapshots (
                AttemptId TEXT NOT NULL,
                SnapshotNumber INTEGER NOT NULL CHECK (SnapshotNumber > 0),
                State INTEGER NOT NULL CHECK (State BETWEEN 0 AND {(int)TaskAttemptState.Unknown}),
                CompletedAt TEXT NULL,
                PRIMARY KEY (AttemptId, SnapshotNumber),
                FOREIGN KEY (AttemptId) REFERENCES TaskAttempts (AttemptId) ON DELETE RESTRICT
            );

            CREATE TABLE StateTransitions (
                TaskId TEXT NOT NULL,
                TransitionId TEXT NOT NULL CHECK (length(trim(TransitionId)) > 0),
                OperationId TEXT NOT NULL CHECK (length(trim(OperationId)) > 0),
                ExpectedRevision INTEGER NOT NULL CHECK (ExpectedRevision >= 0),
                ResultingRevision INTEGER NOT NULL CHECK (ResultingRevision > 0),
                PreviousState INTEGER NOT NULL CHECK (PreviousState BETWEEN 0 AND {MaximumTaskStateValue}),
                NextState INTEGER NOT NULL CHECK (NextState BETWEEN 0 AND {MaximumTaskStateValue}),
                Timestamp TEXT NOT NULL,
                Reason TEXT NOT NULL CHECK (length(trim(Reason)) > 0),
                AttemptId TEXT NULL,
                AttemptSnapshotNumber INTEGER NULL,
                PRIMARY KEY (TaskId, TransitionId),
                UNIQUE (TaskId, OperationId),
                UNIQUE (TaskId, ResultingRevision),
                FOREIGN KEY (TaskId, OperationId)
                    REFERENCES TaskOperations (TaskId, OperationId) ON DELETE RESTRICT,
                FOREIGN KEY (AttemptId, AttemptSnapshotNumber)
                    REFERENCES TaskAttemptSnapshots (AttemptId, SnapshotNumber) ON DELETE RESTRICT,
                FOREIGN KEY (TaskId, AttemptId)
                    REFERENCES TaskAttempts (TaskId, AttemptId) ON DELETE RESTRICT,
                CHECK (ResultingRevision = ExpectedRevision + 1),
                CHECK ((AttemptId IS NULL) = (AttemptSnapshotNumber IS NULL)),
                CHECK (PreviousState <> NextState)
            );

            CREATE TRIGGER TaskAttempts_NoUpdate
            BEFORE UPDATE ON TaskAttempts
            BEGIN
                SELECT RAISE(ABORT, 'Task attempt identity is immutable.');
            END;

            CREATE TRIGGER TaskAttempts_NoDelete
            BEFORE DELETE ON TaskAttempts
            BEGIN
                SELECT RAISE(ABORT, 'Task attempt identity is immutable.');
            END;

            CREATE TRIGGER TaskAttemptSnapshots_NoUpdate
            BEFORE UPDATE ON TaskAttemptSnapshots
            BEGIN
                SELECT RAISE(ABORT, 'Task attempt history is immutable.');
            END;

            CREATE TRIGGER TaskAttemptSnapshots_NoDelete
            BEFORE DELETE ON TaskAttemptSnapshots
            BEGIN
                SELECT RAISE(ABORT, 'Task attempt history is immutable.');
            END;

            CREATE TRIGGER StateTransitions_NoUpdate
            BEFORE UPDATE ON StateTransitions
            BEGIN
                SELECT RAISE(ABORT, 'Task state-transition history is immutable.');
            END;

            CREATE TRIGGER StateTransitions_NoDelete
            BEFORE DELETE ON StateTransitions
            BEGIN
                SELECT RAISE(ABORT, 'Task state-transition history is immutable.');
            END;

            UPDATE {SchemaVersionTable}
            SET Version = $version, AppliedAt = $appliedAt
            WHERE Id = 1 AND Version = $previousVersion;

            PRAGMA user_version = {SchemaVersionThree};
            """;
        command.Parameters.AddWithValue("$version", SchemaVersionThree);
        command.Parameters.AddWithValue("$previousVersion", SchemaVersionTwo);
        command.Parameters.AddWithValue("$appliedAt", FormatTimestamp(DateTimeOffset.UtcNow));
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var verifyCommand = connection.CreateCommand();
        verifyCommand.Transaction = transaction;
        verifyCommand.CommandText = $"SELECT Version FROM {SchemaVersionTable} WHERE Id = 1;";
        var version = Convert.ToInt32(
            await verifyCommand.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
        if (version != SchemaVersionThree)
        {
            throw new TaskPersistenceException(
                "The schema-version metadata was not at version 2 when the attempt-history migration ran.");
        }
    }

    private static async AsyncTask ApplySchemaVersionFourAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            CREATE TABLE EvidenceRecords (
                EvidenceSequence INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                EvidenceId TEXT NOT NULL UNIQUE CHECK (length(trim(EvidenceId)) > 0),
                TaskId TEXT NOT NULL,
                AttemptId TEXT NULL CHECK (AttemptId IS NULL OR length(trim(AttemptId)) > 0),
                Type INTEGER NOT NULL CHECK (Type BETWEEN 0 AND {(int)EvidenceType.System}),
                Source INTEGER NOT NULL CHECK (Source BETWEEN 0 AND {(int)EvidenceSource.External}),
                Summary TEXT NOT NULL CHECK (length(trim(Summary)) > 0),
                Timestamp TEXT NOT NULL,
                Reference TEXT NULL CHECK (Reference IS NULL OR length(trim(Reference)) > 0),
                FOREIGN KEY (TaskId) REFERENCES Tasks (TaskId) ON DELETE RESTRICT,
                FOREIGN KEY (TaskId, AttemptId)
                    REFERENCES TaskAttempts (TaskId, AttemptId) ON DELETE RESTRICT
            );

            CREATE INDEX EvidenceRecords_ByTaskSequence
                ON EvidenceRecords (TaskId, EvidenceSequence);

            CREATE INDEX EvidenceRecords_ByAttemptSequence
                ON EvidenceRecords (TaskId, AttemptId, EvidenceSequence);

            CREATE TRIGGER EvidenceRecords_NoUpdate
            BEFORE UPDATE ON EvidenceRecords
            BEGIN
                SELECT RAISE(ABORT, 'Evidence history is immutable.');
            END;

            CREATE TRIGGER EvidenceRecords_NoDelete
            BEFORE DELETE ON EvidenceRecords
            BEGIN
                SELECT RAISE(ABORT, 'Evidence history is immutable.');
            END;

            UPDATE {SchemaVersionTable}
            SET Version = $version, AppliedAt = $appliedAt
            WHERE Id = 1 AND Version = $previousVersion;

            PRAGMA user_version = {SchemaVersion};
            """;
        command.Parameters.AddWithValue("$version", SchemaVersion);
        command.Parameters.AddWithValue("$previousVersion", SchemaVersionThree);
        command.Parameters.AddWithValue("$appliedAt", FormatTimestamp(DateTimeOffset.UtcNow));
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var verifyCommand = connection.CreateCommand();
        verifyCommand.Transaction = transaction;
        verifyCommand.CommandText = $"SELECT Version FROM {SchemaVersionTable} WHERE Id = 1;";
        var version = Convert.ToInt32(
            await verifyCommand.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
        if (version != SchemaVersion)
        {
            throw new TaskPersistenceException(
                "The schema-version metadata was not at version 3 when the evidence migration ran.");
        }
    }

    private static async AsyncTask SetLegacySchemaVersionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int version,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA user_version = {version};";
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

    private static async System.Threading.Tasks.Task<EvidenceRecord?> LoadEvidenceByIdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        EvidenceId evidenceId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT EvidenceId, TaskId, AttemptId, Type, Source, Summary, Timestamp, Reference
            FROM EvidenceRecords
            WHERE EvidenceId = $evidenceId;
            """;
        command.Parameters.AddWithValue("$evidenceId", evidenceId.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadEvidenceRecord(reader)
            : null;
    }

    private static async System.Threading.Tasks.Task<IReadOnlyList<EvidenceRecord>> LoadEvidenceAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        TaskId taskId,
        AttemptId? attemptId,
        bool filterByAttempt,
        CancellationToken cancellationToken)
    {
        var evidence = new List<EvidenceRecord>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT EvidenceId, TaskId, AttemptId, Type, Source, Summary, Timestamp, Reference
            FROM EvidenceRecords
            WHERE TaskId = $taskId
              AND ($filterByAttempt = 0 OR AttemptId = $attemptId)
            ORDER BY EvidenceSequence;
            """;
        command.Parameters.AddWithValue("$taskId", taskId.Value);
        command.Parameters.AddWithValue("$filterByAttempt", filterByAttempt ? 1 : 0);
        command.Parameters.AddWithValue("$attemptId", (object?)(attemptId?.Value) ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            evidence.Add(ReadEvidenceRecord(reader));
        }

        return evidence;
    }

    private static EvidenceRecord ReadEvidenceRecord(SqliteDataReader reader)
    {
        try
        {
            var evidenceType = reader.GetInt32(3);
            var evidenceSource = reader.GetInt32(4);
            if (!Enum.IsDefined(typeof(EvidenceType), evidenceType)
                || !Enum.IsDefined(typeof(EvidenceSource), evidenceSource))
            {
                throw new InvalidDataException("Persisted evidence contains an undefined type or source.");
            }

            return new EvidenceRecord(
                new EvidenceId(reader.GetString(0)),
                new TaskId(reader.GetString(1)),
                (EvidenceType)evidenceType,
                (EvidenceSource)evidenceSource,
                reader.GetString(5),
                ParseTimestamp(reader.GetString(6), "Timestamp"),
                reader.IsDBNull(2) ? null : new AttemptId(reader.GetString(2)),
                reader.IsDBNull(7) ? null : new EvidenceReference(reader.GetString(7)));
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidCastException
            or InvalidDataException
            or OverflowException)
        {
            throw new TaskDataCorruptionException(
                "Persisted evidence contains invalid domain data.",
                exception);
        }
    }

    private static async System.Threading.Tasks.Task<int> AppendAttemptSnapshotAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        TaskAttempt attempt,
        CancellationToken cancellationToken)
    {
        await using (var taskCommand = connection.CreateCommand())
        {
            taskCommand.Transaction = transaction;
            taskCommand.CommandText = "SELECT 1 FROM Tasks WHERE TaskId = $taskId;";
            taskCommand.Parameters.AddWithValue("$taskId", attempt.TaskId.Value);
            if (await taskCommand.ExecuteScalarAsync(cancellationToken) is null)
            {
                throw new TaskNotFoundException(attempt.TaskId.Value);
            }
        }

        var history = await LoadAttemptSnapshotsForIdAsync(
            connection,
            transaction,
            attempt.Id,
            cancellationToken);
        if (history.Count != 0)
        {
            var latest = history[^1];
            if (latest.Attempt.TaskId != attempt.TaskId
                || latest.Attempt.Number != attempt.Number
                || latest.Attempt.StartedAt != attempt.StartedAt)
            {
                throw new TaskPersistenceException(
                    $"Attempt identifier '{attempt.Id.Value}' was reused with different identity data.");
            }

            var duplicate = history.FirstOrDefault(snapshot => snapshot.Attempt == attempt);
            if (duplicate is not null)
            {
                return duplicate.SnapshotNumber;
            }

            var expectedNext = latest.Attempt.TransitionTo(attempt.State, attempt.CompletedAt);
            if (expectedNext != attempt)
            {
                throw new InvalidOperationException(
                    $"Attempt '{attempt.Id.Value}' is not a valid next state snapshot.");
            }
        }
        else
        {
            await using (var numberCommand = connection.CreateCommand())
            {
                numberCommand.Transaction = transaction;
                numberCommand.CommandText = """
                    SELECT COALESCE(MAX(Number), 0)
                    FROM TaskAttempts
                    WHERE TaskId = $taskId;
                    """;
                numberCommand.Parameters.AddWithValue("$taskId", attempt.TaskId.Value);
                var lastNumber = Convert.ToInt32(
                    await numberCommand.ExecuteScalarAsync(cancellationToken),
                    CultureInfo.InvariantCulture);
                if (attempt.Number != checked(lastNumber + 1))
                {
                    throw new InvalidOperationException(
                        $"Task '{attempt.TaskId.Value}' must append attempt number {lastNumber + 1} next.");
                }
            }

            await using var insertIdentity = connection.CreateCommand();
            insertIdentity.Transaction = transaction;
            insertIdentity.CommandText = """
                INSERT INTO TaskAttempts (AttemptId, TaskId, Number, StartedAt)
                VALUES ($attemptId, $taskId, $number, $startedAt);
                """;
            insertIdentity.Parameters.AddWithValue("$attemptId", attempt.Id.Value);
            insertIdentity.Parameters.AddWithValue("$taskId", attempt.TaskId.Value);
            insertIdentity.Parameters.AddWithValue("$number", attempt.Number);
            insertIdentity.Parameters.AddWithValue("$startedAt", FormatTimestamp(attempt.StartedAt));
            await insertIdentity.ExecuteNonQueryAsync(cancellationToken);
        }

        var snapshotNumber = history.Count == 0
            ? 1
            : checked(history[^1].SnapshotNumber + 1);
        await InsertAttemptSnapshotAsync(
            connection,
            transaction,
            attempt,
            snapshotNumber,
            cancellationToken);
        return snapshotNumber;
    }

    private static async System.Threading.Tasks.Task InsertAttemptSnapshotAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        TaskAttempt attempt,
        int snapshotNumber,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO TaskAttemptSnapshots (AttemptId, SnapshotNumber, State, CompletedAt)
            VALUES ($attemptId, $snapshotNumber, $state, $completedAt);
            """;
        command.Parameters.AddWithValue("$attemptId", attempt.Id.Value);
        command.Parameters.AddWithValue("$snapshotNumber", snapshotNumber);
        command.Parameters.AddWithValue("$state", (int)attempt.State);
        command.Parameters.AddWithValue(
            "$completedAt",
            (object?)(attempt.CompletedAt.HasValue
                ? FormatTimestamp(attempt.CompletedAt.Value)
                : null) ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async System.Threading.Tasks.Task<IReadOnlyList<AttemptSnapshotRow>>
        LoadAttemptSnapshotsForIdAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            AttemptId attemptId,
            CancellationToken cancellationToken)
    {
        var snapshots = new List<AttemptSnapshotRow>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT a.TaskId, a.Number, a.StartedAt,
                   s.SnapshotNumber, s.State, s.CompletedAt
            FROM TaskAttempts AS a
            INNER JOIN TaskAttemptSnapshots AS s ON s.AttemptId = a.AttemptId
            WHERE a.AttemptId = $attemptId
            ORDER BY s.SnapshotNumber;
            """;
        command.Parameters.AddWithValue("$attemptId", attemptId.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            try
            {
                var taskId = new TaskId(reader.GetString(0));
                var number = reader.GetInt32(1);
                var startedAt = ParseTimestamp(reader.GetString(2), "StartedAt");
                var snapshotNumber = reader.GetInt32(3);
                var stateValue = reader.GetInt32(4);
                if (!Enum.IsDefined(typeof(TaskAttemptState), stateValue))
                {
                    throw new InvalidDataException("The stored attempt state is undefined.");
                }

                var completedAt = reader.IsDBNull(5)
                    ? (DateTimeOffset?)null
                    : ParseTimestamp(reader.GetString(5), "CompletedAt");
                var attempt = new TaskAttempt(
                    attemptId,
                    taskId,
                    number,
                    (TaskAttemptState)stateValue,
                    startedAt,
                    completedAt);
                if (snapshotNumber != snapshots.Count + 1)
                {
                    throw new InvalidDataException("Attempt snapshot numbers are not contiguous.");
                }

                if (snapshots.Count > 0)
                {
                    var expectedNext = snapshots[^1].Attempt.TransitionTo(attempt.State, attempt.CompletedAt);
                    if (expectedNext != attempt)
                    {
                        throw new InvalidDataException("Attempt snapshots do not follow the domain transition rules.");
                    }
                }

                snapshots.Add(new AttemptSnapshotRow(snapshotNumber, attempt));
            }
            catch (TaskDataCorruptionException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is ArgumentException
                or FormatException
                or InvalidCastException
                or InvalidDataException
                or InvalidOperationException
                or OverflowException)
            {
                throw new TaskDataCorruptionException(
                    $"Attempt '{attemptId.Value}' contains an invalid persisted snapshot.",
                    exception);
            }
        }

        if (snapshots.Count == 0)
        {
            await using var identityCommand = connection.CreateCommand();
            identityCommand.Transaction = transaction;
            identityCommand.CommandText = "SELECT 1 FROM TaskAttempts WHERE AttemptId = $attemptId;";
            identityCommand.Parameters.AddWithValue("$attemptId", attemptId.Value);
            if (await identityCommand.ExecuteScalarAsync(cancellationToken) is not null)
            {
                throw new TaskDataCorruptionException(
                    $"Attempt '{attemptId.Value}' has an identity record without any state snapshots.",
                    new InvalidDataException("Every attempt identity must have a first snapshot."));
            }
        }

        return snapshots;
    }

    private static async System.Threading.Tasks.Task<IReadOnlyList<TaskAttempt>> LoadAttemptSnapshotsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        TaskId taskId,
        CancellationToken cancellationToken)
    {
        var attemptIds = new List<(AttemptId Id, int Number)>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                SELECT AttemptId, Number
                FROM TaskAttempts
                WHERE TaskId = $taskId
                ORDER BY Number;
                """;
            command.Parameters.AddWithValue("$taskId", taskId.Value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                try
                {
                    var attemptNumber = reader.GetInt32(1);
                    if (attemptNumber != attemptIds.Count + 1)
                    {
                        throw new InvalidDataException(
                            "Persisted task attempt numbers are not contiguous.");
                    }

                    attemptIds.Add((new AttemptId(reader.GetString(0)), attemptNumber));
                }
                catch (Exception exception) when (
                    exception is ArgumentException
                    or InvalidCastException
                    or InvalidDataException
                    or OverflowException)
                {
                    throw new TaskDataCorruptionException(
                        $"Task '{taskId.Value}' contains an invalid attempt identifier.",
                        exception);
                }
            }
        }

        var attempts = new List<TaskAttempt>();
        foreach (var (attemptId, _) in attemptIds)
        {
            var snapshots = await LoadAttemptSnapshotsForIdAsync(
                connection,
                transaction,
                attemptId,
                cancellationToken);
            if (snapshots.Count == 0 || snapshots.Any(snapshot => snapshot.Attempt.TaskId != taskId))
            {
                throw new TaskDataCorruptionException(
                    $"Attempt '{attemptId.Value}' has incomplete or inconsistent persisted history.",
                    new InvalidDataException("Attempt identity and history do not match."));
            }

            attempts.AddRange(snapshots.Select(snapshot => snapshot.Attempt));
        }

        return attempts;
    }

    private static async System.Threading.Tasks.Task<int?> FindAttemptSnapshotAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        TaskAttempt attempt,
        CancellationToken cancellationToken)
    {
        var snapshots = await LoadAttemptSnapshotsForIdAsync(
            connection,
            transaction,
            attempt.Id,
            cancellationToken);
        return snapshots.FirstOrDefault(snapshot => snapshot.Attempt == attempt)?.SnapshotNumber;
    }

    private static async AsyncTask InsertStateTransitionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        TaskTransitionOperation operation,
        StateTransition transition,
        TaskRevision resultingRevision,
        TaskAttempt? attempt,
        int? attemptSnapshotNumber,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO StateTransitions (
                TaskId, TransitionId, OperationId, ExpectedRevision, ResultingRevision,
                PreviousState, NextState, Timestamp, Reason, AttemptId, AttemptSnapshotNumber)
            VALUES (
                $taskId, $transitionId, $operationId, $expectedRevision, $resultingRevision,
                $previousState, $nextState, $timestamp, $reason, $attemptId, $attemptSnapshotNumber);
            """;
        command.Parameters.AddWithValue("$taskId", transition.TaskId.Value);
        command.Parameters.AddWithValue("$transitionId", transition.Id.Value);
        command.Parameters.AddWithValue("$operationId", operation.OperationId.Value);
        command.Parameters.AddWithValue("$expectedRevision", operation.ExpectedRevision.Value);
        command.Parameters.AddWithValue("$resultingRevision", resultingRevision.Value);
        command.Parameters.AddWithValue("$previousState", (int)transition.PreviousState);
        command.Parameters.AddWithValue("$nextState", (int)transition.NextState);
        command.Parameters.AddWithValue("$timestamp", FormatTimestamp(transition.Timestamp));
        command.Parameters.AddWithValue("$reason", transition.Reason);
        command.Parameters.AddWithValue("$attemptId", (object?)attempt?.Id.Value ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$attemptSnapshotNumber",
            (object?)attemptSnapshotNumber ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async System.Threading.Tasks.Task<TaskStateTransitionRecord?>
        LoadStateTransitionForOperationAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            TaskId taskId,
            OperationId operationId,
            CancellationToken cancellationToken)
    {
        var transitions = await LoadStateTransitionsAsync(connection, transaction, taskId, cancellationToken);
        return transitions.FirstOrDefault(record => record.OperationId == operationId);
    }

    private static async System.Threading.Tasks.Task<IReadOnlyList<TaskStateTransitionRecord>>
        LoadStateTransitionsAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            TaskId taskId,
            CancellationToken cancellationToken)
    {
        var transitions = new List<TaskStateTransitionRecord>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT s.TransitionId, s.OperationId, s.ExpectedRevision, s.ResultingRevision,
                   s.PreviousState, s.NextState, s.Timestamp, s.Reason,
                   s.AttemptId, s.AttemptSnapshotNumber,
                   o.ExpectedRevision, o.PreviousState, o.TargetState,
                   o.ResultingRevision, o.ResultingState, o.AppliedAt
            FROM StateTransitions AS s
            INNER JOIN TaskOperations AS o
                ON o.TaskId = s.TaskId AND o.OperationId = s.OperationId
            WHERE s.TaskId = $taskId
            ORDER BY s.ResultingRevision;
            """;
        command.Parameters.AddWithValue("$taskId", taskId.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            try
            {
                var operationId = new OperationId(reader.GetString(1));
                var expectedRevision = new TaskRevision(reader.GetInt32(2));
                var resultingRevision = new TaskRevision(reader.GetInt32(3));
                var previousState = ReadState(reader.GetInt32(4), "PreviousState");
                var nextState = ReadState(reader.GetInt32(5), "NextState");
                var timestamp = ParseTimestamp(reader.GetString(6), "Timestamp");
                var transition = new StateTransition(
                    new TransitionId(reader.GetString(0)),
                    taskId,
                    previousState,
                    nextState,
                    timestamp,
                    reader.GetString(7));

                var associatedAttemptId = reader.IsDBNull(8)
                    ? null
                    : new AttemptId(reader.GetString(8));
                var associatedAttemptSnapshotNumber = reader.IsDBNull(9)
                    ? (int?)null
                    : reader.GetInt32(9);
                if ((associatedAttemptId is null) != (associatedAttemptSnapshotNumber is null))
                {
                    throw new InvalidDataException(
                        "The state transition contains an incomplete attempt snapshot reference.");
                }

                var operationExpectedRevision = new TaskRevision(reader.GetInt32(10));
                var operationPreviousState = ReadState(reader.GetInt32(11), "OperationPreviousState");
                var operationTargetState = ReadState(reader.GetInt32(12), "OperationTargetState");
                var operationResultingRevision = new TaskRevision(reader.GetInt32(13));
                var operationResultingState = ReadState(reader.GetInt32(14), "OperationResultingState");
                var operationAppliedAt = ParseTimestamp(reader.GetString(15), "AppliedAt");
                if (resultingRevision.Value != expectedRevision.Value + 1
                    || expectedRevision != operationExpectedRevision
                    || resultingRevision != operationResultingRevision
                    || previousState != operationPreviousState
                    || nextState != operationTargetState
                    || nextState != operationResultingState
                    || timestamp != operationAppliedAt)
                {
                    throw new InvalidDataException(
                        "The persisted state transition does not match its task operation.");
                }

                transitions.Add(new TaskStateTransitionRecord(
                    transition,
                    operationId,
                    expectedRevision,
                    resultingRevision,
                    associatedAttemptId,
                    associatedAttemptSnapshotNumber));
            }
            catch (TaskDataCorruptionException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is ArgumentException
                or FormatException
                or InvalidCastException
                or InvalidDataException
                or InvalidOperationException
                or OverflowException)
            {
                throw new TaskDataCorruptionException(
                    $"Task '{taskId.Value}' contains an invalid state-transition record.",
                    exception);
            }
        }

        return transitions;
    }

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

    private sealed record AttemptSnapshotRow(int SnapshotNumber, TaskAttempt Attempt);

    private sealed record AcceptanceCriterionData(string Id, string Description, bool IsMandatory);

    private sealed record StopConditionData(string Id, string Description, bool IsMandatory);

    private sealed record TableColumn(string Name, int PrimaryKeyPosition);

    private sealed record UniqueIndex(IReadOnlyList<string> Columns);

    private delegate AsyncTask SchemaMigrationAction(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken);

    private sealed record SchemaMigration(
        int FromVersion,
        int ToVersion,
        SchemaMigrationAction Apply);

    private enum PersistenceState
    {
        NotInitialized,
        Initializing,
        Ready,
        Failed,
        Disposed
    }
}
