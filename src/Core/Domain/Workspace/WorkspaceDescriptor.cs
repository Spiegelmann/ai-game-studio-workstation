using System.Collections.ObjectModel;
using AiGameStudioWorkstation.Core.Domain.Tasks;

namespace AiGameStudioWorkstation.Core.Domain.Workspace;

public sealed class WorkspaceDescriptor
{
    private readonly ReadOnlyCollection<WorkspaceResourceId> _permittedRoots;
    private readonly ReadOnlyCollection<WorkspaceResourceId> _protectedPaths;
    private readonly ReadOnlyCollection<WorkspaceResourceType> _protectedResourceTypes;

    public WorkspaceDescriptor(
        WorkspaceId id,
        WorkspaceRootId rootId,
        IEnumerable<WorkspaceResourceId> permittedRoots,
        IEnumerable<WorkspaceResourceId> protectedPaths,
        IEnumerable<WorkspaceResourceType> protectedResourceTypes,
        WorkspaceEnvironmentProfileId environmentProfileId,
        TaskScope taskScope)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(rootId);
        ArgumentNullException.ThrowIfNull(environmentProfileId);
        ArgumentNullException.ThrowIfNull(taskScope);

        _permittedRoots = CopyResourceIds(permittedRoots, nameof(permittedRoots));
        if (_permittedRoots.Count == 0)
        {
            throw new ArgumentException("A workspace must define at least one permitted root.", nameof(permittedRoots));
        }

        _protectedPaths = CopyResourceIds(protectedPaths, nameof(protectedPaths));
        _protectedResourceTypes = CopyResourceTypes(protectedResourceTypes);

        Id = id;
        RootId = rootId;
        EnvironmentProfileId = environmentProfileId;
        TaskScope = taskScope;
    }

    public WorkspaceId Id { get; }

    public WorkspaceRootId RootId { get; }

    public IReadOnlyList<WorkspaceResourceId> PermittedRoots => _permittedRoots;

    public IReadOnlyList<WorkspaceResourceId> ProtectedPaths => _protectedPaths;

    public IReadOnlyList<WorkspaceResourceType> ProtectedResourceTypes => _protectedResourceTypes;

    public WorkspaceEnvironmentProfileId EnvironmentProfileId { get; }

    public TaskScope TaskScope { get; }

    private static ReadOnlyCollection<WorkspaceResourceId> CopyResourceIds(
        IEnumerable<WorkspaceResourceId> resourceIds,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(resourceIds, parameterName);
        var copy = resourceIds.ToArray();
        if (copy.Any(resourceId => resourceId is null))
        {
            throw new ArgumentException("A workspace resource collection must not contain null values.", parameterName);
        }

        return Array.AsReadOnly(copy);
    }

    private static ReadOnlyCollection<WorkspaceResourceType> CopyResourceTypes(
        IEnumerable<WorkspaceResourceType> resourceTypes)
    {
        ArgumentNullException.ThrowIfNull(resourceTypes);
        var copy = resourceTypes.ToArray();
        if (copy.Any(resourceType => !Enum.IsDefined(typeof(WorkspaceResourceType), resourceType)))
        {
            throw new ArgumentException(
                "A protected resource type collection must contain only defined resource types.",
                nameof(resourceTypes));
        }

        return Array.AsReadOnly(copy);
    }
}
