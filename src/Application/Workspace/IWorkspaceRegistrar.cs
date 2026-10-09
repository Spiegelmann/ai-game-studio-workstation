namespace AiGameStudioWorkstation.Application.Workspace;

public interface IWorkspaceRegistrar
{
    WorkspaceRegistrationResult Register(WorkspaceRegistration registration);
}
