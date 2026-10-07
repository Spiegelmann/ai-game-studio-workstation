using AiGameStudioWorkstation.Core.Domain.Tasks;

namespace AiGameStudioWorkstation.Core.Domain.Execution;

public sealed record HumanDecision
{
    public HumanDecision(
        HumanDecisionId id,
        TaskId taskId,
        HumanDecisionType decisionType,
        string decision,
        string reason,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(taskId);

        if (!Enum.IsDefined(typeof(HumanDecisionType), decisionType))
        {
            throw new ArgumentOutOfRangeException(nameof(decisionType), decisionType, "The human decision type is not defined.");
        }

        if (string.IsNullOrWhiteSpace(decision))
        {
            throw new ArgumentException("A human decision value must not be empty or whitespace.", nameof(decision));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A human decision reason must not be empty or whitespace.", nameof(reason));
        }

        if (timestamp == default)
        {
            throw new ArgumentException("A human decision timestamp must be specified.", nameof(timestamp));
        }

        Id = id;
        TaskId = taskId;
        DecisionType = decisionType;
        Decision = decision;
        Reason = reason;
        Timestamp = timestamp;
    }

    public HumanDecisionId Id { get; }

    public TaskId TaskId { get; }

    public HumanDecisionType DecisionType { get; }

    public string Decision { get; }

    public string Reason { get; }

    public DateTimeOffset Timestamp { get; }
}
