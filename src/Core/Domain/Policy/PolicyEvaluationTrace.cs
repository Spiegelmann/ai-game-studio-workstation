namespace AiGameStudioWorkstation.Core.Domain.Policy;

public sealed class PolicyEvaluationTrace : IEquatable<PolicyEvaluationTrace>
{
    internal PolicyEvaluationTrace(
        PolicyAction action,
        PolicyContextStatus contextStatus,
        PolicyCapabilityEvaluation capability,
        IEnumerable<PolicyRuleEvaluation> rules,
        PolicyDecisionOutcome finalOutcome,
        PolicyReason finalReason,
        PolicyEvaluationDecisionSource decisionSource,
        int? winningRuleOrder)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(finalReason);

        if (!Enum.IsDefined(typeof(PolicyContextStatus), contextStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(contextStatus), contextStatus, "The policy context status is not defined.");
        }

        if (!Enum.IsDefined(typeof(PolicyDecisionOutcome), finalOutcome))
        {
            throw new ArgumentOutOfRangeException(nameof(finalOutcome), finalOutcome, "The policy decision outcome is not defined.");
        }

        if (!Enum.IsDefined(typeof(PolicyEvaluationDecisionSource), decisionSource))
        {
            throw new ArgumentOutOfRangeException(nameof(decisionSource), decisionSource, "The decision source is not defined.");
        }

        var ruleValues = rules.ToArray();
        if (ruleValues.Any(rule => rule is null))
        {
            throw new ArgumentException("A rule evaluation collection must not contain null values.", nameof(rules));
        }

        if (ruleValues.Select(rule => rule.Order).Distinct().Count() != ruleValues.Length ||
            ruleValues.Any(rule => rule.Order >= ruleValues.Length))
        {
            throw new ArgumentException("Rule evaluations must have unique, contiguous ordering.", nameof(rules));
        }

        if ((decisionSource == PolicyEvaluationDecisionSource.Rule) != (winningRuleOrder is not null) ||
            (winningRuleOrder is not null &&
             (winningRuleOrder < 0 ||
              !ruleValues.Any(rule => rule.Order == winningRuleOrder && rule.OverridesPreviousDecision))))
        {
            throw new ArgumentException("The winning rule must identify a rule that overrides the previous decision.", nameof(winningRuleOrder));
        }

        Action = action;
        ContextStatus = contextStatus;
        Capability = capability;
        Rules = Array.AsReadOnly(ruleValues.OrderBy(rule => rule.Order).ToArray());
        FinalOutcome = finalOutcome;
        FinalReason = finalReason;
        DecisionSource = decisionSource;
        WinningRuleOrder = winningRuleOrder;
    }

    public PolicyAction Action { get; }

    public PolicyContextStatus ContextStatus { get; }

    public PolicyCapabilityEvaluation Capability { get; }

    public IReadOnlyList<PolicyRuleEvaluation> Rules { get; }

    public PolicyDecisionOutcome FinalOutcome { get; }

    public PolicyReason FinalReason { get; }

    public PolicyDecisionPrecedence FinalPrecedence => FinalOutcome switch
    {
        PolicyDecisionOutcome.Allow => PolicyDecisionPrecedence.Allow,
        PolicyDecisionOutcome.Escalate => PolicyDecisionPrecedence.Escalate,
        PolicyDecisionOutcome.Deny => PolicyDecisionPrecedence.Deny,
        _ => throw new ArgumentOutOfRangeException(nameof(FinalOutcome), FinalOutcome, "The policy decision outcome is not defined.")
    };

    public PolicyEvaluationDecisionSource DecisionSource { get; }

    public int? WinningRuleOrder { get; }

    public bool Equals(PolicyEvaluationTrace? other) =>
        other is not null &&
        Action == other.Action &&
        ContextStatus == other.ContextStatus &&
        Capability.RequiredCapability == other.Capability.RequiredCapability &&
        Capability.Availability == other.Capability.Availability &&
        Capability.Outcome == other.Capability.Outcome &&
        Capability.Reason == other.Capability.Reason &&
        Capability.Registrations.SequenceEqual(other.Capability.Registrations) &&
        Rules.SequenceEqual(other.Rules) &&
        FinalOutcome == other.FinalOutcome &&
        FinalReason == other.FinalReason &&
        DecisionSource == other.DecisionSource &&
        WinningRuleOrder == other.WinningRuleOrder;

    public override bool Equals(object? obj) =>
        obj is PolicyEvaluationTrace other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Action);
        hash.Add(ContextStatus);
        hash.Add(Capability.RequiredCapability);
        hash.Add(Capability.Availability);
        hash.Add(Capability.Outcome);
        hash.Add(Capability.Reason);

        foreach (var registration in Capability.Registrations)
        {
            hash.Add(registration);
        }

        foreach (var rule in Rules)
        {
            hash.Add(rule);
        }

        hash.Add(FinalOutcome);
        hash.Add(FinalReason);
        hash.Add(DecisionSource);
        hash.Add(WinningRuleOrder);
        return hash.ToHashCode();
    }
}
