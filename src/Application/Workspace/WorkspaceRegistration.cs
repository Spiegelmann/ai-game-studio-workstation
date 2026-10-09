using AiGameStudioWorkstation.Core.Domain.Workspace;

namespace AiGameStudioWorkstation.Application.Workspace;

public enum WorkspaceRegistrationOrigin
{
    HostConfiguration,
    AgentRequest,
    Unknown
}

public sealed class WorkspaceRegistration
{
    public WorkspaceRegistration(
        WorkspaceDescriptor descriptor,
        string configuredPhysicalRoot,
        WorkspaceRegistrationOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredPhysicalRoot);

        if (!Enum.IsDefined(typeof(WorkspaceRegistrationOrigin), origin))
        {
            throw new ArgumentOutOfRangeException(nameof(origin), origin, "The registration origin is not defined.");
        }

        Descriptor = descriptor;
        ConfiguredPhysicalRoot = configuredPhysicalRoot;
        Origin = origin;
    }

    public WorkspaceDescriptor Descriptor { get; }

    public string ConfiguredPhysicalRoot { get; }

    public WorkspaceRegistrationOrigin Origin { get; }
}

public enum WorkspaceRegistrationStatus
{
    Registered,
    Duplicate,
    RejectedUntrustedOrigin
}

public sealed class WorkspaceRegistrationResult
{
    public WorkspaceRegistrationResult(WorkspaceId workspaceId, WorkspaceRegistrationStatus status)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);

        if (!Enum.IsDefined(typeof(WorkspaceRegistrationStatus), status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The workspace registration status is not defined.");
        }

        WorkspaceId = workspaceId;
        Status = status;
    }

    public WorkspaceId WorkspaceId { get; }

    public WorkspaceRegistrationStatus Status { get; }
}

public enum WorkspaceLookupStatus
{
    Found,
    NotFound
}

public sealed class WorkspaceLookupResult
{
    public WorkspaceLookupResult(
        WorkspaceId workspaceId,
        WorkspaceLookupStatus status,
        WorkspaceRegistration? registration)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);

        if (!Enum.IsDefined(typeof(WorkspaceLookupStatus), status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "The workspace lookup status is not defined.");
        }

        if ((status == WorkspaceLookupStatus.Found) != (registration is not null) ||
            (registration is not null && registration.Descriptor.Id != workspaceId))
        {
            throw new ArgumentException("The workspace lookup result must be consistent with its identity and status.");
        }

        WorkspaceId = workspaceId;
        Status = status;
        Registration = registration;
    }

    public WorkspaceId WorkspaceId { get; }

    public WorkspaceLookupStatus Status { get; }

    public WorkspaceRegistration? Registration { get; }
}
