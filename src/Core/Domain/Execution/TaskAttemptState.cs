namespace AiGameStudioWorkstation.Core.Domain.Execution;

public enum TaskAttemptState
{
    Created,
    Running,
    Completed,
    Failed,
    Cancelled,
    Unknown
}
