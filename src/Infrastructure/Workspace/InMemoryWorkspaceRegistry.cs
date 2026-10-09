using AiGameStudioWorkstation.Application.Workspace;
using AiGameStudioWorkstation.Core.Domain.Workspace;

namespace AiGameStudioWorkstation.Infrastructure.Workspace;

public sealed class InMemoryWorkspaceRegistry : IWorkspaceRegistry, IWorkspaceRegistrar
{
    private readonly object _gate = new();
    private readonly Dictionary<WorkspaceId, WorkspaceRegistration> _registrations = [];

    public WorkspaceRegistrationResult Register(WorkspaceRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        var workspaceId = registration.Descriptor.Id;
        if (registration.Origin != WorkspaceRegistrationOrigin.HostConfiguration)
        {
            return new WorkspaceRegistrationResult(
                workspaceId,
                WorkspaceRegistrationStatus.RejectedUntrustedOrigin);
        }

        lock (_gate)
        {
            if (_registrations.ContainsKey(workspaceId))
            {
                return new WorkspaceRegistrationResult(workspaceId, WorkspaceRegistrationStatus.Duplicate);
            }

            _registrations.Add(workspaceId, registration);
            return new WorkspaceRegistrationResult(workspaceId, WorkspaceRegistrationStatus.Registered);
        }
    }

    public WorkspaceLookupResult Get(WorkspaceId workspaceId)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);

        lock (_gate)
        {
            return _registrations.TryGetValue(workspaceId, out var registration)
                ? new WorkspaceLookupResult(workspaceId, WorkspaceLookupStatus.Found, registration)
                : new WorkspaceLookupResult(workspaceId, WorkspaceLookupStatus.NotFound, registration: null);
        }
    }
}
