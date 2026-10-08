using AiGameStudioWorkstation.Core.Domain.Policy;

namespace AiGameStudioWorkstation.Core.Tests;

public sealed class PolicyEvaluatorTests
{
    [Theory]
    [InlineData(PolicyDecisionOutcome.Allow)]
    [InlineData(PolicyDecisionOutcome.Deny)]
    [InlineData(PolicyDecisionOutcome.Escalate)]
    public void Represents_each_decision_outcome_with_a_reason(PolicyDecisionOutcome outcome)
    {
        var reason = new PolicyReason("test.reason", "A test policy reason.");

        var decision = outcome switch
        {
            PolicyDecisionOutcome.Allow => PolicyDecision.Allow(reason),
            PolicyDecisionOutcome.Deny => PolicyDecision.Deny(reason),
            PolicyDecisionOutcome.Escalate => PolicyDecision.Escalate(reason),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
        };

        Assert.Equal(outcome, decision.Outcome);
        Assert.Equal(reason, decision.Reason);
    }

    [Fact]
    public void Rejects_undefined_decision_outcomes_and_missing_reasons()
    {
        var reason = new PolicyReason("test.reason", "A test policy reason.");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PolicyDecision((PolicyDecisionOutcome)int.MaxValue, reason));
        Assert.Throws<ArgumentNullException>(() =>
            new PolicyDecision(PolicyDecisionOutcome.Allow, null!));
    }

    [Theory]
    [InlineData("", "A reason.")]
    [InlineData(" ", "A reason.")]
    [InlineData("test.reason", "")]
    [InlineData("test.reason", " ")]
    public void Rejects_empty_reason_codes_or_explanations(string code, string explanation)
    {
        Assert.Throws<ArgumentException>(() => new PolicyReason(code, explanation));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Rejects_empty_action_categories(string category)
    {
        Assert.Throws<ArgumentException>(() => new PolicyAction(category));
    }

    [Fact]
    public void Rejects_undefined_context_status()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PolicyContext((PolicyContextStatus)int.MaxValue));
    }

    [Fact]
    public void Escalates_when_context_is_incomplete()
    {
        var decision = new PolicyEvaluator().Evaluate(
            new PolicyAction("read"),
            new PolicyContext(PolicyContextStatus.Incomplete));

        Assert.Equal(PolicyDecisionOutcome.Escalate, decision.Outcome);
        Assert.Equal("context.incomplete", decision.Reason.Code);
    }

    [Fact]
    public void Escalates_when_context_is_unknown()
    {
        var decision = new PolicyEvaluator().Evaluate(
            new PolicyAction("write"),
            new PolicyContext(PolicyContextStatus.Unknown));

        Assert.Equal(PolicyDecisionOutcome.Escalate, decision.Outcome);
        Assert.Equal("context.unknown", decision.Reason.Code);
    }

    [Fact]
    public void Escalates_when_no_policy_rule_applies()
    {
        var decision = new PolicyEvaluator().Evaluate(
            new PolicyAction("unregistered.action"),
            new PolicyContext(PolicyContextStatus.Complete));

        Assert.Equal(PolicyDecisionOutcome.Escalate, decision.Outcome);
        Assert.Equal("action.unknown", decision.Reason.Code);
    }

    [Fact]
    public void Evaluation_is_deterministic_for_identical_inputs()
    {
        var evaluator = new PolicyEvaluator();
        var action = ScopedAction("read", PolicyScope.Global);
        var context = PolicyContext.FromRegistrations(
            PolicyContextStatus.Complete,
            [TrustedRegistration("workspace.read", PolicyScope.Global)]);

        var first = evaluator.Evaluate(action, context);
        var second = evaluator.Evaluate(action, context);

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("read", "workspace.read")]
    [InlineData("write", "workspace.write")]
    [InlineData("execute", "process.execute")]
    public void Allows_a_known_action_when_its_required_capability_is_present(string action, string capability)
    {
        var decision = new PolicyEvaluator().Evaluate(
            ScopedAction(action, PolicyScope.Global),
            PolicyContext.FromRegistrations(
                PolicyContextStatus.Complete,
                [TrustedRegistration(capability, PolicyScope.Global)]));

        Assert.Equal(PolicyDecisionOutcome.Allow, decision.Outcome);
        Assert.Equal("capability.granted", decision.Reason.Code);
    }

    [Fact]
    public void Matches_action_and_capability_names_without_case_sensitivity()
    {
        var decision = new PolicyEvaluator().Evaluate(
            ScopedAction("READ", PolicyScope.Global),
            PolicyContext.FromRegistrations(
                PolicyContextStatus.Complete,
                [TrustedRegistration(" WORKSPACE.READ ", PolicyScope.Global)]));

        Assert.Equal(PolicyDecisionOutcome.Allow, decision.Outcome);
    }

    [Fact]
    public void Escalates_when_a_known_action_lacks_its_required_capability()
    {
        var decision = new PolicyEvaluator().Evaluate(
            new PolicyAction("read"),
            new PolicyContext(PolicyContextStatus.Complete));

        Assert.Equal(PolicyDecisionOutcome.Escalate, decision.Outcome);
        Assert.Equal("capability.missing", decision.Reason.Code);
    }

    [Fact]
    public void Denies_an_action_when_an_explicit_deny_rule_applies_even_if_capability_is_absent()
    {
        var denial = PolicyDecision.Deny(new PolicyReason(
            "policy.explicit-denial",
            "The action is explicitly prohibited."));
        var context = new PolicyContext(
            PolicyContextStatus.Complete,
            Array.Empty<PolicyCapability>(),
            [new PolicyRule(new PolicyAction("read"), denial)]);

        var decision = new PolicyEvaluator().Evaluate(new PolicyAction("READ"), context);

        Assert.Equal(PolicyDecisionOutcome.Deny, decision.Outcome);
        Assert.Equal("policy.explicit-denial", decision.Reason.Code);
    }

    [Fact]
    public void Explicit_deny_overrides_a_present_capability_and_incomplete_context()
    {
        var denial = PolicyDecision.Deny(new PolicyReason(
            "policy.explicit-denial",
            "The action is explicitly prohibited."));
        var context = new PolicyContext(
            PolicyContextStatus.Incomplete,
            [new PolicyCapability("workspace.read")],
            [new PolicyRule(new PolicyAction("read"), denial)]);

        var decision = new PolicyEvaluator().Evaluate(ScopedAction("read", PolicyScope.Global), context);

        Assert.Equal(PolicyDecisionOutcome.Deny, decision.Outcome);
        Assert.Equal("policy.explicit-denial", decision.Reason.Code);
    }

    [Theory]
    [InlineData(PolicyDecisionOutcome.Allow, PolicyDecisionOutcome.Allow, PolicyDecisionOutcome.Allow)]
    [InlineData(PolicyDecisionOutcome.Allow, PolicyDecisionOutcome.Escalate, PolicyDecisionOutcome.Escalate)]
    [InlineData(PolicyDecisionOutcome.Allow, PolicyDecisionOutcome.Deny, PolicyDecisionOutcome.Deny)]
    [InlineData(PolicyDecisionOutcome.Escalate, PolicyDecisionOutcome.Escalate, PolicyDecisionOutcome.Escalate)]
    [InlineData(PolicyDecisionOutcome.Escalate, PolicyDecisionOutcome.Deny, PolicyDecisionOutcome.Deny)]
    [InlineData(PolicyDecisionOutcome.Deny, PolicyDecisionOutcome.Deny, PolicyDecisionOutcome.Deny)]
    public void Combines_applicable_rules_conservatively(
        PolicyDecisionOutcome firstOutcome,
        PolicyDecisionOutcome secondOutcome,
        PolicyDecisionOutcome expectedOutcome)
    {
        var context = PolicyContext.FromRegistrations(
            PolicyContextStatus.Complete,
            [TrustedRegistration("workspace.read", PolicyScope.Global)],
            [
                new PolicyRule(new PolicyAction("read"), DecisionFor(firstOutcome, "first")),
                new PolicyRule(new PolicyAction("READ"), DecisionFor(secondOutcome, "second"))
            ]);

        var decision = new PolicyEvaluator().Evaluate(ScopedAction("read", PolicyScope.Global), context);

        Assert.Equal(expectedOutcome, decision.Outcome);
    }

    [Fact]
    public void Context_copies_input_collections_and_exposes_read_only_collections()
    {
        var capabilities = new List<PolicyCapability> { new("workspace.read") };
        var rules = new List<PolicyRule>
        {
            new(new PolicyAction("read"), DecisionFor(PolicyDecisionOutcome.Deny, "denial"))
        };
        var context = new PolicyContext(PolicyContextStatus.Complete, capabilities, rules);

        capabilities.Clear();
        rules.Clear();

        Assert.Single(context.Capabilities);
        Assert.Single(context.Rules);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<PolicyCapability>)context.Capabilities).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<PolicyRule>)context.Rules).Clear());
    }

    [Fact]
    public void Context_normalizes_and_deduplicates_capabilities()
    {
        var context = new PolicyContext(
            PolicyContextStatus.Complete,
            [
                new PolicyCapability("WORKSPACE.READ"),
                new PolicyCapability("workspace.read")
            ]);

        Assert.Single(context.Capabilities);
        Assert.Equal("workspace.read", context.Capabilities[0].Value);
    }

    [Theory]
    [InlineData(PolicyCapabilitySource.System, PolicyCapabilityTrust.Trusted)]
    [InlineData(PolicyCapabilitySource.Application, PolicyCapabilityTrust.Untrusted)]
    [InlineData(PolicyCapabilitySource.Unknown, PolicyCapabilityTrust.Unknown)]
    public void Represents_valid_capability_provenance(
        PolicyCapabilitySource source,
        PolicyCapabilityTrust trust)
    {
        var provenance = new PolicyCapabilityProvenance(source, trust);

        Assert.Equal(source, provenance.Source);
        Assert.Equal(trust, provenance.Trust);
    }

    [Fact]
    public void Rejects_undefined_or_inconsistent_capability_provenance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PolicyCapabilityProvenance((PolicyCapabilitySource)int.MaxValue, PolicyCapabilityTrust.Trusted));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PolicyCapabilityProvenance(PolicyCapabilitySource.System, (PolicyCapabilityTrust)int.MaxValue));
        Assert.Throws<ArgumentException>(() =>
            new PolicyCapabilityProvenance(PolicyCapabilitySource.Unknown, PolicyCapabilityTrust.Trusted));
    }

    [Theory]
    [InlineData(PolicyCapabilityTrust.Unknown, "capability.provenance.unknown")]
    [InlineData(PolicyCapabilityTrust.Untrusted, "capability.provenance.untrusted")]
    public void Escalates_when_a_capability_registration_is_not_trusted(
        PolicyCapabilityTrust trust,
        string expectedReason)
    {
        var registration = new PolicyCapabilityRegistration(
            new PolicyCapability("workspace.read"),
            new PolicyCapabilityProvenance(PolicyCapabilitySource.Agent, trust));
        var context = PolicyContext.FromRegistrations(PolicyContextStatus.Complete, [registration]);

        var decision = new PolicyEvaluator().Evaluate(new PolicyAction("read"), context);

        Assert.Equal(PolicyDecisionOutcome.Escalate, decision.Outcome);
        Assert.Equal(expectedReason, decision.Reason.Code);
    }

    [Fact]
    public void Treats_legacy_capability_values_as_unknown_provenance()
    {
        var context = new PolicyContext(
            PolicyContextStatus.Complete,
            [new PolicyCapability("workspace.read")]);

        var decision = new PolicyEvaluator().Evaluate(new PolicyAction("read"), context);

        Assert.Equal(PolicyDecisionOutcome.Escalate, decision.Outcome);
        Assert.Equal("capability.provenance.unknown", decision.Reason.Code);
    }

    [Fact]
    public void Deduplicates_identical_registrations_and_rejects_conflicting_provenance()
    {
        var registration = TrustedRegistration("workspace.read");
        var context = PolicyContext.FromRegistrations(
            PolicyContextStatus.Complete,
            [registration, registration]);

        Assert.Single(context.Registrations);
        Assert.Throws<ArgumentException>(() =>
            PolicyContext.FromRegistrations(
                PolicyContextStatus.Complete,
                [
                    registration,
                    new PolicyCapabilityRegistration(
                        new PolicyCapability("workspace.read"),
                        new PolicyCapabilityProvenance(PolicyCapabilitySource.Tool, PolicyCapabilityTrust.Trusted))
                ]));
    }

    [Fact]
    public void Registered_context_copies_and_exposes_read_only_registrations()
    {
        var registrations = new List<PolicyCapabilityRegistration>
        {
            TrustedRegistration("workspace.read")
        };
        var context = PolicyContext.FromRegistrations(PolicyContextStatus.Complete, registrations);

        registrations.Clear();

        Assert.Single(context.Registrations);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<PolicyCapabilityRegistration>)context.Registrations).Clear());
    }

    [Fact]
    public void Explicit_deny_overrides_trusted_or_unknown_provenance()
    {
        var denial = PolicyDecision.Deny(new PolicyReason(
            "policy.explicit-denial",
            "The action is explicitly prohibited."));
        var rule = new PolicyRule(new PolicyAction("read"), denial);
        var contexts = new[]
        {
            PolicyContext.FromRegistrations(
                PolicyContextStatus.Complete,
                [TrustedRegistration("workspace.read")],
                [rule]),
            PolicyContext.FromRegistrations(
                PolicyContextStatus.Complete,
                [
                    new PolicyCapabilityRegistration(
                        new PolicyCapability("workspace.read"),
                        PolicyCapabilityProvenance.Unknown)
                ],
                [rule])
        };

        foreach (var context in contexts)
        {
            var decision = new PolicyEvaluator().Evaluate(new PolicyAction("read"), context);
            Assert.Equal(PolicyDecisionOutcome.Deny, decision.Outcome);
            Assert.Equal("policy.explicit-denial", decision.Reason.Code);
        }
    }

    [Fact]
    public void Rejects_missing_registration_inputs()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PolicyCapabilityRegistration(null!, PolicyCapabilityProvenance.Unknown));
        Assert.Throws<ArgumentNullException>(() =>
            new PolicyCapabilityRegistration(new PolicyCapability("workspace.read"), null!));
        Assert.Throws<ArgumentNullException>(() =>
            new PolicyCapabilityRegistration(
                new PolicyCapability("workspace.read"),
                PolicyCapabilityProvenance.Unknown,
                null!));
        Assert.Throws<ArgumentNullException>(() =>
            PolicyScope.Bounded(null!));
        Assert.Throws<ArgumentNullException>(() =>
            new PolicyAction("read", null!));
        Assert.Throws<ArgumentNullException>(() =>
            PolicyContext.FromRegistrations(PolicyContextStatus.Complete, null!));
        Assert.Throws<ArgumentException>(() =>
            PolicyContext.FromRegistrations(PolicyContextStatus.Complete, [null!]));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Rejects_empty_capability_values(string value)
    {
        Assert.Throws<ArgumentException>(() => new PolicyCapability(value));
    }

    [Fact]
    public void Rejects_missing_rule_and_context_collection_inputs()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PolicyRule(null!, DecisionFor(PolicyDecisionOutcome.Deny, "denial")));
        Assert.Throws<ArgumentNullException>(() =>
            new PolicyRule(new PolicyAction("read"), null!));
        Assert.Throws<ArgumentNullException>(() =>
            new PolicyContext(PolicyContextStatus.Complete, null!));
        Assert.Throws<ArgumentNullException>(() =>
            new PolicyContext(PolicyContextStatus.Complete, [], null!));
        Assert.Throws<ArgumentException>(() =>
            new PolicyContext(PolicyContextStatus.Complete, [null!]));
        Assert.Throws<ArgumentException>(() =>
            new PolicyContext(PolicyContextStatus.Complete, [], [null!]));
    }

    [Fact]
    public void Rejects_missing_evaluation_inputs()
    {
        var evaluator = new PolicyEvaluator();
        var context = new PolicyContext(PolicyContextStatus.Complete);

        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate(null!, context));
        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate(new PolicyAction("read"), null!));
    }

    [Fact]
    public void Normalizes_scope_identifiers_without_changing_case_or_interior_characters()
    {
        var scopeId = new PolicyScopeId("  Workspace-A / Project B  ");

        Assert.Equal("Workspace-A / Project B", scopeId.Value);
        Assert.Equal(new PolicyScopeId("Workspace-A / Project B"), scopeId);
        Assert.NotEqual(new PolicyScopeId("workspace-A / Project B"), scopeId);
        Assert.Throws<ArgumentException>(() => new PolicyScopeId("  "));
        Assert.Throws<ArgumentException>(() => new PolicyScopeId("workspace-\nA"));
    }

    [Fact]
    public void Represents_unknown_global_and_bounded_scopes_explicitly()
    {
        var bounded = BoundedScope("workspace-A");

        Assert.Equal(PolicyScopeKind.Unknown, PolicyScope.Unknown.Kind);
        Assert.Equal(PolicyScopeKind.Global, PolicyScope.Global.Kind);
        Assert.Equal(PolicyScopeKind.Bounded, bounded.Kind);
        Assert.Equal("workspace-A", bounded.BoundaryId!.Value);
        Assert.NotEqual(PolicyScope.Unknown, PolicyScope.Global);
        Assert.Equal(PolicyScope.Unknown, new PolicyAction("read").Scope);
    }

    [Fact]
    public void Compares_scopes_with_match_mismatch_and_unknown_results()
    {
        var workspaceA = BoundedScope("workspace-A");
        var workspaceB = BoundedScope("workspace-B");

        Assert.Equal(PolicyScopeMatch.Match, workspaceA.CompareTo(BoundedScope("workspace-A")));
        Assert.Equal(PolicyScopeMatch.NoMatch, workspaceA.CompareTo(workspaceB));
        Assert.Equal(PolicyScopeMatch.NoMatch, workspaceA.CompareTo(PolicyScope.Global));
        Assert.Equal(PolicyScopeMatch.Match, PolicyScope.Global.CompareTo(workspaceB));
        Assert.Equal(PolicyScopeMatch.Match, PolicyScope.Global.CompareTo(PolicyScope.Global));
        Assert.Equal(PolicyScopeMatch.Unknown, workspaceA.CompareTo(PolicyScope.Unknown));
        Assert.Equal(PolicyScopeMatch.Unknown, PolicyScope.Unknown.CompareTo(workspaceB));
    }

    [Fact]
    public void Allows_a_trusted_registration_only_when_requested_scope_matches()
    {
        var action = ScopedAction("read", BoundedScope("workspace-A"));
        var context = PolicyContext.FromRegistrations(
            PolicyContextStatus.Complete,
            [TrustedRegistration("workspace.read", BoundedScope("workspace-A"))]);

        var decision = new PolicyEvaluator().Evaluate(action, context);

        Assert.Equal(PolicyDecisionOutcome.Allow, decision.Outcome);
        Assert.Equal("capability.granted", decision.Reason.Code);
    }

    [Fact]
    public void Explicit_global_scope_matches_a_bounded_request()
    {
        var context = PolicyContext.FromRegistrations(
            PolicyContextStatus.Complete,
            [TrustedRegistration("workspace.read", PolicyScope.Global)]);

        var decision = new PolicyEvaluator().Evaluate(
            ScopedAction("read", BoundedScope("workspace-A")),
            context);

        Assert.Equal(PolicyDecisionOutcome.Allow, decision.Outcome);
    }

    [Fact]
    public void Denies_a_known_scope_mismatch_even_with_unknown_provenance()
    {
        var registration = new PolicyCapabilityRegistration(
            new PolicyCapability("workspace.read"),
            PolicyCapabilityProvenance.Unknown,
            BoundedScope("workspace-A"));
        var context = PolicyContext.FromRegistrations(PolicyContextStatus.Complete, [registration]);

        var decision = new PolicyEvaluator().Evaluate(
            ScopedAction("read", BoundedScope("workspace-B")),
            context);

        Assert.Equal(PolicyDecisionOutcome.Deny, decision.Outcome);
        Assert.Equal("capability.scope.denied", decision.Reason.Code);
    }

    [Theory]
    [InlineData(PolicyCapabilityTrust.Unknown, "capability.provenance.unknown")]
    [InlineData(PolicyCapabilityTrust.Untrusted, "capability.provenance.untrusted")]
    public void Escalates_when_matching_registration_provenance_is_not_trusted(
        PolicyCapabilityTrust trust,
        string expectedReason)
    {
        var registration = new PolicyCapabilityRegistration(
            new PolicyCapability("workspace.read"),
            new PolicyCapabilityProvenance(PolicyCapabilitySource.Agent, trust),
            BoundedScope("workspace-A"));
        var context = PolicyContext.FromRegistrations(PolicyContextStatus.Complete, [registration]);

        var decision = new PolicyEvaluator().Evaluate(
            ScopedAction("read", BoundedScope("workspace-A")),
            context);

        Assert.Equal(PolicyDecisionOutcome.Escalate, decision.Outcome);
        Assert.Equal(expectedReason, decision.Reason.Code);
    }

    [Fact]
    public void Escalates_when_requested_or_registered_scope_is_missing()
    {
        var trusted = TrustedRegistration("workspace.read", BoundedScope("workspace-A"));
        var context = PolicyContext.FromRegistrations(PolicyContextStatus.Complete, [trusted]);

        var missingRequestedScope = new PolicyEvaluator().Evaluate(new PolicyAction("read"), context);
        var globalRegistrationWithoutRequestedScope = new PolicyEvaluator().Evaluate(
            new PolicyAction("read"),
            PolicyContext.FromRegistrations(
                PolicyContextStatus.Complete,
                [TrustedRegistration("workspace.read", PolicyScope.Global)]));
        var missingRegistrationScope = new PolicyEvaluator().Evaluate(
            ScopedAction("read", BoundedScope("workspace-A")),
            PolicyContext.FromRegistrations(
                PolicyContextStatus.Complete,
                [TrustedRegistration("workspace.read")]));

        Assert.Equal(PolicyDecisionOutcome.Escalate, missingRequestedScope.Outcome);
        Assert.Equal("capability.scope.unknown", missingRequestedScope.Reason.Code);
        Assert.Equal(PolicyDecisionOutcome.Escalate, globalRegistrationWithoutRequestedScope.Outcome);
        Assert.Equal("capability.scope.unknown", globalRegistrationWithoutRequestedScope.Reason.Code);
        Assert.Equal(PolicyDecisionOutcome.Escalate, missingRegistrationScope.Outcome);
        Assert.Equal("capability.scope.unknown", missingRegistrationScope.Reason.Code);
    }

    [Fact]
    public void Retains_multiple_scopes_for_one_capability_and_allows_any_matching_trusted_scope()
    {
        var registrations = new[]
        {
            TrustedRegistration("workspace.read", BoundedScope("workspace-A")),
            TrustedRegistration("workspace.read", BoundedScope("workspace-B"))
        };
        var context = PolicyContext.FromRegistrations(PolicyContextStatus.Complete, registrations);

        var decision = new PolicyEvaluator().Evaluate(
            ScopedAction("read", BoundedScope("workspace-B")),
            context);

        Assert.Collection(
            context.Registrations,
            registration => Assert.Equal(BoundedScope("workspace-A"), registration.Scope),
            registration => Assert.Equal(BoundedScope("workspace-B"), registration.Scope));
        Assert.Single(context.Capabilities);
        Assert.Equal(PolicyDecisionOutcome.Allow, decision.Outcome);
    }

    [Fact]
    public void Unknown_scope_relationship_prevents_a_decision_from_known_mismatches()
    {
        var context = PolicyContext.FromRegistrations(
            PolicyContextStatus.Complete,
            [
                TrustedRegistration("workspace.read", BoundedScope("workspace-A")),
                TrustedRegistration("workspace.read")
            ]);

        var decision = new PolicyEvaluator().Evaluate(
            ScopedAction("read", BoundedScope("workspace-B")),
            context);

        Assert.Equal(PolicyDecisionOutcome.Escalate, decision.Outcome);
        Assert.Equal("capability.scope.unknown", decision.Reason.Code);
    }

    [Fact]
    public void Deduplicates_identical_scoped_registrations_without_collapsing_distinct_scopes()
    {
        var registrationA = TrustedRegistration("workspace.read", BoundedScope("workspace-A"));
        var registrationB = TrustedRegistration("workspace.read", BoundedScope("workspace-B"));
        var context = PolicyContext.FromRegistrations(
            PolicyContextStatus.Complete,
            [registrationA, registrationA, registrationB]);

        Assert.Collection(
            context.Registrations,
            registration => Assert.Equal(BoundedScope("workspace-A"), registration.Scope),
            registration => Assert.Equal(BoundedScope("workspace-B"), registration.Scope));
        Assert.Equal(
            ["workspace-A", "workspace-B"],
            context.Registrations.Select(registration => registration.Scope.BoundaryId!.Value));
    }

    [Fact]
    public void Copies_scoped_registration_collections_and_exposes_them_read_only()
    {
        var registrations = new List<PolicyCapabilityRegistration>
        {
            TrustedRegistration("workspace.read", BoundedScope("workspace-A"))
        };
        var context = PolicyContext.FromRegistrations(PolicyContextStatus.Complete, registrations);

        registrations.Clear();

        Assert.Single(context.Registrations);
        Assert.Equal("workspace-A", context.Registrations[0].Scope.BoundaryId!.Value);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<PolicyCapabilityRegistration>)context.Registrations).Clear());
    }

    [Fact]
    public void Explicit_deny_overrides_a_matching_scoped_registration()
    {
        var denial = PolicyDecision.Deny(new PolicyReason(
            "policy.explicit-denial",
            "The action is explicitly prohibited."));
        var context = PolicyContext.FromRegistrations(
            PolicyContextStatus.Complete,
            [TrustedRegistration("workspace.read", BoundedScope("workspace-A"))],
            [new PolicyRule(new PolicyAction("read"), denial)]);

        var decision = new PolicyEvaluator().Evaluate(
            ScopedAction("read", BoundedScope("workspace-A")),
            context);

        Assert.Equal(PolicyDecisionOutcome.Deny, decision.Outcome);
        Assert.Equal("policy.explicit-denial", decision.Reason.Code);
    }

    [Fact]
    public void A_less_restrictive_rule_cannot_override_a_scope_denial()
    {
        var allow = DecisionFor(PolicyDecisionOutcome.Allow, "allow");
        var context = PolicyContext.FromRegistrations(
            PolicyContextStatus.Complete,
            [TrustedRegistration("workspace.read", BoundedScope("workspace-A"))],
            [new PolicyRule(new PolicyAction("read"), allow)]);

        var decision = new PolicyEvaluator().Evaluate(
            ScopedAction("read", BoundedScope("workspace-B")),
            context);

        Assert.Equal(PolicyDecisionOutcome.Deny, decision.Outcome);
        Assert.Equal("capability.scope.denied", decision.Reason.Code);
    }

    private static PolicyDecision DecisionFor(PolicyDecisionOutcome outcome, string code)
    {
        var reason = new PolicyReason($"test.{code}", $"The {code} rule applies.");
        return outcome switch
        {
            PolicyDecisionOutcome.Allow => PolicyDecision.Allow(reason),
            PolicyDecisionOutcome.Deny => PolicyDecision.Deny(reason),
            PolicyDecisionOutcome.Escalate => PolicyDecision.Escalate(reason),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
        };
    }

    private static PolicyAction ScopedAction(string category, PolicyScope scope) =>
        new(category, scope);

    private static PolicyScope BoundedScope(string boundaryId) =>
        PolicyScope.Bounded(new PolicyScopeId(boundaryId));

    private static PolicyCapabilityRegistration TrustedRegistration(
        string capability,
        PolicyScope? scope = null) =>
        new(
            new PolicyCapability(capability),
            new PolicyCapabilityProvenance(PolicyCapabilitySource.System, PolicyCapabilityTrust.Trusted),
            scope ?? PolicyScope.Unknown);
}
