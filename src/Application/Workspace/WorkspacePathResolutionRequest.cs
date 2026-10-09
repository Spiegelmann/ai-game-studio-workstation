using AiGameStudioWorkstation.Core.Domain.Workspace;

namespace AiGameStudioWorkstation.Application.Workspace;

public sealed class WorkspacePathResolutionRequest
{
    public WorkspacePathResolutionRequest(
        WorkspaceDescriptor workspace,
        string configuredPhysicalRoot,
        WorkspaceOperationRequest operationRequest)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredPhysicalRoot);
        ArgumentNullException.ThrowIfNull(operationRequest);

        if (workspace.Id != operationRequest.WorkspaceId)
        {
            throw new ArgumentException(
                "The operation request workspace does not match the configured workspace.",
                nameof(operationRequest));
        }

        Workspace = workspace;
        ConfiguredPhysicalRoot = configuredPhysicalRoot;
        OperationRequest = operationRequest;
    }

    public WorkspaceDescriptor Workspace { get; }

    public string ConfiguredPhysicalRoot { get; }

    public WorkspaceOperationRequest OperationRequest { get; }
}
