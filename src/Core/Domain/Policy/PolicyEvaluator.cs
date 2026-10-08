namespace AiGameStudioWorkstation.Core.Domain.Policy;

public sealed class PolicyEvaluator : IPolicyEvaluator
{
    public PolicyDecision Evaluate(PolicyAction action, PolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);

        var decision = EvaluateCapabilities(action, context);

        foreach (var rule in context.Rules)
        {
            if (rule.AppliesTo(action) && IsMoreRestrictive(rule.Decision, decision))
            {
                decision = rule.Decision;
            }
        }

        return decision;
    }

    private static PolicyDecision EvaluateCapabilities(PolicyAction action, PolicyContext context)
    {
        if (context.Status == PolicyContextStatus.Incomplete)
        {
            return Escalate(
                "context.incomplete",
                "Policy evaluation cannot continue because the context is incomplete.");
        }

        if (context.Status == PolicyContextStatus.Unknown)
        {
            return Escalate(
                "context.unknown",
                "Policy evaluation cannot continue because the context is unknown.");
        }

        var requiredCapability = GetRequiredCapability(action);
        if (requiredCapability is null)
        {
            return Escalate(
                "action.unknown",
                "Policy evaluation cannot continue because the action is unsupported.");
        }

        var registration = context.Registrations.FirstOrDefault(
            candidate => candidate.Capability == requiredCapability);
        if (registration is null)
        {
            return Escalate(
                "capability.missing",
                "Policy evaluation cannot establish permission because the required capability is not present.");
        }

        if (registration.Provenance.Trust != PolicyCapabilityTrust.Trusted)
        {
            return Escalate(
                registration.Provenance.Trust == PolicyCapabilityTrust.Unknown
                    ? "capability.provenance.unknown"
                    : "capability.provenance.untrusted",
                "Policy evaluation cannot establish permission because the required capability provenance is not trusted.");
        }

        return PolicyDecision.Allow(new PolicyReason(
            "capability.granted",
            "The context contains a trusted registration for the capability required for this action."));
    }

    private static PolicyCapability? GetRequiredCapability(PolicyAction action)
    {
        var value = action.Category.ToUpperInvariant() switch
        {
            "READ" => "workspace.read",
            "WRITE" => "workspace.write",
            "EXECUTE" => "process.execute",
            _ => null
        };

        return value is null ? null : new PolicyCapability(value);
    }

    private static bool IsMoreRestrictive(PolicyDecision candidate, PolicyDecision current) =>
        GetRestriction(candidate.Outcome) > GetRestriction(current.Outcome);

    private static int GetRestriction(PolicyDecisionOutcome outcome) => outcome switch
    {
        PolicyDecisionOutcome.Allow => 0,
        PolicyDecisionOutcome.Escalate => 1,
        PolicyDecisionOutcome.Deny => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The policy decision outcome is not defined.")
    };

    private static PolicyDecision Escalate(string code, string explanation) =>
        PolicyDecision.Escalate(new PolicyReason(code, explanation));
}
