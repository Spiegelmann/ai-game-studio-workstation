using AiGameStudioWorkstation.Application.Workspace;
using AiGameStudioWorkstation.Core.Domain.Tasks;
using AiGameStudioWorkstation.Core.Domain.Workspace;

namespace AiGameStudioWorkstation.Application.Tests;

public sealed class WorkspacePathResolutionContractTests
{
    [Fact]
    public void Resolution_request_reuses_the_operation_request_and_rejects_workspace_mismatch()
    {
        var workspace = CreateWorkspace(new WorkspaceId("workspace-a"));
        var operationRequest = CreateOperationRequest(new WorkspaceId("workspace-b"));

        Assert.Throws<ArgumentException>(() =>
            new WorkspacePathResolutionRequest(workspace, Path.GetTempPath(), operationRequest));
    }

    [Fact]
    public void Indeterminate_resolution_cannot_claim_containment_or_a_verified_target()
    {
        var request = CreateOperationRequest(new WorkspaceId("workspace-a"));

        Assert.Throws<ArgumentException>(() => new WorkspacePathResolutionResult(
            request.WorkspaceId,
            new WorkspaceRootId("root"),
            request.ResourceId,
            request.Operation,
            WorkspacePathResolutionStatus.Indeterminate,
            WorkspaceResourceBoundary.WithinPermittedWorkspace,
            Path.GetFullPath("src/file.txt"),
            verifiedTargetPath: null,
            verifiedAncestorPath: null,
            targetKind: null,
            WorkspacePathResolutionFailure.FilesystemAccessError));
    }

    [Fact]
    public void Prospective_resolution_records_candidate_and_verified_ancestor_separately()
    {
        var request = CreateOperationRequest(new WorkspaceId("workspace-a"));
        var candidatePath = Path.GetFullPath("src/new.txt");
        var ancestorPath = Path.GetFullPath("src");

        var result = new WorkspacePathResolutionResult(
            request.WorkspaceId,
            new WorkspaceRootId("root"),
            request.ResourceId,
            WorkspaceOperation.Write,
            WorkspacePathResolutionStatus.ResolvedWithinPermittedRoot,
            WorkspaceResourceBoundary.WithinPermittedWorkspace,
            candidatePath,
            verifiedTargetPath: null,
            ancestorPath,
            WorkspacePathTargetKind.ProspectiveFile,
            failure: null);

        Assert.Equal(candidatePath, result.NormalizedCandidatePath);
        Assert.Null(result.VerifiedTargetPath);
        Assert.Equal(ancestorPath, result.VerifiedAncestorPath);
        Assert.Equal(WorkspacePathTargetKind.ProspectiveFile, result.TargetKind);
    }

    private static WorkspaceDescriptor CreateWorkspace(WorkspaceId workspaceId) =>
        new(
            workspaceId,
            new WorkspaceRootId("root"),
            [new WorkspaceResourceId("src")],
            [],
            [],
            new WorkspaceEnvironmentProfileId("test"),
            new TaskScope(["src"]));

    private static WorkspaceOperationRequest CreateOperationRequest(WorkspaceId workspaceId) =>
        new(
            workspaceId,
            WorkspaceOperation.Write,
            new WorkspaceResourceId("src/file.txt"),
            new WorkspaceResourceClassification(
                WorkspaceResourceType.Unknown,
                WorkspaceResourceBoundary.Unknown),
            new OperationId("operation"),
            new CorrelationId("correlation"));
}
