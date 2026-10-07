using System.Collections.ObjectModel;

namespace AiGameStudioWorkstation.Core.Domain.Tasks;

public sealed class Task
{
    private readonly ReadOnlyCollection<TaskId> _dependencies;
    private readonly ReadOnlyCollection<AcceptanceCriterion> _acceptanceCriteria;
    private readonly ReadOnlyCollection<StopCondition> _stopConditions;
    private readonly TaskTransitionOperation[] _appliedOperations;

    public Task(
        TaskId id,
        string intent,
        string description,
        IEnumerable<TaskId> dependencies,
        TaskScope scope,
        IEnumerable<AcceptanceCriterion> acceptanceCriteria,
        IEnumerable<StopCondition> stopConditions,
        ChangeBudget changeBudget,
        TaskState state,
        DateTimeOffset createdAt,
        TaskRevision revision)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(scope);
        ValidateText(intent, nameof(intent), "Task intent");
        ValidateText(description, nameof(description), "Task description");

        if (!Enum.IsDefined(typeof(TaskState), state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "The task state is not defined.");
        }

        Id = id;
        Intent = intent;
        Description = description;
        _dependencies = CopyCollection(dependencies, nameof(dependencies));
        Scope = scope;
        _acceptanceCriteria = CopyCollection(acceptanceCriteria, nameof(acceptanceCriteria));
        _stopConditions = CopyCollection(stopConditions, nameof(stopConditions));
        ChangeBudget = changeBudget;
        State = state;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        Revision = revision;
        _appliedOperations = [];
    }

    private Task(
        Task source,
        TaskState state,
        DateTimeOffset updatedAt,
        TaskRevision revision,
        TaskTransitionOperation[] appliedOperations)
    {
        Id = source.Id;
        Intent = source.Intent;
        Description = source.Description;
        _dependencies = source._dependencies;
        Scope = source.Scope;
        _acceptanceCriteria = source._acceptanceCriteria;
        _stopConditions = source._stopConditions;
        ChangeBudget = source.ChangeBudget;
        State = state;
        CreatedAt = source.CreatedAt;
        UpdatedAt = updatedAt;
        Revision = revision;
        _appliedOperations = appliedOperations;
    }

    public TaskId Id { get; }

    public string Intent { get; }

    public string Description { get; }

    public IReadOnlyList<TaskId> Dependencies => _dependencies;

    public TaskScope Scope { get; }

    public IReadOnlyList<AcceptanceCriterion> AcceptanceCriteria => _acceptanceCriteria;

    public IReadOnlyList<StopCondition> StopConditions => _stopConditions;

    public ChangeBudget ChangeBudget { get; }

    public TaskState State { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    public TaskRevision Revision { get; }

    public TaskOperationResult ApplyTransition(
        TaskTransitionOperation operation,
        DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.TaskId != Id)
        {
            return TaskOperationResult.Rejected(this, TaskOperationRejection.TaskMismatch);
        }

        var previouslyApplied = _appliedOperations.FirstOrDefault(
            applied => applied.OperationId == operation.OperationId);
        if (previouslyApplied is not null)
        {
            return previouslyApplied == operation
                ? TaskOperationResult.AlreadyApplied(this)
                : TaskOperationResult.Rejected(this, TaskOperationRejection.OperationIdConflict);
        }

        if (operation.ExpectedRevision != Revision)
        {
            return TaskOperationResult.Rejected(this, TaskOperationRejection.StaleRevision);
        }

        if (!TaskStateTransitions.IsValidTransition(State, operation.TargetState))
        {
            return TaskOperationResult.Rejected(this, TaskOperationRejection.InvalidTransition);
        }

        TaskRevision nextRevision;
        try
        {
            nextRevision = Revision.Next();
        }
        catch (OverflowException)
        {
            return TaskOperationResult.Rejected(this, TaskOperationRejection.RevisionExhausted);
        }

        var appliedOperations = new TaskTransitionOperation[_appliedOperations.Length + 1];
        Array.Copy(_appliedOperations, appliedOperations, _appliedOperations.Length);
        appliedOperations[^1] = operation;

        var updatedTask = new Task(
            this,
            operation.TargetState,
            updatedAt,
            nextRevision,
            appliedOperations);
        return TaskOperationResult.Applied(updatedTask);
    }

    private static ReadOnlyCollection<T> CopyCollection<T>(IEnumerable<T> values, string parameterName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);

        var copy = values.ToArray();
        if (copy.Any(value => value is null))
        {
            throw new ArgumentException("Task collections cannot contain null entries.", parameterName);
        }

        return Array.AsReadOnly(copy);
    }

    private static void ValidateText(string value, string parameterName, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{label} must not be empty or whitespace.", parameterName);
        }
    }
}
