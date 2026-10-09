using AiGameStudioWorkstation.Core.Domain.Tasks;
using AiGameStudioWorkstation.Core.Domain.Workspace;

namespace AiGameStudioWorkstation.Core.Tests;

public sealed class WorkspaceContractTests
{
    [Fact]
    public void Workspace_identifiers_reject_incomplete_values_and_preserve_valid_identity()
    {
        Assert.Throws<ArgumentException>(() => new WorkspaceId(" "));
        Assert.Throws<ArgumentException>(() => new WorkspaceId("workspace\nid"));
        Assert.Throws<ArgumentException>(() => new WorkspaceRootId(""));
        Assert.Throws<ArgumentException>(() => new WorkspaceEnvironmentProfileId("\t"));

        Assert.Equal("workspace-a", new WorkspaceId(" workspace-a ").Value);
        Assert.Equal("repository", new WorkspaceRootId("repository").Value);
        Assert.Equal("dev", new WorkspaceEnvironmentProfileId("dev").Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("/workspace/file")]
    [InlineData("\\workspace\\file")]
    [InlineData("C:\\workspace\\file")]
    [InlineData("src/../secrets.txt")]
    [InlineData("src/./file.txt")]
    [InlineData("src//file.txt")]
    [InlineData("src\\")]
    [InlineData("src/file\n.txt")]
    public void Workspace_resource_identifiers_reject_incomplete_or_non_relative_values(string value)
    {
        Assert.Throws<ArgumentException>(() => new WorkspaceResourceId(value));
    }

    [Fact]
    public void Workspace_resource_identifier_preserves_its_semantic_relative_value()
    {
        var resourceId = new WorkspaceResourceId("src/Core/Domain/Task.cs");

        Assert.Equal("src/Core/Domain/Task.cs", resourceId.Value);
    }

    [Fact]
    public void Workspace_descriptor_copies_and_exposes_read_only_boundary_collections()
    {
        var permittedRoots = new List<WorkspaceResourceId> { new("src") };
        var protectedPaths = new List<WorkspaceResourceId> { new("src/secrets") };
        var protectedTypes = new List<WorkspaceResourceType> { WorkspaceResourceType.Config };
        var scope = new TaskScope(["src"]);

        var descriptor = new WorkspaceDescriptor(
            new WorkspaceId("workspace-a"),
            new WorkspaceRootId("repository"),
            permittedRoots,
            protectedPaths,
            protectedTypes,
            new WorkspaceEnvironmentProfileId("dev"),
            scope);

        permittedRoots.Add(new WorkspaceResourceId("tests"));
        protectedPaths.Clear();
        protectedTypes.Clear();

        Assert.Equal([new WorkspaceResourceId("src")], descriptor.PermittedRoots);
        Assert.Equal([new WorkspaceResourceId("src/secrets")], descriptor.ProtectedPaths);
        Assert.Equal([WorkspaceResourceType.Config], descriptor.ProtectedResourceTypes);
        Assert.Same(scope, descriptor.TaskScope);
        Assert.Throws<NotSupportedException>(
            () => ((IList<WorkspaceResourceId>)descriptor.PermittedRoots)[0] = new WorkspaceResourceId("tests"));
        Assert.Throws<NotSupportedException>(
            () => ((IList<WorkspaceResourceType>)descriptor.ProtectedResourceTypes)[0] = WorkspaceResourceType.Source);
    }

    [Fact]
    public void Workspace_descriptor_rejects_missing_identity_scope_and_permitted_roots()
    {
        var args = CreateDescriptorArguments();
        Assert.Throws<ArgumentNullException>(() => new WorkspaceDescriptor(
            null!,
            args.RootId,
            args.PermittedRoots,
            args.ProtectedPaths,
            args.ProtectedResourceTypes,
            args.EnvironmentProfileId,
            args.TaskScope));
        Assert.Throws<ArgumentNullException>(() => new WorkspaceDescriptor(
            args.Id,
            args.RootId,
            args.PermittedRoots,
            args.ProtectedPaths,
            args.ProtectedResourceTypes,
            args.EnvironmentProfileId,
            null!));
        Assert.Throws<ArgumentException>(() => new WorkspaceDescriptor(
            args.Id,
            args.RootId,
            [],
            args.ProtectedPaths,
            args.ProtectedResourceTypes,
            args.EnvironmentProfileId,
            args.TaskScope));
        Assert.Throws<ArgumentException>(() => new WorkspaceDescriptor(
            args.Id,
            args.RootId,
            [null!],
            args.ProtectedPaths,
            args.ProtectedResourceTypes,
            args.EnvironmentProfileId,
            args.TaskScope));
        Assert.Throws<ArgumentException>(() => new WorkspaceDescriptor(
            args.Id,
            args.RootId,
            args.PermittedRoots,
            args.ProtectedPaths,
            [(WorkspaceResourceType)int.MaxValue],
            args.EnvironmentProfileId,
            args.TaskScope));
    }

    [Theory]
    [InlineData(WorkspaceResourceType.Source, WorkspaceResourceBoundary.WithinPermittedWorkspace)]
    [InlineData(WorkspaceResourceType.Config, WorkspaceResourceBoundary.Protected)]
    [InlineData(WorkspaceResourceType.External, WorkspaceResourceBoundary.OutsidePermittedWorkspace)]
    [InlineData(WorkspaceResourceType.Unknown, WorkspaceResourceBoundary.Unknown)]
    public void Resource_classification_preserves_resource_type_and_boundary(
        WorkspaceResourceType resourceType,
        WorkspaceResourceBoundary boundary)
    {
        var classification = new WorkspaceResourceClassification(resourceType, boundary);

        Assert.Equal(resourceType, classification.ResourceType);
        Assert.Equal(boundary, classification.Boundary);
    }

    [Fact]
    public void Resource_classification_rejects_undefined_values()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorkspaceResourceClassification((WorkspaceResourceType)int.MaxValue, WorkspaceResourceBoundary.Unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorkspaceResourceClassification(WorkspaceResourceType.Unknown, (WorkspaceResourceBoundary)int.MaxValue));
    }

    [Fact]
    public void Workspace_operations_are_limited_to_the_semantic_mvp_operations()
    {
        Assert.Equal(
            [
                WorkspaceOperation.Read,
                WorkspaceOperation.List,
                WorkspaceOperation.Write,
                WorkspaceOperation.CreateDirectory
            ],
            Enum.GetValues<WorkspaceOperation>());
    }

    [Fact]
    public void Operation_request_reuses_correlation_identifiers_and_keeps_unknown_classification_explicit()
    {
        var workspaceId = new WorkspaceId("workspace-a");
        var operationId = new OperationId("operation-1");
        var correlationId = new CorrelationId("correlation-1");
        var classification = new WorkspaceResourceClassification(
            WorkspaceResourceType.Unknown,
            WorkspaceResourceBoundary.Unknown);
        var request = new WorkspaceOperationRequest(
            workspaceId,
            WorkspaceOperation.Read,
            new WorkspaceResourceId("src/file.txt"),
            classification,
            operationId,
            correlationId);

        Assert.Same(workspaceId, request.WorkspaceId);
        Assert.Equal(WorkspaceOperation.Read, request.Operation);
        Assert.Equal(new WorkspaceResourceId("src/file.txt"), request.ResourceId);
        Assert.Same(classification, request.Classification);
        Assert.Same(operationId, request.OperationId);
        Assert.Same(correlationId, request.CorrelationId);
        Assert.Equal(WorkspaceResourceBoundary.Unknown, request.Classification.Boundary);
    }

    [Fact]
    public void Operation_request_rejects_missing_values_and_undefined_operations()
    {
        var request = CreateRequestArguments();

        Assert.Throws<ArgumentNullException>(() => new WorkspaceOperationRequest(
            null!, request.Operation, request.ResourceId, request.Classification, request.OperationId, request.CorrelationId));
        Assert.Throws<ArgumentNullException>(() => new WorkspaceOperationRequest(
            request.WorkspaceId, request.Operation, null!, request.Classification, request.OperationId, request.CorrelationId));
        Assert.Throws<ArgumentNullException>(() => new WorkspaceOperationRequest(
            request.WorkspaceId, request.Operation, request.ResourceId, null!, request.OperationId, request.CorrelationId));
        Assert.Throws<ArgumentNullException>(() => new WorkspaceOperationRequest(
            request.WorkspaceId, request.Operation, request.ResourceId, request.Classification, null!, request.CorrelationId));
        Assert.Throws<ArgumentNullException>(() => new WorkspaceOperationRequest(
            request.WorkspaceId, request.Operation, request.ResourceId, request.Classification, request.OperationId, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceOperationRequest(
            request.WorkspaceId,
            (WorkspaceOperation)int.MaxValue,
            request.ResourceId,
            request.Classification,
            request.OperationId,
            request.CorrelationId));
    }

    private static (
        WorkspaceId Id,
        WorkspaceRootId RootId,
        WorkspaceResourceId[] PermittedRoots,
        WorkspaceResourceId[] ProtectedPaths,
        WorkspaceResourceType[] ProtectedResourceTypes,
        WorkspaceEnvironmentProfileId EnvironmentProfileId,
        TaskScope TaskScope) CreateDescriptorArguments() =>
        (
            new WorkspaceId("workspace-a"),
            new WorkspaceRootId("repository"),
            [new WorkspaceResourceId("src")],
            [],
            [],
            new WorkspaceEnvironmentProfileId("dev"),
            new TaskScope(["src"]));

    private static (
        WorkspaceId WorkspaceId,
        WorkspaceOperation Operation,
        WorkspaceResourceId ResourceId,
        WorkspaceResourceClassification Classification,
        OperationId OperationId,
        CorrelationId CorrelationId) CreateRequestArguments() =>
        (
            new WorkspaceId("workspace-a"),
            WorkspaceOperation.Read,
            new WorkspaceResourceId("src/file.txt"),
            new WorkspaceResourceClassification(
                WorkspaceResourceType.Source,
                WorkspaceResourceBoundary.WithinPermittedWorkspace),
            new OperationId("operation-1"),
            new CorrelationId("correlation-1"));
}
