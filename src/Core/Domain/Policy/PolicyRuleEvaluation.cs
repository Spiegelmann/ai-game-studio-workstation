namespace AiGameStudioWorkstation.Core.Domain.Policy;

public sealed record PolicyRuleEvaluation
{
    internal PolicyRuleEvaluation(
        int order,
        PolicyRule rule,
        bool applies,
        PolicyDecisionOutcome? outcome,
        PolicyDecisionOutcome? previousOutcome,
        PolicyDecisionOutcome? resultingOutcome,
        bool overridesPreviousDecision)
    {
        if (order < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(order), order, "A rule evaluation order must not be negative.");
        }

        ArgumentNullException.ThrowIfNull(rule);

        if (applies != (outcome is not null) ||
            applies != (previousOutcome is not null) ||
            applies != (resultingOutcome is not null) ||
            (!applies && overridesPreviousDecision))
        {
            throw new ArgumentException("Rule evaluation outcomes must be present only for applicable rules.");
        }

        Order = order;
        Rule = rule;
        Applies = applies;
        Outcome = outcome;
        PreviousOutcome = previousOutcome;
        ResultingOutcome = resultingOutcome;
        OverridesPreviousDecision = overridesPreviousDecision;
    }

    public int Order { get; }

    public PolicyRule Rule { get; }

    public bool Applies { get; }

    public PolicyDecisionOutcome? Outcome { get; }

    public PolicyDecisionOutcome? PreviousOutcome { get; }

    public PolicyDecisionOutcome? ResultingOutcome { get; }

    public bool OverridesPreviousDecision { get; }
}
