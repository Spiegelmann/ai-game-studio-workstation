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
        : this(outcome, reason, null)
    {
    }

    internal PolicyDecision(
        PolicyDecisionOutcome outcome,
        PolicyReason reason,
        PolicyEvaluationTrace? trace)
    {
        if (!Enum.IsDefined(typeof(PolicyDecisionOutcome), outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The policy decision outcome is not defined.");
        }

        ArgumentNullException.ThrowIfNull(reason);
        if (trace is not null &&
            (trace.FinalOutcome != outcome || trace.FinalReason != reason))
        {
            throw new ArgumentException("The policy evaluation trace must describe this decision.", nameof(trace));
        }

        Outcome = outcome;
        Reason = reason;
        Trace = trace;
    }

    public PolicyDecisionOutcome Outcome { get; }

    public PolicyReason Reason { get; }

    public PolicyEvaluationTrace? Trace { get; }

    public static PolicyDecision Allow(PolicyReason reason) =>
        new(PolicyDecisionOutcome.Allow, reason);

    public static PolicyDecision Deny(PolicyReason reason) =>
        new(PolicyDecisionOutcome.Deny, reason);

    public static PolicyDecision Escalate(PolicyReason reason) =>
        new(PolicyDecisionOutcome.Escalate, reason);
}
