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
        var action = new PolicyAction("read");
        var context = new PolicyContext(
            PolicyContextStatus.Complete,
            [new PolicyCapability("workspace.read")]);

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
            new PolicyAction(action),
            new PolicyContext(
                PolicyContextStatus.Complete,
                [new PolicyCapability(capability)]));

        Assert.Equal(PolicyDecisionOutcome.Allow, decision.Outcome);
        Assert.Equal("capability.granted", decision.Reason.Code);
    }

    [Fact]
    public void Matches_action_and_capability_names_without_case_sensitivity()
    {
        var decision = new PolicyEvaluator().Evaluate(
            new PolicyAction("READ"),
            new PolicyContext(
                PolicyContextStatus.Complete,
                [new PolicyCapability(" WORKSPACE.READ ")]));

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

        var decision = new PolicyEvaluator().Evaluate(new PolicyAction("read"), context);

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
        var context = new PolicyContext(
            PolicyContextStatus.Complete,
            [new PolicyCapability("workspace.read")],
            [
                new PolicyRule(new PolicyAction("read"), DecisionFor(firstOutcome, "first")),
                new PolicyRule(new PolicyAction("READ"), DecisionFor(secondOutcome, "second"))
            ]);

        var decision = new PolicyEvaluator().Evaluate(new PolicyAction("read"), context);

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
}
