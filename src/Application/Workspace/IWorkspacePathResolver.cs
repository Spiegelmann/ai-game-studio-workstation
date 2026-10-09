namespace AiGameStudioWorkstation.Application.Workspace;

public interface IWorkspacePathResolver
{
    WorkspacePathResolutionResult Resolve(WorkspacePathResolutionRequest request);
}
