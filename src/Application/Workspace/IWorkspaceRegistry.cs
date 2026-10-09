using AiGameStudioWorkstation.Core.Domain.Workspace;

namespace AiGameStudioWorkstation.Application.Workspace;

public interface IWorkspaceRegistry
{
    WorkspaceLookupResult Get(WorkspaceId workspaceId);
}
