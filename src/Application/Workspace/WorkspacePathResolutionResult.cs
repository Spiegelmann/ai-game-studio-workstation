using AiGameStudioWorkstation.Core.Domain.Workspace;

namespace AiGameStudioWorkstation.Application.Workspace;

public enum WorkspacePathResolutionStatus
{
    ResolvedWithinPermittedRoot,
    OutsidePermittedRoot,
    Rejected,
    NotFound,
    Indeterminate,
    Failed
}

public enum WorkspacePathTargetKind
{
    ExistingFile,
    ExistingDirectory,
    ProspectiveFile,
    ProspectiveDirectory
}

public enum WorkspacePathResolutionFailure
{
    InvalidPhysicalRoot,
    UnsupportedPhysicalRoot,
    UnsupportedFilesystemConfiguration,
    ResourceOutsidePermittedRoots,
    SymbolicLinkOrReparsePoint,
    NonDirectoryAncestor,
    WrongTargetKind,
    MissingTarget,
    MissingAncestor,
    FilesystemAccessError,
    InvalidPhysicalPath
}

public sealed class WorkspacePathResolutionResult
{
    public WorkspacePathResolutionResult(
        WorkspaceId workspaceId,
        WorkspaceRootId logicalRootId,
        WorkspaceResourceId resourceId,
        WorkspaceOperation operation,
        WorkspacePathResolutionStatus status,
        WorkspaceResourceBoundary containment,
        string? normalizedCandidatePath,
        string? verifiedTargetPath,
        string? verifiedAncestorPath,
        WorkspacePathTargetKind? targetKind,
        WorkspacePathResolutionFailure? failure)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        ArgumentNullException.ThrowIfNull(logicalRootId);
        ArgumentNullException.ThrowIfNull(resourceId);

        if (!Enum.IsDefined(typeof(WorkspaceOperation), operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (!Enum.IsDefined(typeof(WorkspacePathResolutionStatus), status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (!Enum.IsDefined(typeof(WorkspaceResourceBoundary), containment))
        {
            throw new ArgumentOutOfRangeException(nameof(containment));
        }

        if (targetKind is not null && !Enum.IsDefined(typeof(WorkspacePathTargetKind), targetKind.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(targetKind));
        }

        if (failure is not null && !Enum.IsDefined(typeof(WorkspacePathResolutionFailure), failure.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(failure));
        }

        if (status == WorkspacePathResolutionStatus.ResolvedWithinPermittedRoot)
        {
            if (containment != WorkspaceResourceBoundary.WithinPermittedWorkspace ||
                normalizedCandidatePath is null ||
                verifiedAncestorPath is null ||
                targetKind is null ||
                (IsExisting(targetKind.Value) != (verifiedTargetPath is not null)) ||
                failure is not null)
            {
                throw new ArgumentException("A resolved path result must include consistent containment evidence.");
            }
        }
        else if (status == WorkspacePathResolutionStatus.OutsidePermittedRoot)
        {
            if (containment != WorkspaceResourceBoundary.OutsidePermittedWorkspace ||
                normalizedCandidatePath is null ||
                verifiedTargetPath is not null ||
                verifiedAncestorPath is not null ||
                targetKind is not null ||
                failure != WorkspacePathResolutionFailure.ResourceOutsidePermittedRoots)
            {
                throw new ArgumentException("An outside-root result must include consistent boundary evidence.");
            }
        }
        else if (containment != WorkspaceResourceBoundary.Unknown || targetKind is not null ||
                 verifiedTargetPath is not null || verifiedAncestorPath is not null || failure is null)
        {
            throw new ArgumentException(
                "An unresolved path result must preserve unknown containment and provide a failure reason.");
        }

        WorkspaceId = workspaceId;
        LogicalRootId = logicalRootId;
        ResourceId = resourceId;
        Operation = operation;
        Status = status;
        Containment = containment;
        NormalizedCandidatePath = normalizedCandidatePath;
        VerifiedTargetPath = verifiedTargetPath;
        VerifiedAncestorPath = verifiedAncestorPath;
        TargetKind = targetKind;
        Failure = failure;
    }

    public WorkspaceId WorkspaceId { get; }

    public WorkspaceRootId LogicalRootId { get; }

    public WorkspaceResourceId ResourceId { get; }

    public WorkspaceOperation Operation { get; }

    public WorkspacePathResolutionStatus Status { get; }

    public WorkspaceResourceBoundary Containment { get; }

    public string? NormalizedCandidatePath { get; }

    public string? VerifiedTargetPath { get; }

    public string? VerifiedAncestorPath { get; }

    public WorkspacePathTargetKind? TargetKind { get; }

    public WorkspacePathResolutionFailure? Failure { get; }

    private static bool IsExisting(WorkspacePathTargetKind kind) =>
        kind is WorkspacePathTargetKind.ExistingFile or WorkspacePathTargetKind.ExistingDirectory;
}
