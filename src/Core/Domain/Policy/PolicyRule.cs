namespace AiGameStudioWorkstation.Core.Domain.Policy;

public sealed record PolicyRule
{
    public PolicyRule(PolicyAction action, PolicyDecision decision)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(decision);

        Action = action;
        Decision = decision;
    }

    public PolicyAction Action { get; }

    public PolicyDecision Decision { get; }

    public bool AppliesTo(PolicyAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return string.Equals(Action.Category, action.Category, StringComparison.OrdinalIgnoreCase);
    }
}
