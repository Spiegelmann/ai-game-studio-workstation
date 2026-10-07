namespace AiGameStudioWorkstation.Core.Domain.Execution;

public enum RecoveryAction
{
    Retry,
    Resume,
    Rollback,
    Restart,
    Escalate,
    Abandon
}
