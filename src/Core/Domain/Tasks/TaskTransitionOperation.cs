namespace AiGameStudioWorkstation.Core.Domain.Tasks;

public sealed record TaskTransitionOperation
{
    public TaskTransitionOperation(
        TaskId taskId,
        OperationId operationId,
        TaskRevision expectedRevision,
        TaskState targetState,
        CorrelationId? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(taskId);
        ArgumentNullException.ThrowIfNull(operationId);

        if (!Enum.IsDefined(typeof(TaskState), targetState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetState),
                targetState,
                "The target task state is not defined.");
        }

        TaskId = taskId;
        OperationId = operationId;
        ExpectedRevision = expectedRevision;
        TargetState = targetState;
        CorrelationId = correlationId;
    }

    public TaskId TaskId { get; }

    public OperationId OperationId { get; }

    public TaskRevision ExpectedRevision { get; }

    public TaskState TargetState { get; }

    public CorrelationId? CorrelationId { get; }
}