using AiGameStudioWorkstation.Application.Workspace;
using AiGameStudioWorkstation.Core.Domain.Tasks;
using AiGameStudioWorkstation.Core.Domain.Workspace;
using AiGameStudioWorkstation.Infrastructure.Workspace;
using Xunit.Abstractions;

namespace AiGameStudioWorkstation.Infrastructure.Tests;

public sealed class PhysicalWorkspacePathResolverTests
{
    private readonly ITestOutputHelper _output;

    public PhysicalWorkspacePathResolverTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Read_resolves_existing_file_within_permitted_root()
    {
        using var workspace = new TemporaryWorkspace();
        var filePath = workspace.CreateFile("src", "module.lua");

        var result = workspace.Resolve("src/module.lua", WorkspaceOperation.Read);

        Assert.Equal(WorkspacePathResolutionStatus.ResolvedWithinPermittedRoot, result.Status);
        Assert.Equal(WorkspaceResourceBoundary.WithinPermittedWorkspace, result.Containment);
        Assert.Equal(workspace.Descriptor.RootId, result.LogicalRootId);
        Assert.Equal(Path.GetFullPath(filePath), result.NormalizedCandidatePath);
        Assert.Equal(Path.GetFullPath(filePath), result.VerifiedTargetPath);
        Assert.Equal(Path.GetFullPath(workspace.PathFor("src")), result.VerifiedAncestorPath);
        Assert.Equal(WorkspacePathTargetKind.ExistingFile, result.TargetKind);
        Assert.Null(result.Failure);
    }

    [Fact]
    public void List_and_create_directory_validate_directory_targets_without_mutation()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.CreateDirectory("src", "existing");

        var permittedRootResult = workspace.Resolve("src", WorkspaceOperation.List);
        var existingResult = workspace.Resolve("src/existing", WorkspaceOperation.List);
        var prospectiveResult = workspace.Resolve(
            "src/new/nested",
            WorkspaceOperation.CreateDirectory);

        Assert.Equal(WorkspacePathResolutionStatus.ResolvedWithinPermittedRoot, permittedRootResult.Status);
        Assert.Equal(WorkspacePathTargetKind.ExistingDirectory, permittedRootResult.TargetKind);
        Assert.Equal(WorkspacePathResolutionStatus.ResolvedWithinPermittedRoot, existingResult.Status);
        Assert.Equal(WorkspacePathTargetKind.ExistingDirectory, existingResult.TargetKind);
        Assert.Equal(WorkspacePathResolutionStatus.ResolvedWithinPermittedRoot, prospectiveResult.Status);
        Assert.Equal(WorkspacePathTargetKind.ProspectiveDirectory, prospectiveResult.TargetKind);
        Assert.Null(prospectiveResult.VerifiedTargetPath);
        Assert.Equal(
            Path.GetFullPath(workspace.PathFor("src")),
            prospectiveResult.VerifiedAncestorPath);
        Assert.False(Directory.Exists(workspace.PathFor("src/new")));
    }

    [Fact]
    public void Write_resolves_existing_file_and_prospective_file_without_mutation()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.CreateFile("src", "existing.txt");

        var existingResult = workspace.Resolve("src/existing.txt", WorkspaceOperation.Write);
        var prospectiveResult = workspace.Resolve("src/new.txt", WorkspaceOperation.Write);

        Assert.Equal(WorkspacePathResolutionStatus.ResolvedWithinPermittedRoot, existingResult.Status);
        Assert.Equal(WorkspacePathTargetKind.ExistingFile, existingResult.TargetKind);
        Assert.Equal(WorkspacePathResolutionStatus.ResolvedWithinPermittedRoot, prospectiveResult.Status);
        Assert.Equal(WorkspacePathTargetKind.ProspectiveFile, prospectiveResult.TargetKind);
        Assert.Equal(
            Path.GetFullPath(workspace.PathFor("src")),
            prospectiveResult.VerifiedAncestorPath);
        Assert.False(File.Exists(workspace.PathFor("src/new.txt")));
    }

    [Fact]
    public void Sibling_prefix_is_outside_the_configured_permitted_root()
    {
        using var workspace = new TemporaryWorkspace();

        var result = workspace.Resolve("src-sibling/secret.txt", WorkspaceOperation.Read);

        Assert.Equal(WorkspacePathResolutionStatus.OutsidePermittedRoot, result.Status);
        Assert.Equal(WorkspaceResourceBoundary.OutsidePermittedWorkspace, result.Containment);
        Assert.Equal(WorkspacePathResolutionFailure.ResourceOutsidePermittedRoots, result.Failure);
        Assert.Null(result.VerifiedTargetPath);
    }

    [Fact]
    public void Permitted_root_matching_is_case_exact_and_platform_independent()
    {
        using var workspace = new TemporaryWorkspace();

        var result = workspace.Resolve("SRC/file.txt", WorkspaceOperation.Read);

        Assert.Equal(WorkspacePathResolutionStatus.OutsidePermittedRoot, result.Status);
        Assert.Equal(WorkspaceResourceBoundary.OutsidePermittedWorkspace, result.Containment);
    }

    [Fact]
    public void Missing_target_and_missing_write_ancestor_are_not_reported_as_contained()
    {
        using var workspace = new TemporaryWorkspace();

        var readResult = workspace.Resolve("src/missing.txt", WorkspaceOperation.Read);
        var writeResult = workspace.Resolve("src/missing/target.txt", WorkspaceOperation.Write);

        Assert.Equal(WorkspacePathResolutionStatus.NotFound, readResult.Status);
        Assert.Equal(WorkspacePathResolutionFailure.MissingTarget, readResult.Failure);
        Assert.Equal(WorkspaceResourceBoundary.Unknown, readResult.Containment);
        Assert.Equal(WorkspacePathResolutionStatus.NotFound, writeResult.Status);
        Assert.Equal(WorkspacePathResolutionFailure.MissingAncestor, writeResult.Failure);
        Assert.Equal(WorkspaceResourceBoundary.Unknown, writeResult.Containment);
    }

    [Fact]
    public void Wrong_target_kind_and_non_directory_ancestor_are_rejected()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.CreateFile("src", "file");

        var listResult = workspace.Resolve("src/file", WorkspaceOperation.List);
        var descendantResult = workspace.Resolve("src/file/child", WorkspaceOperation.Read);

        Assert.Equal(WorkspacePathResolutionStatus.Rejected, listResult.Status);
        Assert.Equal(WorkspacePathResolutionFailure.WrongTargetKind, listResult.Failure);
        Assert.Equal(WorkspacePathResolutionStatus.Rejected, descendantResult.Status);
        Assert.Equal(WorkspacePathResolutionFailure.NonDirectoryAncestor, descendantResult.Failure);
        Assert.Equal(WorkspaceResourceBoundary.Unknown, descendantResult.Containment);
    }

    [Fact]
    public void Invalid_or_missing_physical_roots_fail_closed()
    {
        using var workspace = new TemporaryWorkspace();

        var relativeRootRequest = workspace.CreateRequest(
            "src/file.txt",
            WorkspaceOperation.Read,
            "relative-root");
        var relativeRootResult = new PhysicalWorkspacePathResolver().Resolve(relativeRootRequest);
        var missingRootRequest = workspace.CreateRequest(
            "src/file.txt",
            WorkspaceOperation.Read,
            workspace.PathFor("not-created"));
        var missingRootResult = new PhysicalWorkspacePathResolver().Resolve(missingRootRequest);

        Assert.Equal(WorkspacePathResolutionStatus.Failed, relativeRootResult.Status);
        Assert.Equal(WorkspacePathResolutionFailure.InvalidPhysicalRoot, relativeRootResult.Failure);
        Assert.Equal(WorkspaceResourceBoundary.Unknown, relativeRootResult.Containment);
        Assert.Equal(WorkspacePathResolutionStatus.Failed, missingRootResult.Status);
        Assert.Equal(WorkspacePathResolutionFailure.InvalidPhysicalRoot, missingRootResult.Failure);
    }

    [Fact]
    public void Permission_errors_are_indeterminate_or_explicitly_unavailable()
    {
        using var workspace = new TemporaryWorkspace();
        if (!OperatingSystem.IsLinux())
        {
            _output.WriteLine("UNSUPPORTED: portable permission-denial fixture is only available on Linux.");
            return;
        }

        var protectedDirectory = workspace.CreateDirectory("src", "no-access");
        var originalMode = File.GetUnixFileMode(protectedDirectory);
        try
        {
            File.SetUnixFileMode(protectedDirectory, UnixFileMode.None);
            var result = workspace.Resolve("src/no-access/child.txt", WorkspaceOperation.Read);
            if (result.Status == WorkspacePathResolutionStatus.Indeterminate)
            {
                Assert.Equal(WorkspacePathResolutionFailure.FilesystemAccessError, result.Failure);
                Assert.Equal(WorkspaceResourceBoundary.Unknown, result.Containment);
                return;
            }

            _output.WriteLine(
                $"UNSUPPORTED: this Linux test process can traverse a mode-000 directory (status: {result.Status}).");
            Assert.Equal(WorkspacePathResolutionStatus.NotFound, result.Status);
        }
        finally
        {
            File.SetUnixFileMode(protectedDirectory, originalMode);
        }
    }

    [Fact]
    public void Symlinks_are_rejected_instead_of_followed()
    {
        using var workspace = new TemporaryWorkspace();
        var outsidePath = workspace.CreateDirectoryOutsideWorkspace("outside");
        var linkPath = workspace.PathFor("src/link");

        try
        {
            Directory.CreateSymbolicLink(linkPath, outsidePath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException or NotSupportedException)
        {
            _output.WriteLine(
                $"UNSUPPORTED: symbolic-link containment scenario unavailable on this platform: {exception.GetType().Name}");
            return;
        }

        var result = workspace.Resolve("src/link", WorkspaceOperation.List);

        Assert.Equal(WorkspacePathResolutionStatus.Rejected, result.Status);
        Assert.Equal(WorkspacePathResolutionFailure.SymbolicLinkOrReparsePoint, result.Failure);
        Assert.Equal(WorkspaceResourceBoundary.Unknown, result.Containment);
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        private readonly string _temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            $"ai-game-studio-tool002-{Guid.NewGuid():N}");

        public TemporaryWorkspace()
        {
            RootPath = Path.Combine(_temporaryRoot, "workspace");
            Directory.CreateDirectory(Path.Combine(RootPath, "src"));
        }

        public string RootPath { get; }

        public WorkspaceDescriptor Descriptor => CreateDescriptor();

        public string PathFor(string relativePath) =>
            Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

        public string CreateFile(string relativeDirectory, string name)
        {
            var directoryPath = PathFor(relativeDirectory);
            Directory.CreateDirectory(directoryPath);
            var filePath = Path.Combine(directoryPath, name);
            File.WriteAllText(filePath, "test");
            return filePath;
        }

        public string CreateDirectory(params string[] segments)
        {
            var path = segments.Aggregate(RootPath, Path.Combine);
            Directory.CreateDirectory(path);
            return path;
        }

        public string CreateDirectoryOutsideWorkspace(string name)
        {
            var path = Path.Combine(_temporaryRoot, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public WorkspacePathResolutionResult Resolve(string resource, WorkspaceOperation operation) =>
            new PhysicalWorkspacePathResolver().Resolve(
                CreateRequest(resource, operation, RootPath));

        public WorkspacePathResolutionRequest CreateRequest(
            string resource,
            WorkspaceOperation operation,
            string physicalRoot)
        {
            var workspaceId = new WorkspaceId("workspace-test");
            var descriptor = CreateDescriptor(workspaceId);
            var operationRequest = new WorkspaceOperationRequest(
                workspaceId,
                operation,
                new WorkspaceResourceId(resource),
                new WorkspaceResourceClassification(
                    WorkspaceResourceType.Unknown,
                    WorkspaceResourceBoundary.Unknown),
                new OperationId("operation-test"),
                new CorrelationId("correlation-test"));

            return new WorkspacePathResolutionRequest(descriptor, physicalRoot, operationRequest);
        }

        public void Dispose()
        {
            if (Directory.Exists(_temporaryRoot))
            {
                Directory.Delete(_temporaryRoot, recursive: true);
            }
        }

        private static WorkspaceDescriptor CreateDescriptor(WorkspaceId? workspaceId = null) =>
            new(
                workspaceId ?? new WorkspaceId("workspace-test"),
                new WorkspaceRootId("logical-root"),
                [new WorkspaceResourceId("src")],
                [],
                [],
                new WorkspaceEnvironmentProfileId("test"),
                new TaskScope(["src"]));
    }
}
