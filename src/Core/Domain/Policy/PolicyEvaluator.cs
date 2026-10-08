namespace AiGameStudioWorkstation.Core.Domain.Policy;

public sealed class PolicyEvaluator : IPolicyEvaluator
{
    public PolicyDecision Evaluate(PolicyAction action, PolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);

        var capability = EvaluateCapabilities(action, context);
        var decision = new PolicyDecision(ToDecisionOutcome(capability.Outcome), capability.Reason);
        var ruleEvaluations = new List<PolicyRuleEvaluation>(context.Rules.Count);
        var decisionSource = PolicyEvaluationDecisionSource.Capability;
        int? winningRuleOrder = null;

        for (var index = 0; index < context.Rules.Count; index++)
        {
            var rule = context.Rules[index];
            if (!rule.AppliesTo(action))
            {
                ruleEvaluations.Add(new PolicyRuleEvaluation(
                    index,
                    rule,
                    applies: false,
                    outcome: null,
                    previousOutcome: null,
                    resultingOutcome: null,
                    overridesPreviousDecision: false));
                continue;
            }

            var previousOutcome = decision.Outcome;
            var overridesPreviousDecision = IsMoreRestrictive(rule.Decision, decision);
            if (overridesPreviousDecision)
            {
                decision = rule.Decision;
                decisionSource = PolicyEvaluationDecisionSource.Rule;
                winningRuleOrder = index;
            }

            ruleEvaluations.Add(new PolicyRuleEvaluation(
                index,
                rule,
                applies: true,
                outcome: rule.Decision.Outcome,
                previousOutcome,
                resultingOutcome: decision.Outcome,
                overridesPreviousDecision));
        }

        var trace = new PolicyEvaluationTrace(
            action,
            context.Status,
            capability,
            ruleEvaluations,
            decision.Outcome,
            decision.Reason,
            decisionSource,
            winningRuleOrder);

        return new PolicyDecision(decision.Outcome, decision.Reason, trace);
    }

    private static PolicyCapabilityEvaluation EvaluateCapabilities(PolicyAction action, PolicyContext context)
    {
        if (context.Status == PolicyContextStatus.Incomplete)
        {
            return CreateCapabilityEvaluation(
                null,
                PolicyCapabilityAvailability.NotEvaluated,
                PolicyDecisionOutcome.Escalate,
                "context.incomplete",
                "Policy evaluation cannot continue because the context is incomplete.");
        }

        if (context.Status == PolicyContextStatus.Unknown)
        {
            return CreateCapabilityEvaluation(
                null,
                PolicyCapabilityAvailability.NotEvaluated,
                PolicyDecisionOutcome.Escalate,
                "context.unknown",
                "Policy evaluation cannot continue because the context is unknown.");
        }

        var requiredCapability = GetRequiredCapability(action);
        if (requiredCapability is null)
        {
            return CreateCapabilityEvaluation(
                null,
                PolicyCapabilityAvailability.NotEvaluated,
                PolicyDecisionOutcome.Escalate,
                "action.unknown",
                "Policy evaluation cannot continue because the action is unsupported.");
        }

        var registrations = context.Registrations
            .Where(candidate => candidate.Capability == requiredCapability)
            .ToArray();
        if (registrations.Length == 0)
        {
            return CreateCapabilityEvaluation(
                requiredCapability,
                PolicyCapabilityAvailability.Missing,
                PolicyDecisionOutcome.Escalate,
                "capability.missing",
                "Policy evaluation cannot establish permission because the required capability is not present.");
        }

        var matches = registrations
            .Select(registration => (Registration: registration, Match: registration.Scope.CompareTo(action.Scope)))
            .ToArray();

        if (matches.Any(candidate =>
                candidate.Match == PolicyScopeMatch.Match &&
                candidate.Registration.Provenance.Trust == PolicyCapabilityTrust.Trusted))
        {
            var reason = new PolicyReason(
                "capability.granted",
                "A trusted registration for the required capability includes the requested scope.");
            var evaluations = matches.Select(candidate =>
                candidate.Match == PolicyScopeMatch.Match &&
                candidate.Registration.Provenance.Trust == PolicyCapabilityTrust.Trusted
                    ? RegistrationEvaluation(
                        candidate.Registration,
                        candidate.Match,
                        PolicyEvaluationOutcome.Allow,
                        reason,
                        contributesToCapabilityDecision: true)
                    : RegistrationEvaluation(
                        candidate.Registration,
                        candidate.Match,
                        PolicyEvaluationOutcome.NotApplicable,
                        reason: null,
                        contributesToCapabilityDecision: false));

            return new PolicyCapabilityEvaluation(
                requiredCapability,
                PolicyCapabilityAvailability.Present,
                PolicyEvaluationOutcome.Allow,
                reason,
                evaluations);
        }

        if (matches.All(candidate => candidate.Match == PolicyScopeMatch.NoMatch))
        {
            var reason = new PolicyReason(
                "capability.scope.denied",
                "The requested scope is outside every registered boundary for the required capability.");
            var evaluations = matches.Select(candidate =>
                RegistrationEvaluation(
                    candidate.Registration,
                    candidate.Match,
                    PolicyEvaluationOutcome.Deny,
                    reason,
                    contributesToCapabilityDecision: true));

            return new PolicyCapabilityEvaluation(
                requiredCapability,
                PolicyCapabilityAvailability.Present,
                PolicyEvaluationOutcome.Deny,
                reason,
                evaluations);
        }

        var untrustedPotentialMatchIndex = Array.FindIndex(matches, candidate =>
            candidate.Match != PolicyScopeMatch.NoMatch &&
            candidate.Registration.Provenance.Trust != PolicyCapabilityTrust.Trusted);
        if (untrustedPotentialMatchIndex >= 0)
        {
            var selected = matches[untrustedPotentialMatchIndex];
            var reason = new PolicyReason(
                selected.Registration.Provenance.Trust == PolicyCapabilityTrust.Unknown
                    ? "capability.provenance.unknown"
                    : "capability.provenance.untrusted",
                "Policy evaluation cannot establish permission because the required capability provenance is not trusted.");
            var evaluations = matches.Select((candidate, index) =>
                candidate.Match == PolicyScopeMatch.NoMatch
                    ? RegistrationEvaluation(
                        candidate.Registration,
                        candidate.Match,
                        PolicyEvaluationOutcome.NotApplicable,
                        reason: null,
                        contributesToCapabilityDecision: false)
                    : RegistrationEvaluation(
                        candidate.Registration,
                        candidate.Match,
                        PolicyEvaluationOutcome.Escalate,
                        reason,
                        contributesToCapabilityDecision: index == untrustedPotentialMatchIndex));

            return new PolicyCapabilityEvaluation(
                requiredCapability,
                PolicyCapabilityAvailability.Present,
                PolicyEvaluationOutcome.Escalate,
                reason,
                evaluations);
        }

        var scopeReason = new PolicyReason(
            "capability.scope.unknown",
            "Policy evaluation cannot establish whether the requested scope is within the registered boundary.");
        var scopeEvaluations = matches.Select(candidate =>
            candidate.Match == PolicyScopeMatch.Unknown
                ? RegistrationEvaluation(
                    candidate.Registration,
                    candidate.Match,
                    PolicyEvaluationOutcome.Escalate,
                    scopeReason,
                    contributesToCapabilityDecision: true)
                : RegistrationEvaluation(
                    candidate.Registration,
                    candidate.Match,
                    PolicyEvaluationOutcome.NotApplicable,
                    reason: null,
                    contributesToCapabilityDecision: false));

        return new PolicyCapabilityEvaluation(
            requiredCapability,
            PolicyCapabilityAvailability.Present,
            PolicyEvaluationOutcome.Escalate,
            scopeReason,
            scopeEvaluations);
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
        PolicyDecisionOutcome.Allow => (int)PolicyDecisionPrecedence.Allow,
        PolicyDecisionOutcome.Escalate => (int)PolicyDecisionPrecedence.Escalate,
        PolicyDecisionOutcome.Deny => (int)PolicyDecisionPrecedence.Deny,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The policy decision outcome is not defined.")
    };

    private static PolicyDecisionOutcome ToDecisionOutcome(PolicyEvaluationOutcome outcome) => outcome switch
    {
        PolicyEvaluationOutcome.Allow => PolicyDecisionOutcome.Allow,
        PolicyEvaluationOutcome.Deny => PolicyDecisionOutcome.Deny,
        PolicyEvaluationOutcome.Escalate => PolicyDecisionOutcome.Escalate,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The capability evaluation has no decision outcome.")
    };

    private static PolicyCapabilityEvaluation CreateCapabilityEvaluation(
        PolicyCapability? requiredCapability,
        PolicyCapabilityAvailability availability,
        PolicyDecisionOutcome outcome,
        string reasonCode,
        string explanation)
    {
        var reason = new PolicyReason(reasonCode, explanation);
        return new PolicyCapabilityEvaluation(
            requiredCapability,
            availability,
            ToEvaluationOutcome(outcome),
            reason,
            Array.Empty<PolicyRegistrationEvaluation>());
    }

    private static PolicyEvaluationOutcome ToEvaluationOutcome(PolicyDecisionOutcome outcome) => outcome switch
    {
        PolicyDecisionOutcome.Allow => PolicyEvaluationOutcome.Allow,
        PolicyDecisionOutcome.Deny => PolicyEvaluationOutcome.Deny,
        PolicyDecisionOutcome.Escalate => PolicyEvaluationOutcome.Escalate,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The policy decision outcome is not defined.")
    };

    private static PolicyRegistrationEvaluation RegistrationEvaluation(
        PolicyCapabilityRegistration registration,
        PolicyScopeMatch scopeMatch,
        PolicyEvaluationOutcome outcome,
        PolicyReason? reason,
        bool contributesToCapabilityDecision) =>
        new(registration, scopeMatch, outcome, reason, contributesToCapabilityDecision);
}
