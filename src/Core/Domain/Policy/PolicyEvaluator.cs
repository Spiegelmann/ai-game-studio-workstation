namespace AiGameStudioWorkstation.Core.Domain.Policy;

public sealed class PolicyEvaluator : IPolicyEvaluator
{
    public PolicyDecision Evaluate(PolicyAction action, PolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);

        return context.Status switch
        {
            PolicyContextStatus.Incomplete => Escalate(
                "context.incomplete",
                "Policy evaluation cannot continue because the context is incomplete."),
            PolicyContextStatus.Unknown => Escalate(
                "context.unknown",
                "Policy evaluation cannot continue because the context is unknown."),
            PolicyContextStatus.Complete => Escalate(
                "policy.no-applicable-rule",
                "No policy rule is defined for the proposed action."),
            _ => Escalate(
                "context.unknown",
                "Policy evaluation cannot continue because the context status is unsupported.")
        };
    }

    private static PolicyDecision Escalate(string code, string explanation) =>
        PolicyDecision.Escalate(new PolicyReason(code, explanation));
}
