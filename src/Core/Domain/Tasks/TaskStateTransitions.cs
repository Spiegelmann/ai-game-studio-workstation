namespace AiGameStudioWorkstation.Core.Domain.Tasks;

public static class TaskStateTransitions
{
    public static bool IsTerminal(TaskState state) =>
        state is TaskState.Accepted or TaskState.Failed or TaskState.Cancelled;

    public static bool IsValidTransition(TaskState from, TaskState to) =>
        from switch
        {
            TaskState.Proposed =>
                to is TaskState.Ready or TaskState.Blocked or TaskState.Cancelled,
            TaskState.Ready =>
                to is TaskState.Claimed or TaskState.Blocked or TaskState.Cancelled,
            TaskState.Claimed =>
                to is TaskState.InProgress or TaskState.Blocked or TaskState.Cancelled or TaskState.Recovering,
            TaskState.InProgress =>
                to is TaskState.Verification or TaskState.Blocked or TaskState.Failed
                    or TaskState.Cancelled or TaskState.Recovering or TaskState.HumanReviewRequired,
            TaskState.Verification =>
                to is TaskState.Accepted or TaskState.Failed or TaskState.Blocked
                    or TaskState.HumanReviewRequired or TaskState.Recovering,
            TaskState.Recovering =>
                to is TaskState.Claimed or TaskState.InProgress or TaskState.HumanReviewRequired or TaskState.Failed,
            TaskState.Blocked =>
                to is TaskState.Ready or TaskState.Cancelled or TaskState.HumanReviewRequired,
            TaskState.HumanReviewRequired =>
                to is TaskState.Ready or TaskState.Cancelled or TaskState.Blocked,
            _ => false
        };
}
