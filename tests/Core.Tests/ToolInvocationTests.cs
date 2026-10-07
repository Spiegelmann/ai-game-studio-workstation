using AiGameStudioWorkstation.Core.Domain.Execution;
using AiGameStudioWorkstation.Core.Domain.Tasks;

namespace AiGameStudioWorkstation.Core.Tests;

public sealed class ToolInvocationTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 10, 2, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructs_a_tool_invocation_with_task_attempt_and_capability_references()
    {
        var invocation = CreateInvocation(
            state: ToolInvocationState.Completed,
            startedAt: StartedAt,
            completedAt: StartedAt.AddMinutes(1),
            outcome: InvocationOutcome.Success);

        Assert.Equal(new InvocationId("invocation-1"), invocation.Id);
        Assert.Equal(new TaskId("task-1"), invocation.TaskId);
        Assert.Equal(new AttemptId("attempt-1"), invocation.AttemptId);
        Assert.Equal(new CapabilityId("DotNet.Build"), invocation.CapabilityId);
        Assert.Equal(ToolInvocationState.Completed, invocation.State);
        Assert.Equal(StartedAt, invocation.StartedAt);
        Assert.Equal(StartedAt.AddMinutes(1), invocation.CompletedAt);
        Assert.Equal(InvocationOutcome.Success, invocation.Outcome);
    }

    [Fact]
    public void Rejects_null_identifiers()
    {
        Assert.Throws<ArgumentNullException>(() => new ToolInvocation(
            null!, new TaskId("task-1"), new AttemptId("attempt-1"), new CapabilityId("capability"), ToolInvocationState.Requested));
        Assert.Throws<ArgumentNullException>(() => new ToolInvocation(
            new InvocationId("invocation-1"), null!, new AttemptId("attempt-1"), new CapabilityId("capability"), ToolInvocationState.Requested));
        Assert.Throws<ArgumentNullException>(() => new ToolInvocation(
            new InvocationId("invocation-1"), new TaskId("task-1"), null!, new CapabilityId("capability"), ToolInvocationState.Requested));
        Assert.Throws<ArgumentNullException>(() => new ToolInvocation(
            new InvocationId("invocation-1"), new TaskId("task-1"), new AttemptId("attempt-1"), null!, ToolInvocationState.Requested));
    }

    [Fact]
    public void Rejects_undefined_states_and_outcomes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateInvocation(state: (ToolInvocationState)int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateInvocation(outcome: (InvocationOutcome)int.MaxValue));
    }

    [Fact]
    public void Represents_every_invocation_lifecycle_state()
    {
        Assert.Equal(
            [
                ToolInvocationState.Requested,
                ToolInvocationState.Authorized,
                ToolInvocationState.Started,
                ToolInvocationState.Completed,
                ToolInvocationState.Failed,
                ToolInvocationState.Cancelled,
                ToolInvocationState.TimedOut,
                ToolInvocationState.Unknown
            ],
            Enum.GetValues<ToolInvocationState>());
    }

    [Fact]
    public void Success_failure_and_unknown_outcomes_remain_distinct()
    {
        var outcomes = new[]
        {
            InvocationOutcome.Success,
            InvocationOutcome.Failure,
            InvocationOutcome.Unknown
        };

        Assert.Equal(3, outcomes.Distinct().Count());
    }

    [Fact]
    public void Allows_missing_timestamps_until_they_are_available()
    {
        var invocation = CreateInvocation(state: ToolInvocationState.Requested);

        Assert.Null(invocation.StartedAt);
        Assert.Null(invocation.CompletedAt);
        Assert.Null(invocation.Outcome);
    }

    [Fact]
    public void Rejects_completion_before_start_and_default_timestamps()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateInvocation(
            startedAt: StartedAt,
            completedAt: StartedAt.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() => CreateInvocation(startedAt: new DateTimeOffset()));
        Assert.Throws<ArgumentException>(() => CreateInvocation(completedAt: new DateTimeOffset()));
    }

    private static ToolInvocation CreateInvocation(
        ToolInvocationState state = ToolInvocationState.Started,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? completedAt = null,
        InvocationOutcome? outcome = null) =>
        new(
            new InvocationId("invocation-1"),
            new TaskId("task-1"),
            new AttemptId("attempt-1"),
            new CapabilityId("DotNet.Build"),
            state,
            startedAt,
            completedAt,
            outcome);
}
