using AiGameStudioWorkstation.Core.Domain.Execution;
using AiGameStudioWorkstation.Core.Domain.Tasks;
using DomainTask = AiGameStudioWorkstation.Core.Domain.Tasks.Task;

namespace AiGameStudioWorkstation.Core.Tests;

public sealed class HumanDecisionTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 10, 2, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Records_human_input_without_mutating_task_state_or_authorizing_it()
    {
        var task = CreateTask();
        var decision = CreateDecision();

        Assert.Equal(new HumanDecisionId("human-decision-1"), decision.Id);
        Assert.Equal(task.Id, decision.TaskId);
        Assert.Equal(HumanDecisionType.Approve, decision.DecisionType);
        Assert.Equal("Approved for the stated scope.", decision.Decision);
        Assert.Equal("The requested changes match the task.", decision.Reason);
        Assert.Equal(Timestamp, decision.Timestamp);
        Assert.Equal(TaskState.Ready, task.State);
    }

    [Fact]
    public void Rejects_null_identifiers_and_undefined_or_empty_values()
    {
        Assert.Throws<ArgumentNullException>(() => new HumanDecision(
            null!, new TaskId("task-1"), HumanDecisionType.Approve, "decision", "reason", Timestamp));
        Assert.Throws<ArgumentNullException>(() => new HumanDecision(
            new HumanDecisionId("human-decision-1"), null!, HumanDecisionType.Approve, "decision", "reason", Timestamp));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateDecision(
            decisionType: (HumanDecisionType)int.MaxValue));
        Assert.Throws<ArgumentException>(() => CreateDecision(decision: " "));
        Assert.Throws<ArgumentException>(() => CreateDecision(reason: ""));
        Assert.Throws<ArgumentException>(() => new HumanDecision(
            new HumanDecisionId("human-decision-1"),
            new TaskId("task-1"),
            HumanDecisionType.Approve,
            "decision",
            "reason",
            default));
    }

    [Fact]
    public void Represents_all_required_human_decision_types()
    {
        Assert.Equal(
            [
                HumanDecisionType.Approve,
                HumanDecisionType.Reject,
                HumanDecisionType.Cancel,
                HumanDecisionType.Retry,
                HumanDecisionType.Resume,
                HumanDecisionType.Authorize,
                HumanDecisionType.Deny,
                HumanDecisionType.Override
            ],
            Enum.GetValues<HumanDecisionType>());
    }

    private static HumanDecision CreateDecision(
        HumanDecisionType decisionType = HumanDecisionType.Approve,
        string decision = "Approved for the stated scope.",
        string reason = "The requested changes match the task.",
        DateTimeOffset? timestamp = null) =>
        new(
            new HumanDecisionId("human-decision-1"),
            new TaskId("task-1"),
            decisionType,
            decision,
            reason,
            timestamp ?? Timestamp);

    private static DomainTask CreateTask() =>
        new(
            new TaskId("task-1"),
            "Implement a feature",
            "A task whose state must remain unaffected.",
            [],
            new TaskScope(["src/Core"]),
            [],
            [],
            new ChangeBudget(1, 1, 2, 0, 0),
            TaskState.Ready,
            Timestamp,
            new TaskRevision(0));
}
