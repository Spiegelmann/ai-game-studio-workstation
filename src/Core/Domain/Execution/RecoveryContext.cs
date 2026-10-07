namespace AiGameStudioWorkstation.Core.Domain.Execution;

public sealed record RecoveryContext
{
    public RecoveryContext(
        string reason,
        FailureClassification? failureClassification = null,
        InvocationId? relevantInvocationId = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A recovery context reason must not be empty or whitespace.", nameof(reason));
        }

        if (failureClassification.HasValue
            && !Enum.IsDefined(typeof(FailureClassification), failureClassification.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureClassification),
                failureClassification,
                "The failure classification is not defined.");
        }

        if (notes is not null && string.IsNullOrWhiteSpace(notes))
        {
            throw new ArgumentException("Recovery notes must not be whitespace when provided.", nameof(notes));
        }

        Reason = reason;
        FailureClassification = failureClassification;
        RelevantInvocationId = relevantInvocationId;
        Notes = notes;
    }

    public string Reason { get; }

    public FailureClassification? FailureClassification { get; }

    public InvocationId? RelevantInvocationId { get; }

    public string? Notes { get; }
}
