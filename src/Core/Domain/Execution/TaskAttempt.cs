using AiGameStudioWorkstation.Core.Domain.Tasks;

namespace AiGameStudioWorkstation.Core.Domain.Execution;

public sealed record TaskAttempt
{
    public TaskAttempt(
        AttemptId id,
        TaskId taskId,
        int number,
        TaskAttemptState state,
        DateTimeOffset startedAt,
        DateTimeOffset? completedAt = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(taskId);

        if (number <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, "An attempt number must be positive.");
        }

        if (!Enum.IsDefined(typeof(TaskAttemptState), state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "The attempt state is not defined.");
        }

        ValidateTimestamp(startedAt, nameof(startedAt));
        if (completedAt.HasValue)
        {
            ValidateTimestamp(completedAt.Value, nameof(completedAt));
            if (completedAt.Value < startedAt)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(completedAt),
                    completedAt,
                    "An attempt cannot complete before it starts.");
            }
        }

        Id = id;
        TaskId = taskId;
        Number = number;
        State = state;
        StartedAt = startedAt;
        CompletedAt = completedAt;
    }

    public AttemptId Id { get; }

    public TaskId TaskId { get; }

    public int Number { get; }

    public TaskAttemptState State { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset? CompletedAt { get; }

    public TaskAttempt TransitionTo(TaskAttemptState newState, DateTimeOffset? completedAt = null)
    {
        if (!Enum.IsDefined(typeof(TaskAttemptState), newState))
        {
            throw new ArgumentOutOfRangeException(nameof(newState), newState, "The attempt state is not defined.");
        }

        if (IsTerminal(State) && newState == TaskAttemptState.Running)
        {
            throw new InvalidOperationException("A terminal task attempt cannot return to the running state.");
        }

        return new TaskAttempt(Id, TaskId, Number, newState, StartedAt, completedAt ?? CompletedAt);
    }

    private static bool IsTerminal(TaskAttemptState state) =>
        state is TaskAttemptState.Completed
            or TaskAttemptState.Failed
            or TaskAttemptState.Cancelled
            or TaskAttemptState.Unknown;

    private static void ValidateTimestamp(DateTimeOffset value, string parameterName)
    {
        if (value == default)
        {
            throw new ArgumentException("A timestamp must be specified.", parameterName);
        }
    }
}
