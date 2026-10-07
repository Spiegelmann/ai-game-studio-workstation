namespace AiGameStudioWorkstation.Core.Domain.Tasks;

public enum TaskOperationStatus
{
    Applied,
    AlreadyApplied,
    Rejected
}

public enum TaskOperationRejection
{
    TaskMismatch,
    StaleRevision,
    OperationIdConflict,
    InvalidTransition,
    RevisionExhausted
}

public sealed class TaskOperationResult
{
    private TaskOperationResult(
        TaskOperationStatus status,
        Task task,
        TaskOperationRejection? rejection)
    {
        Status = status;
        Task = task;
        Rejection = rejection;
    }

    public TaskOperationStatus Status { get; }

    public Task Task { get; }

    public TaskOperationRejection? Rejection { get; }

    internal static TaskOperationResult Applied(Task task) =>
        new(TaskOperationStatus.Applied, task, null);

    internal static TaskOperationResult AlreadyApplied(Task task) =>
        new(TaskOperationStatus.AlreadyApplied, task, null);

    internal static TaskOperationResult Rejected(Task task, TaskOperationRejection rejection) =>
        new(TaskOperationStatus.Rejected, task, rejection);
}