namespace AiGameStudioWorkstation.Core.Domain.Policy;

public sealed record PolicyRegistrationEvaluation
{
    internal PolicyRegistrationEvaluation(
        PolicyCapabilityRegistration registration,
        PolicyScopeMatch scopeMatch,
        PolicyEvaluationOutcome outcome,
        PolicyReason? reason,
        bool contributesToCapabilityDecision)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (!Enum.IsDefined(typeof(PolicyScopeMatch), scopeMatch))
        {
            throw new ArgumentOutOfRangeException(nameof(scopeMatch), scopeMatch, "The scope match is not defined.");
        }

        if (!Enum.IsDefined(typeof(PolicyEvaluationOutcome), outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The evaluation outcome is not defined.");
        }

        if ((outcome is PolicyEvaluationOutcome.Allow or PolicyEvaluationOutcome.Deny or PolicyEvaluationOutcome.Escalate) &&
            reason is null)
        {
            throw new ArgumentNullException(nameof(reason));
        }

        Registration = registration;
        ScopeMatch = scopeMatch;
        Outcome = outcome;
        Reason = reason;
        ContributesToCapabilityDecision = contributesToCapabilityDecision;
    }

    public PolicyCapabilityRegistration Registration { get; }

    public PolicyScopeMatch ScopeMatch { get; }

    public PolicyEvaluationOutcome Outcome { get; }

    public PolicyReason? Reason { get; }

    public bool ContributesToCapabilityDecision { get; }
}
