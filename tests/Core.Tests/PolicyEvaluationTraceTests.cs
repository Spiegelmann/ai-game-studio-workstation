using AiGameStudioWorkstation.Core.Domain.Policy;

namespace AiGameStudioWorkstation.Core.Tests;

public sealed class PolicyEvaluationTraceTests
{
    [Fact]
    public void Explains_a_trusted_matching_registration_and_final_allow()
    {
        var action = ScopedAction("read", PolicyScope.Global);
        var decision = Evaluate(action, TrustedRegistration("workspace.read", PolicyScope.Global));
        var trace = Assert.IsType<PolicyEvaluationTrace>(decision.Trace);

        Assert.Equal(action, trace.Action);
        Assert.Equal(PolicyCapabilityAvailability.Present, trace.Capability.Availability);
        Assert.Equal("workspace.read", trace.Capability.RequiredCapability!.Value);
        Assert.Equal(PolicyEvaluationOutcome.Allow, trace.Capability.Outcome);
        var registration = Assert.Single(trace.Capability.Registrations);
        Assert.Equal(PolicyScopeMatch.Match, registration.ScopeMatch);
        Assert.Equal(PolicyCapabilityTrust.Trusted, registration.Registration.Provenance.Trust);
        Assert.Equal(PolicyEvaluationOutcome.Allow, registration.Outcome);
        Assert.True(registration.ContributesToCapabilityDecision);
        Assert.Equal(decision.Outcome, trace.FinalOutcome);
        Assert.Equal(decision.Reason, trace.FinalReason);
        Assert.Equal(PolicyDecisionPrecedence.Allow, trace.FinalPrecedence);
        Assert.Equal(PolicyEvaluationDecisionSource.Capability, trace.DecisionSource);
    }

    [Fact]
    public void Explains_an_explicit_deny_rule_even_when_capability_is_missing()
    {
        var denial = PolicyDecision.Deny(new PolicyReason("policy.explicit-denial", "The action is prohibited."));
        var context = PolicyContext.FromRegistrations(
            PolicyContextStatus.Complete,
            Array.Empty<PolicyCapabilityRegistration>(),
            [new PolicyRule(new PolicyAction("read"), denial)]);

        var decision = new PolicyEvaluator().Evaluate(new PolicyAction("read"), context);
        var trace = Assert.IsType<PolicyEvaluationTrace>(decision.Trace);
        var rule = Assert.Single(trace.Rules);

        Assert.Equal(PolicyDecisionOutcome.Deny, decision.Outcome);
        Assert.Equal("policy.explicit-denial", decision.Reason.Code);
        Assert.Equal(PolicyCapabilityAvailability.Missing, trace.Capability.Availability);
        Assert.True(rule.Applies);
        Assert.Equal(PolicyDecisionOutcome.Deny, rule.Outcome);
        Assert.True(rule.OverridesPreviousDecision);
        Assert.Equal(PolicyDecisionOutcome.Escalate, rule.PreviousOutcome);
        Assert.Equal(PolicyDecisionOutcome.Deny, rule.ResultingOutcome);
        Assert.Equal(PolicyEvaluationDecisionSource.Rule, trace.DecisionSource);
        Assert.Equal(rule.Order, trace.WinningRuleOrder);
        Assert.Equal(PolicyDecisionPrecedence.Deny, trace.FinalPrecedence);
    }

    [Theory]
    [InlineData(PolicyCapabilityTrust.Unknown, "capability.provenance.unknown")]
    [InlineData(PolicyCapabilityTrust.Untrusted, "capability.provenance.untrusted")]
    public void Explains_untrusted_or_unknown_capability_provenance(
        PolicyCapabilityTrust trust,
        string expectedReason)
    {
        var registration = new PolicyCapabilityRegistration(
            new PolicyCapability("workspace.read"),
            new PolicyCapabilityProvenance(
                trust == PolicyCapabilityTrust.Unknown ? PolicyCapabilitySource.Unknown : PolicyCapabilitySource.Agent,
                trust),
            PolicyScope.Global);

        var decision = Evaluate(ScopedAction("read", PolicyScope.Global), registration);
        var trace = Assert.IsType<PolicyEvaluationTrace>(decision.Trace);
        var candidate = Assert.Single(trace.Capability.Registrations);

        Assert.Equal(PolicyDecisionOutcome.Escalate, decision.Outcome);
        Assert.Equal(expectedReason, decision.Reason.Code);
        Assert.Equal(trust, candidate.Registration.Provenance.Trust);
        Assert.Equal(PolicyEvaluationOutcome.Escalate, candidate.Outcome);
        Assert.Equal(expectedReason, candidate.Reason!.Code);
        Assert.True(candidate.ContributesToCapabilityDecision);
        Assert.Equal(PolicyDecisionPrecedence.Escalate, trace.FinalPrecedence);
    }

    [Fact]
    public void Explains_known_scope_mismatch_as_a_deny()
    {
        var registration = TrustedRegistration("workspace.read", PolicyScope.Bounded(new PolicyScopeId("workspace-A")));
        var decision = Evaluate(ScopedAction("read", PolicyScope.Bounded(new PolicyScopeId("workspace-B"))), registration);
        var trace = Assert.IsType<PolicyEvaluationTrace>(decision.Trace);
        var candidate = Assert.Single(trace.Capability.Registrations);

        Assert.Equal(PolicyDecisionOutcome.Deny, decision.Outcome);
        Assert.Equal("capability.scope.denied", decision.Reason.Code);
        Assert.Equal(PolicyScopeMatch.NoMatch, candidate.ScopeMatch);
        Assert.Equal(PolicyEvaluationOutcome.Deny, candidate.Outcome);
    }

    [Fact]
    public void Distinguishes_unknown_scope_relationship_from_a_mismatch()
    {
        var registration = TrustedRegistration("workspace.read", PolicyScope.Unknown);
        var decision = Evaluate(ScopedAction("read", PolicyScope.Global), registration);
        var trace = Assert.IsType<PolicyEvaluationTrace>(decision.Trace);
        var candidate = Assert.Single(trace.Capability.Registrations);

        Assert.Equal(PolicyDecisionOutcome.Escalate, decision.Outcome);
        Assert.Equal("capability.scope.unknown", decision.Reason.Code);
        Assert.Equal(PolicyScopeMatch.Unknown, candidate.ScopeMatch);
        Assert.Equal(PolicyEvaluationOutcome.Escalate, candidate.Outcome);
    }

    [Theory]
    [InlineData(PolicyDecisionOutcome.Allow, PolicyDecisionOutcome.Escalate, PolicyDecisionOutcome.Escalate)]
    [InlineData(PolicyDecisionOutcome.Allow, PolicyDecisionOutcome.Deny, PolicyDecisionOutcome.Deny)]
    [InlineData(PolicyDecisionOutcome.Escalate, PolicyDecisionOutcome.Deny, PolicyDecisionOutcome.Deny)]
    [InlineData(PolicyDecisionOutcome.Allow, PolicyDecisionOutcome.Allow, PolicyDecisionOutcome.Allow)]
    public void Explains_precedence_between_capability_and_rule_candidates(
        PolicyDecisionOutcome capabilityOutcome,
        PolicyDecisionOutcome ruleOutcome,
        PolicyDecisionOutcome expectedOutcome)
    {
        var capability = capabilityOutcome == PolicyDecisionOutcome.Allow
            ? TrustedRegistration("workspace.read", PolicyScope.Global)
            : new PolicyCapabilityRegistration(
                new PolicyCapability("workspace.read"),
                PolicyCapabilityProvenance.Unknown,
                PolicyScope.Global);
        var rule = new PolicyRule(new PolicyAction("read"), DecisionFor(ruleOutcome));
        var context = PolicyContext.FromRegistrations(PolicyContextStatus.Complete, [capability], [rule]);

        var decision = new PolicyEvaluator().Evaluate(ScopedAction("read", PolicyScope.Global), context);
        var trace = Assert.IsType<PolicyEvaluationTrace>(decision.Trace);
        var ruleEvaluation = Assert.Single(trace.Rules);
        var overrides = (int)Precedence(ruleOutcome) > (int)Precedence(capabilityOutcome);

        Assert.Equal(expectedOutcome, decision.Outcome);
        Assert.Equal(Precedence(expectedOutcome), trace.FinalPrecedence);
        Assert.Equal(overrides, ruleEvaluation.OverridesPreviousDecision);
        Assert.Equal(
            overrides ? PolicyEvaluationDecisionSource.Rule : PolicyEvaluationDecisionSource.Capability,
            trace.DecisionSource);
        Assert.Equal(overrides ? ruleEvaluation.Order : null, trace.WinningRuleOrder);
    }

    [Fact]
    public void Orders_registration_and_rule_evaluations_deterministically()
    {
        var registrationA = TrustedRegistration("workspace.read", PolicyScope.Bounded(new PolicyScopeId("workspace-A")));
        var registrationB = TrustedRegistration("workspace.read", PolicyScope.Bounded(new PolicyScopeId("workspace-B")));
        var rules = new[]
        {
            new PolicyRule(new PolicyAction("read"), PolicyDecision.Allow(new PolicyReason("policy.allow", "Allow."))),
            new PolicyRule(new PolicyAction("write"), PolicyDecision.Deny(new PolicyReason("policy.deny", "Deny.")))
        };
        var context = PolicyContext.FromRegistrations(
            PolicyContextStatus.Complete,
            [registrationB, registrationA],
            rules);

        var decision = new PolicyEvaluator().Evaluate(
            ScopedAction("read", PolicyScope.Bounded(new PolicyScopeId("workspace-B"))),
            context);
        var trace = Assert.IsType<PolicyEvaluationTrace>(decision.Trace);

        Assert.Equal(PolicyDecisionOutcome.Allow, decision.Outcome);
        Assert.Collection(
            trace.Capability.Registrations,
            candidate => Assert.Equal("workspace-A", candidate.Registration.Scope.BoundaryId!.Value),
            candidate => Assert.Equal("workspace-B", candidate.Registration.Scope.BoundaryId!.Value));
        Assert.Equal([0, 1], trace.Rules.Select(rule => rule.Order));
        Assert.Equal([true, false], trace.Rules.Select(rule => rule.Applies));
        Assert.Equal(PolicyScopeMatch.NoMatch, trace.Capability.Registrations[0].ScopeMatch);
        Assert.Equal(PolicyScopeMatch.Match, trace.Capability.Registrations[1].ScopeMatch);
        Assert.Equal(PolicyEvaluationOutcome.NotApplicable, trace.Capability.Registrations[0].Outcome);
        Assert.Equal(PolicyEvaluationOutcome.Allow, trace.Capability.Registrations[1].Outcome);
    }

    [Fact]
    public void Equivalent_evaluations_have_structurally_equal_decisions_and_traces()
    {
        var action = ScopedAction("read", PolicyScope.Global);
        var context = PolicyContext.FromRegistrations(
            PolicyContextStatus.Complete,
            [TrustedRegistration("workspace.read", PolicyScope.Global)]);
        var evaluator = new PolicyEvaluator();

        var first = evaluator.Evaluate(action, context);
        var second = evaluator.Evaluate(action, context);

        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(first.Reason, second.Reason);
        Assert.Equal(first.Trace, second.Trace);
        Assert.Equal(first.Trace!.GetHashCode(), second.Trace!.GetHashCode());
        Assert.Equal(first, second);
    }

    [Fact]
    public void Equivalent_registration_and_rule_sets_have_order_independent_traces()
    {
        var scopeA = PolicyScope.Bounded(new PolicyScopeId("workspace-A"));
        var scopeB = PolicyScope.Bounded(new PolicyScopeId("workspace-B"));
        var registrationA = TrustedRegistration("workspace.read", scopeA);
        var registrationB = TrustedRegistration("workspace.read", scopeB);
        var reason = new PolicyReason("policy.explicit-denial", "The action is prohibited.");
        var ruleA = new PolicyRule(new PolicyAction("read", scopeA), PolicyDecision.Deny(reason));
        var ruleB = new PolicyRule(new PolicyAction("read", scopeB), PolicyDecision.Deny(reason));
        var evaluator = new PolicyEvaluator();
        var action = ScopedAction("read", scopeB);

        var first = evaluator.Evaluate(
            action,
            PolicyContext.FromRegistrations(
                PolicyContextStatus.Complete,
                [registrationB, registrationA],
                [ruleB, ruleA]));
        var second = evaluator.Evaluate(
            action,
            PolicyContext.FromRegistrations(
                PolicyContextStatus.Complete,
                [registrationA, registrationB],
                [ruleA, ruleB]));
        var trace = Assert.IsType<PolicyEvaluationTrace>(first.Trace);

        Assert.Equal(first, second);
        Assert.Equal(trace, second.Trace);
        Assert.Equal(
            ["workspace-A", "workspace-B"],
            trace.Capability.Registrations.Select(candidate => candidate.Registration.Scope.BoundaryId!.Value));
        Assert.Equal(
            ["workspace-A", "workspace-B"],
            trace.Rules.Select(rule => rule.Rule.Action.Scope.BoundaryId!.Value));
        Assert.Equal(PolicyEvaluationDecisionSource.Rule, trace.DecisionSource);
        Assert.Equal(0, trace.WinningRuleOrder);
    }

    [Fact]
    public void Directly_constructed_decisions_remain_untraced_and_value_equal()
    {
        var reason = new PolicyReason("policy.direct", "Constructed outside the evaluator.");
        var first = PolicyDecision.Allow(reason);
        var second = PolicyDecision.Allow(new PolicyReason("policy.direct", "Constructed outside the evaluator."));

        Assert.Null(first.Trace);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Preserves_input_order_for_rules_tied_on_policy_fields_but_differing_in_embedded_traces()
    {
        var evaluator = new PolicyEvaluator();
        var scopeA = PolicyScope.Bounded(new PolicyScopeId("workspace-A"));
        var scopeB = PolicyScope.Bounded(new PolicyScopeId("workspace-B"));
        var context = PolicyContext.FromRegistrations(
            PolicyContextStatus.Complete,
            [TrustedRegistration("workspace.read", PolicyScope.Global)]);
        var decisionA = evaluator.Evaluate(ScopedAction("read", scopeA), context);
        var decisionB = evaluator.Evaluate(ScopedAction("read", scopeB), context);
        var ruleA = new PolicyRule(new PolicyAction("read"), decisionA);
        var ruleB = new PolicyRule(new PolicyAction("read"), decisionB);

        var forward = evaluator.Evaluate(
            ScopedAction("read", PolicyScope.Global),
            PolicyContext.FromRegistrations(PolicyContextStatus.Complete, context.Registrations, [ruleA, ruleB]));
        var reversed = evaluator.Evaluate(
            ScopedAction("read", PolicyScope.Global),
            PolicyContext.FromRegistrations(PolicyContextStatus.Complete, context.Registrations, [ruleB, ruleA]));
        var forwardTrace = Assert.IsType<PolicyEvaluationTrace>(forward.Trace);
        var reversedTrace = Assert.IsType<PolicyEvaluationTrace>(reversed.Trace);

        Assert.Equal(
            ["workspace-A", "workspace-B"],
            forwardTrace.Rules.Select(rule => rule.Rule.Decision.Trace!.Action.Scope.BoundaryId!.Value));
        Assert.Equal(
            ["workspace-B", "workspace-A"],
            reversedTrace.Rules.Select(rule => rule.Rule.Decision.Trace!.Action.Scope.BoundaryId!.Value));
        Assert.Equal(forward.Outcome, reversed.Outcome);
        Assert.Equal(forward.Reason, reversed.Reason);
        Assert.NotEqual(forwardTrace, reversedTrace);
    }

    [Fact]
    public void Trace_collections_are_copied_and_cannot_be_mutated_by_callers()
    {
        var sourceRegistrations = new List<PolicyCapabilityRegistration>
        {
            TrustedRegistration("workspace.read", PolicyScope.Global)
        };
        var sourceRules = new List<PolicyRule>
        {
            new(new PolicyAction("write"), DecisionFor(PolicyDecisionOutcome.Deny))
        };
        var context = PolicyContext.FromRegistrations(PolicyContextStatus.Complete, sourceRegistrations, sourceRules);
        var trace = Assert.IsType<PolicyEvaluationTrace>(
            new PolicyEvaluator().Evaluate(ScopedAction("read", PolicyScope.Global), context).Trace);

        sourceRegistrations.Clear();
        sourceRules.Clear();

        Assert.Single(trace.Capability.Registrations);
        Assert.Single(trace.Rules);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<PolicyRegistrationEvaluation>)trace.Capability.Registrations).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<PolicyRuleEvaluation>)trace.Rules).Clear());
    }

    [Fact]
    public void Reports_missing_and_unsupported_capability_evaluations()
    {
        var missing = Assert.IsType<PolicyEvaluationTrace>(
            new PolicyEvaluator().Evaluate(
                new PolicyAction("read"),
                new PolicyContext(PolicyContextStatus.Complete)).Trace);
        var unsupported = Assert.IsType<PolicyEvaluationTrace>(
            new PolicyEvaluator().Evaluate(
                new PolicyAction("unmapped"),
                new PolicyContext(PolicyContextStatus.Complete)).Trace);

        Assert.Equal(PolicyCapabilityAvailability.Missing, missing.Capability.Availability);
        Assert.Equal("capability.missing", missing.Capability.Reason.Code);
        Assert.Equal(PolicyCapabilityAvailability.NotEvaluated, unsupported.Capability.Availability);
        Assert.Null(unsupported.Capability.RequiredCapability);
        Assert.Equal("action.unknown", unsupported.Capability.Reason.Code);
    }

    private static PolicyDecision Evaluate(PolicyAction action, PolicyCapabilityRegistration registration) =>
        new PolicyEvaluator().Evaluate(
            action,
            PolicyContext.FromRegistrations(PolicyContextStatus.Complete, [registration]));

    private static PolicyAction ScopedAction(string category, PolicyScope scope) => new(category, scope);

    private static PolicyCapabilityRegistration TrustedRegistration(string capability, PolicyScope scope) =>
        new(
            new PolicyCapability(capability),
            new PolicyCapabilityProvenance(PolicyCapabilitySource.System, PolicyCapabilityTrust.Trusted),
            scope);

    private static PolicyDecision DecisionFor(PolicyDecisionOutcome outcome) =>
        new(
            outcome,
            new PolicyReason($"policy.{outcome.ToString().ToLowerInvariant()}", $"Rule decision: {outcome}."));

    private static PolicyDecisionPrecedence Precedence(PolicyDecisionOutcome outcome) => outcome switch
    {
        PolicyDecisionOutcome.Allow => PolicyDecisionPrecedence.Allow,
        PolicyDecisionOutcome.Escalate => PolicyDecisionPrecedence.Escalate,
        PolicyDecisionOutcome.Deny => PolicyDecisionPrecedence.Deny,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
    };
}
