using AiGameStudioWorkstation.Core.Domain.Tasks;

namespace AiGameStudioWorkstation.Core.Domain.Workspace;

public sealed record WorkspaceOperationRequest
{
    public WorkspaceOperationRequest(
        WorkspaceId workspaceId,
        WorkspaceOperation operation,
        WorkspaceResourceId resourceId,
        WorkspaceResourceClassification classification,
        OperationId operationId,
        CorrelationId correlationId)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        ArgumentNullException.ThrowIfNull(resourceId);
        ArgumentNullException.ThrowIfNull(classification);
        ArgumentNullException.ThrowIfNull(operationId);
        ArgumentNullException.ThrowIfNull(correlationId);

        if (!Enum.IsDefined(typeof(WorkspaceOperation), operation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation,
                "The workspace operation is not defined.");
        }

        WorkspaceId = workspaceId;
        Operation = operation;
        ResourceId = resourceId;
        Classification = classification;
        OperationId = operationId;
        CorrelationId = correlationId;
    }

    public WorkspaceId WorkspaceId { get; }

    public WorkspaceOperation Operation { get; }

    public WorkspaceResourceId ResourceId { get; }

    public WorkspaceResourceClassification Classification { get; }

    public OperationId OperationId { get; }

    public CorrelationId CorrelationId { get; }
}
