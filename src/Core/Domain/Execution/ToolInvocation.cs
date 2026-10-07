using AiGameStudioWorkstation.Core.Domain.Tasks;

namespace AiGameStudioWorkstation.Core.Domain.Execution;

public sealed record ToolInvocation
{
    public ToolInvocation(
        InvocationId id,
        TaskId taskId,
        AttemptId attemptId,
        CapabilityId capabilityId,
        ToolInvocationState state,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? completedAt = null,
        InvocationOutcome? outcome = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(taskId);
        ArgumentNullException.ThrowIfNull(attemptId);
        ArgumentNullException.ThrowIfNull(capabilityId);

        if (!Enum.IsDefined(typeof(ToolInvocationState), state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "The invocation state is not defined.");
        }

        if (outcome.HasValue && !Enum.IsDefined(typeof(InvocationOutcome), outcome.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The invocation outcome is not defined.");
        }

        ValidateOptionalTimestamp(startedAt, nameof(startedAt));
        ValidateOptionalTimestamp(completedAt, nameof(completedAt));
        if (startedAt.HasValue && completedAt.HasValue && completedAt.Value < startedAt.Value)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedAt),
                completedAt,
                "An invocation cannot complete before it starts.");
        }

        Id = id;
        TaskId = taskId;
        AttemptId = attemptId;
        CapabilityId = capabilityId;
        State = state;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        Outcome = outcome;
    }

    public InvocationId Id { get; }

    public TaskId TaskId { get; }

    public AttemptId AttemptId { get; }

    public CapabilityId CapabilityId { get; }

    public ToolInvocationState State { get; }

    public DateTimeOffset? StartedAt { get; }

    public DateTimeOffset? CompletedAt { get; }

    public InvocationOutcome? Outcome { get; }

    private static void ValidateOptionalTimestamp(DateTimeOffset? value, string parameterName)
    {
        if (value.HasValue && value.Value == default)
        {
            throw new ArgumentException("A timestamp must be specified when provided.", parameterName);
        }
    }
}
