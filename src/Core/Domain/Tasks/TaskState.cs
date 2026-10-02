namespace AiGameStudioWorkstation.Core.Domain.Tasks;

public enum TaskState
{
    Proposed,
    Ready,
    Claimed,
    InProgress,
    Verification,
    Accepted,
    Blocked,
    Failed,
    Cancelled,
    Recovering,
    HumanReviewRequired
}
