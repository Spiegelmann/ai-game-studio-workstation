namespace AiGameStudioWorkstation.Core.Domain.Policy;

public enum PolicyCapabilityAvailability
{
    NotEvaluated,
    Missing,
    Present
}

public enum PolicyEvaluationOutcome
{
    NotEvaluated,
    NotApplicable,
    Allow,
    Deny,
    Escalate
}

public enum PolicyDecisionPrecedence
{
    Allow,
    Escalate,
    Deny
}

public enum PolicyEvaluationDecisionSource
{
    Capability,
    Rule
}
