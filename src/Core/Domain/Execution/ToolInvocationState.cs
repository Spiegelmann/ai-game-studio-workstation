namespace AiGameStudioWorkstation.Core.Domain.Execution;

public enum ToolInvocationState
{
    Requested,
    Authorized,
    Started,
    Completed,
    Failed,
    Cancelled,
    TimedOut,
    Unknown
}
