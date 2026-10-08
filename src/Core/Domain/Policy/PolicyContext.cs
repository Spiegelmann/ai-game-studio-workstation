namespace AiGameStudioWorkstation.Core.Domain.Policy;

public enum PolicyContextStatus
{
    Complete,
    Incomplete,
    Unknown
}

public sealed record PolicyContext
{
    public PolicyContext(PolicyContextStatus status)
    {
        if (!Enum.IsDefined(typeof(PolicyContextStatus), status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "The policy context status is not defined.");
        }

        Status = status;
    }

    public PolicyContextStatus Status { get; }
}
