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
        Assert.Equal("policy.no-applicable-rule", decision.Reason.Code);
    }

    [Fact]
    public void Evaluation_is_deterministic_for_identical_inputs()
    {
        var evaluator = new PolicyEvaluator();
        var action = new PolicyAction("modify");
        var context = new PolicyContext(PolicyContextStatus.Complete);

        var first = evaluator.Evaluate(action, context);
        var second = evaluator.Evaluate(action, context);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Rejects_missing_evaluation_inputs()
    {
        var evaluator = new PolicyEvaluator();
        var context = new PolicyContext(PolicyContextStatus.Complete);

        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate(null!, context));
        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate(new PolicyAction("read"), null!));
    }
}
