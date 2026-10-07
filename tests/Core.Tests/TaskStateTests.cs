using AiGameStudioWorkstation.Core.Domain.Tasks;

namespace AiGameStudioWorkstation.Core.Tests;

public sealed class TaskStateTests
{
    [Fact]
    public void Defines_all_current_task_states()
    {
        Assert.Equal(
            new[]
            {
                TaskState.Proposed,
                TaskState.Ready,
                TaskState.Claimed,
                TaskState.InProgress,
                TaskState.Verification,
                TaskState.Accepted,
                TaskState.Blocked,
                TaskState.Failed,
                TaskState.Cancelled,
                TaskState.Recovering,
                TaskState.HumanReviewRequired
            },
            Enum.GetValues<TaskState>());
    }

    [Theory]
    [InlineData(TaskState.Accepted)]
    [InlineData(TaskState.Cancelled)]
    public void Identifies_terminal_states(TaskState state)
    {
        Assert.True(TaskStateTransitions.IsTerminal(state));
    }

    [Theory]
    [InlineData(TaskState.Proposed)]
    [InlineData(TaskState.Ready)]
    [InlineData(TaskState.Claimed)]
    [InlineData(TaskState.InProgress)]
    [InlineData(TaskState.Verification)]
    [InlineData(TaskState.Failed)]
    [InlineData(TaskState.Blocked)]
    [InlineData(TaskState.Recovering)]
    [InlineData(TaskState.HumanReviewRequired)]
    public void Identifies_non_terminal_states(TaskState state)
    {
        Assert.False(TaskStateTransitions.IsTerminal(state));
    }
}
