namespace AiGameStudioWorkstation.Core.Domain.Policy;

public enum PolicyDecisionOutcome
{
    Allow,
    Deny,
    Escalate
}

public sealed record PolicyDecision
{
    public PolicyDecision(PolicyDecisionOutcome outcome, PolicyReason reason)
    {
        if (!Enum.IsDefined(typeof(PolicyDecisionOutcome), outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The policy decision outcome is not defined.");
        }

        ArgumentNullException.ThrowIfNull(reason);

        Outcome = outcome;
        Reason = reason;
    }

    public PolicyDecisionOutcome Outcome { get; }

    public PolicyReason Reason { get; }

    public static PolicyDecision Allow(PolicyReason reason) =>
        new(PolicyDecisionOutcome.Allow, reason);

    public static PolicyDecision Deny(PolicyReason reason) =>
        new(PolicyDecisionOutcome.Deny, reason);

    public static PolicyDecision Escalate(PolicyReason reason) =>
        new(PolicyDecisionOutcome.Escalate, reason);
}
