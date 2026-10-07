using AiGameStudioWorkstation.Core.Domain.Tasks;

namespace AiGameStudioWorkstation.Core.Domain.Execution;

public sealed record StateTransition
{
    public StateTransition(
        TransitionId id,
        TaskId taskId,
        TaskState previousState,
        TaskState nextState,
        DateTimeOffset timestamp,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(taskId);

        if (!Enum.IsDefined(typeof(TaskState), previousState))
        {
            throw new ArgumentOutOfRangeException(nameof(previousState), previousState, "The previous task state is not defined.");
        }

        if (!Enum.IsDefined(typeof(TaskState), nextState))
        {
            throw new ArgumentOutOfRangeException(nameof(nextState), nextState, "The next task state is not defined.");
        }

        if (previousState == nextState)
        {
            throw new ArgumentException("A state transition must change the task state.", nameof(nextState));
        }

        if (timestamp == default)
        {
            throw new ArgumentException("A transition timestamp must be specified.", nameof(timestamp));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A transition reason must not be empty or whitespace.", nameof(reason));
        }

        Id = id;
        TaskId = taskId;
        PreviousState = previousState;
        NextState = nextState;
        Timestamp = timestamp;
        Reason = reason;
    }

    public TransitionId Id { get; }

    public TaskId TaskId { get; }

    public TaskState PreviousState { get; }

    public TaskState NextState { get; }

    public DateTimeOffset Timestamp { get; }

    public string Reason { get; }
}
