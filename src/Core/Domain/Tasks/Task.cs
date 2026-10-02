using System.Collections.ObjectModel;

namespace AiGameStudioWorkstation.Core.Domain.Tasks;

public sealed class Task
{
    private readonly ReadOnlyCollection<TaskId> _dependencies;
    private readonly ReadOnlyCollection<AcceptanceCriterion> _acceptanceCriteria;
    private readonly ReadOnlyCollection<StopCondition> _stopConditions;

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
    }

    private Task(Task source, TaskState state, DateTimeOffset updatedAt)
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
        Revision = source.Revision;
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

    public Task TransitionTo(TaskState newState, DateTimeOffset updatedAt)
    {
        if (!TaskStateTransitions.IsValidTransition(State, newState))
        {
            throw new InvalidTaskStateTransitionException(State, newState);
        }

        return new Task(this, newState, updatedAt);
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
