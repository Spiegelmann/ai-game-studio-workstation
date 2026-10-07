using AiGameStudioWorkstation.Core.Domain.Execution;
using AiGameStudioWorkstation.Core.Domain.Tasks;

namespace AiGameStudioWorkstation.Core.Tests;

public sealed class StateTransitionTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 10, 2, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Records_an_immutable_task_state_change()
    {
        var transition = CreateTransition();

        Assert.Equal(new TransitionId("transition-1"), transition.Id);
        Assert.Equal(new TaskId("task-1"), transition.TaskId);
        Assert.Equal(TaskState.Proposed, transition.PreviousState);
        Assert.Equal(TaskState.Ready, transition.NextState);
        Assert.Equal(Timestamp, transition.Timestamp);
        Assert.Equal("Task dependencies are satisfied.", transition.Reason);
    }

    [Fact]
    public void Rejects_null_identifiers()
    {
        Assert.Throws<ArgumentNullException>(() => new StateTransition(
            null!, new TaskId("task-1"), TaskState.Proposed, TaskState.Ready, Timestamp, "reason"));
        Assert.Throws<ArgumentNullException>(() => new StateTransition(
            new TransitionId("transition-1"), null!, TaskState.Proposed, TaskState.Ready, Timestamp, "reason"));
    }

    [Fact]
    public void Rejects_undefined_states_identical_states_and_invalid_timestamps()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTransition(previousState: (TaskState)int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTransition(nextState: (TaskState)int.MaxValue));
        Assert.Throws<ArgumentException>(() => CreateTransition(nextState: TaskState.Proposed));
        Assert.Throws<ArgumentException>(() => new StateTransition(
            new TransitionId("transition-1"),
            new TaskId("task-1"),
            TaskState.Proposed,
            TaskState.Ready,
            default,
            "reason"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Rejects_an_empty_reason(string reason)
    {
        Assert.Throws<ArgumentException>(() => CreateTransition(reason: reason));
    }

    [Fact]
    public void Does_not_apply_or_validate_the_task_transition()
    {
        var transition = CreateTransition(
            previousState: TaskState.Accepted,
            nextState: TaskState.InProgress);

        Assert.Equal(TaskState.Accepted, transition.PreviousState);
        Assert.Equal(TaskState.InProgress, transition.NextState);
    }

    private static StateTransition CreateTransition(
        TaskState previousState = TaskState.Proposed,
        TaskState nextState = TaskState.Ready,
        DateTimeOffset? timestamp = null,
        string reason = "Task dependencies are satisfied.") =>
        new(
            new TransitionId("transition-1"),
            new TaskId("task-1"),
            previousState,
            nextState,
            timestamp ?? Timestamp,
            reason);
}
