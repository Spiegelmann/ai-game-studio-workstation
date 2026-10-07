using AiGameStudioWorkstation.Core.Domain.Tasks;

namespace AiGameStudioWorkstation.Core.Tests;

public sealed class TaskStateTransitionTests
{
    public static TheoryData<TaskState, TaskState> ValidTransitions => new()
    {
        { TaskState.Proposed, TaskState.Ready },
        { TaskState.Proposed, TaskState.Cancelled },

        { TaskState.Ready, TaskState.Claimed },
        { TaskState.Ready, TaskState.Blocked },
        { TaskState.Ready, TaskState.Cancelled },

        { TaskState.Claimed, TaskState.InProgress },
        { TaskState.Claimed, TaskState.Blocked },
        { TaskState.Claimed, TaskState.Cancelled },
        { TaskState.Claimed, TaskState.Recovering },

        { TaskState.InProgress, TaskState.Verification },
        { TaskState.InProgress, TaskState.Blocked },
        { TaskState.InProgress, TaskState.Failed },
        { TaskState.InProgress, TaskState.Cancelled },
        { TaskState.InProgress, TaskState.Recovering },

        { TaskState.Verification, TaskState.Accepted },
        { TaskState.Verification, TaskState.Failed },
        { TaskState.Verification, TaskState.Blocked },
        { TaskState.Verification, TaskState.HumanReviewRequired },
        { TaskState.Verification, TaskState.Recovering },

        { TaskState.Blocked, TaskState.Ready },
        { TaskState.Blocked, TaskState.Claimed },
        { TaskState.Blocked, TaskState.Recovering },
        { TaskState.Blocked, TaskState.Cancelled },
        { TaskState.Blocked, TaskState.HumanReviewRequired },

        { TaskState.Failed, TaskState.Recovering },
        { TaskState.Failed, TaskState.HumanReviewRequired },
        { TaskState.Failed, TaskState.Cancelled },

        { TaskState.Recovering, TaskState.Ready },
        { TaskState.Recovering, TaskState.Claimed },
        { TaskState.Recovering, TaskState.InProgress },
        { TaskState.Recovering, TaskState.Failed },
        { TaskState.Recovering, TaskState.Blocked },
        { TaskState.Recovering, TaskState.HumanReviewRequired },
        { TaskState.Recovering, TaskState.Cancelled },

        { TaskState.HumanReviewRequired, TaskState.Ready },
        { TaskState.HumanReviewRequired, TaskState.Cancelled },
        { TaskState.HumanReviewRequired, TaskState.Blocked }
    };

    [Theory]
    [MemberData(nameof(ValidTransitions))]
    public void Accepts_each_defined_transition(TaskState from, TaskState to)
    {
        Assert.True(TaskStateTransitions.IsValidTransition(from, to));
    }

    [Fact]
    public void Rejects_every_undefined_transition()
    {
        var validTransitions = ValidTransitions
            .Select(transition => ((TaskState)transition[0]!, (TaskState)transition[1]!))
            .ToHashSet();

        foreach (var from in Enum.GetValues<TaskState>())
        {
            foreach (var to in Enum.GetValues<TaskState>())
            {
                if (!validTransitions.Contains((from, to)))
                {
                    Assert.False(TaskStateTransitions.IsValidTransition(from, to), $"{from} -> {to}");
                }
            }
        }
    }

    [Theory]
    [InlineData(TaskState.Accepted)]
    [InlineData(TaskState.Cancelled)]
    public void Terminal_states_cannot_transition_to_any_state(TaskState from)
    {
        Assert.True(TaskStateTransitions.IsTerminal(from));

        foreach (var to in Enum.GetValues<TaskState>())
        {
            Assert.False(TaskStateTransitions.IsValidTransition(from, to), $"{from} -> {to}");
        }
    }

    [Fact]
    public void Failed_is_recoverable_and_not_terminal()
    {
        Assert.False(TaskStateTransitions.IsTerminal(TaskState.Failed));
        Assert.True(TaskStateTransitions.IsValidTransition(TaskState.Failed, TaskState.Recovering));
    }

    [Fact]
    public void Rejects_undefined_state_values()
    {
        Assert.False(TaskStateTransitions.IsValidTransition((TaskState)int.MaxValue, TaskState.Ready));
    }
}
