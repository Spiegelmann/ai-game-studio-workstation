namespace AiGameStudioWorkstation.Core.Domain.Workspace;

public sealed record WorkspaceResourceClassification
{
    public WorkspaceResourceClassification(
        WorkspaceResourceType resourceType,
        WorkspaceResourceBoundary boundary)
    {
        if (!Enum.IsDefined(typeof(WorkspaceResourceType), resourceType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resourceType),
                resourceType,
                "The workspace resource type is not defined.");
        }

        if (!Enum.IsDefined(typeof(WorkspaceResourceBoundary), boundary))
        {
            throw new ArgumentOutOfRangeException(
                nameof(boundary),
                boundary,
                "The workspace resource boundary is not defined.");
        }

        ResourceType = resourceType;
        Boundary = boundary;
    }

    public WorkspaceResourceType ResourceType { get; }

    public WorkspaceResourceBoundary Boundary { get; }
}
