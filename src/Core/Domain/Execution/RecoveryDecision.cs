using AiGameStudioWorkstation.Core.Domain.Tasks;

namespace AiGameStudioWorkstation.Core.Domain.Execution;

public sealed record RecoveryDecision
{
    public RecoveryDecision(
        RecoveryDecisionId id,
        TaskId taskId,
        AttemptId attemptId,
        FailureClassification failureClassification,
        RecoveryAction action,
        string reason,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(taskId);
        ArgumentNullException.ThrowIfNull(attemptId);

        if (!Enum.IsDefined(typeof(FailureClassification), failureClassification))
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureClassification),
                failureClassification,
                "The failure classification is not defined.");
        }

        if (!Enum.IsDefined(typeof(RecoveryAction), action))
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "The recovery action is not defined.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A recovery decision reason must not be empty or whitespace.", nameof(reason));
        }

        if (timestamp == default)
        {
            throw new ArgumentException("A recovery decision timestamp must be specified.", nameof(timestamp));
        }

        Id = id;
        TaskId = taskId;
        AttemptId = attemptId;
        FailureClassification = failureClassification;
        Action = action;
        Reason = reason;
        Timestamp = timestamp;
    }

    public RecoveryDecisionId Id { get; }

    public TaskId TaskId { get; }

    public AttemptId AttemptId { get; }

    public FailureClassification FailureClassification { get; }

    public RecoveryAction Action { get; }

    public string Reason { get; }

    public DateTimeOffset Timestamp { get; }
}
