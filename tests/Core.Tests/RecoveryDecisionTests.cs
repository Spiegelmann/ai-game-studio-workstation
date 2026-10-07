using AiGameStudioWorkstation.Core.Domain.Execution;
using AiGameStudioWorkstation.Core.Domain.Tasks;
using DomainTask = AiGameStudioWorkstation.Core.Domain.Tasks.Task;

namespace AiGameStudioWorkstation.Core.Tests;

public sealed class RecoveryDecisionTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 10, 2, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Records_a_recovery_decision_without_mutating_task_state()
    {
        var task = CreateTask();
        var decision = CreateDecision();

        Assert.Equal(new RecoveryDecisionId("recovery-1"), decision.Id);
        Assert.Equal(task.Id, decision.TaskId);
        Assert.Equal(new AttemptId("attempt-1"), decision.AttemptId);
        Assert.Equal(FailureClassification.BuildFailure, decision.FailureClassification);
        Assert.Equal(RecoveryAction.Retry, decision.Action);
        Assert.Equal("Retry after correcting the build input.", decision.Reason);
        Assert.Equal(Timestamp, decision.Timestamp);
        Assert.Equal(TaskState.InProgress, task.State);
    }

    [Fact]
    public void Rejects_null_identifiers_and_undefined_decision_values()
    {
        Assert.Throws<ArgumentNullException>(() => new RecoveryDecision(
            null!, new TaskId("task-1"), new AttemptId("attempt-1"),
            FailureClassification.ToolFailure, RecoveryAction.Retry, "reason", Timestamp));
        Assert.Throws<ArgumentNullException>(() => new RecoveryDecision(
            new RecoveryDecisionId("recovery-1"), null!, new AttemptId("attempt-1"),
            FailureClassification.ToolFailure, RecoveryAction.Retry, "reason", Timestamp));
        Assert.Throws<ArgumentNullException>(() => new RecoveryDecision(
            new RecoveryDecisionId("recovery-1"), new TaskId("task-1"), null!,
            FailureClassification.ToolFailure, RecoveryAction.Retry, "reason", Timestamp));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateDecision(
            failureClassification: (FailureClassification)int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateDecision(action: (RecoveryAction)int.MaxValue));
        Assert.Throws<ArgumentException>(() => CreateDecision(reason: " "));
        Assert.Throws<ArgumentException>(() => new RecoveryDecision(
            new RecoveryDecisionId("recovery-1"),
            new TaskId("task-1"),
            new AttemptId("attempt-1"),
            FailureClassification.ToolFailure,
            RecoveryAction.Retry,
            "reason",
            default));
    }

    [Fact]
    public void Represents_all_failure_classifications_and_recovery_actions()
    {
        Assert.Equal(
            [
                FailureClassification.ValidationFailure,
                FailureClassification.PolicyFailure,
                FailureClassification.ScopeFailure,
                FailureClassification.EnvironmentFailure,
                FailureClassification.ToolFailure,
                FailureClassification.ProcessFailure,
                FailureClassification.Timeout,
                FailureClassification.Cancellation,
                FailureClassification.BuildFailure,
                FailureClassification.TestFailure,
                FailureClassification.DependencyFailure,
                FailureClassification.StateFailure,
                FailureClassification.GitFailure,
                FailureClassification.ExternalFailure,
                FailureClassification.SecurityFailure,
                FailureClassification.UnknownFailure
            ],
            Enum.GetValues<FailureClassification>());

        Assert.Equal(
            [
                RecoveryAction.Retry,
                RecoveryAction.Resume,
                RecoveryAction.Rollback,
                RecoveryAction.Restart,
                RecoveryAction.Escalate,
                RecoveryAction.Abandon
            ],
            Enum.GetValues<RecoveryAction>());
    }

    private static RecoveryDecision CreateDecision(
        FailureClassification failureClassification = FailureClassification.BuildFailure,
        RecoveryAction action = RecoveryAction.Retry,
        string reason = "Retry after correcting the build input.",
        DateTimeOffset? timestamp = null) =>
        new(
            new RecoveryDecisionId("recovery-1"),
            new TaskId("task-1"),
            new AttemptId("attempt-1"),
            failureClassification,
            action,
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
            TaskState.InProgress,
            Timestamp,
            new TaskRevision(0));
}
