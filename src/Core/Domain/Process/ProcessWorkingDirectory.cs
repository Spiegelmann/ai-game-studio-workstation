using AiGameStudioWorkstation.Core.Domain.Workspace;

namespace AiGameStudioWorkstation.Core.Domain.Process;

public sealed record ProcessWorkingDirectory
{
    public ProcessWorkingDirectory(WorkspaceId workspaceId, WorkspaceResourceId resourceId)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        ArgumentNullException.ThrowIfNull(resourceId);

        WorkspaceId = workspaceId;
        ResourceId = resourceId;
    }

    public WorkspaceId WorkspaceId { get; }

    public WorkspaceResourceId ResourceId { get; }
}
