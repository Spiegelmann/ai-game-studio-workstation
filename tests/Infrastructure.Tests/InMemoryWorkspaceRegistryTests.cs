using AiGameStudioWorkstation.Application.Workspace;
using AiGameStudioWorkstation.Core.Domain.Tasks;
using AiGameStudioWorkstation.Core.Domain.Workspace;
using AiGameStudioWorkstation.Infrastructure.Workspace;

namespace AiGameStudioWorkstation.Infrastructure.Tests;

public sealed class InMemoryWorkspaceRegistryTests
{
    [Fact]
    public void Registers_and_retrieves_host_configured_workspace_without_changing_its_configuration()
    {
        var registry = new InMemoryWorkspaceRegistry();
        var registration = CreateRegistration(
            "workspace-a",
            Path.GetFullPath("configured-root"),
            WorkspaceRegistrationOrigin.HostConfiguration);

        var result = registry.Register(registration);
        var lookup = registry.Get(new WorkspaceId("workspace-a"));

        Assert.Equal(WorkspaceRegistrationStatus.Registered, result.Status);
        Assert.Equal(WorkspaceLookupStatus.Found, lookup.Status);
        Assert.Same(registration, lookup.Registration);
        Assert.Equal(registration.Descriptor, lookup.Registration!.Descriptor);
        Assert.Equal(registration.ConfiguredPhysicalRoot, lookup.Registration.ConfiguredPhysicalRoot);
    }

    [Fact]
    public void Duplicate_identifier_does_not_replace_the_existing_configuration()
    {
        var registry = new InMemoryWorkspaceRegistry();
        var original = CreateRegistration(
            "workspace-a",
            Path.GetFullPath("original-root"),
            WorkspaceRegistrationOrigin.HostConfiguration);
        var replacement = CreateRegistration(
            "workspace-a",
            Path.GetFullPath("replacement-root"),
            WorkspaceRegistrationOrigin.HostConfiguration);

        Assert.Equal(WorkspaceRegistrationStatus.Registered, registry.Register(original).Status);
        Assert.Equal(WorkspaceRegistrationStatus.Duplicate, registry.Register(replacement).Status);

        var lookup = registry.Get(original.Descriptor.Id);
        Assert.Same(original, lookup.Registration);
        Assert.Equal(original.ConfiguredPhysicalRoot, lookup.Registration!.ConfiguredPhysicalRoot);
    }

    [Theory]
    [InlineData(WorkspaceRegistrationOrigin.AgentRequest)]
    [InlineData(WorkspaceRegistrationOrigin.Unknown)]
    public void Rejects_untrusted_registration_origins_without_registering_workspace(
        WorkspaceRegistrationOrigin origin)
    {
        var registry = new InMemoryWorkspaceRegistry();
        var registration = CreateRegistration("workspace-a", Path.GetTempPath(), origin);

        var result = registry.Register(registration);
        var lookup = registry.Get(registration.Descriptor.Id);

        Assert.Equal(WorkspaceRegistrationStatus.RejectedUntrustedOrigin, result.Status);
        Assert.Equal(WorkspaceLookupStatus.NotFound, lookup.Status);
        Assert.Null(lookup.Registration);
    }

    [Fact]
    public void Missing_workspace_is_reported_explicitly()
    {
        var lookup = new InMemoryWorkspaceRegistry().Get(new WorkspaceId("missing"));

        Assert.Equal(WorkspaceLookupStatus.NotFound, lookup.Status);
        Assert.Null(lookup.Registration);
    }

    [Fact]
    public void Registration_rejects_incomplete_configuration_and_undefined_origins()
    {
        var descriptor = CreateDescriptor("workspace-a");

        Assert.Throws<ArgumentNullException>(() =>
            new WorkspaceRegistration(null!, Path.GetTempPath(), WorkspaceRegistrationOrigin.HostConfiguration));
        Assert.Throws<ArgumentException>(() =>
            new WorkspaceRegistration(descriptor, " ", WorkspaceRegistrationOrigin.HostConfiguration));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorkspaceRegistration(descriptor, Path.GetTempPath(), (WorkspaceRegistrationOrigin)int.MaxValue));
    }

    [Fact]
    public void Registered_configuration_flows_to_path_resolution_without_bypassing_containment()
    {
        using var temporaryRoot = new TemporaryRoot();
        Directory.CreateDirectory(Path.Combine(temporaryRoot.Path, "src"));
        File.WriteAllText(Path.Combine(temporaryRoot.Path, "src", "module.lua"), "return true");

        var registry = new InMemoryWorkspaceRegistry();
        var registration = CreateRegistration(
            "workspace-a",
            temporaryRoot.Path,
            WorkspaceRegistrationOrigin.HostConfiguration);
        Assert.Equal(WorkspaceRegistrationStatus.Registered, registry.Register(registration).Status);

        var registered = registry.Get(registration.Descriptor.Id).Registration!;
        var operation = CreateOperationRequest(registration.Descriptor.Id, "src/module.lua");
        var resolver = new PhysicalWorkspacePathResolver();
        var inScope = resolver.Resolve(new WorkspacePathResolutionRequest(
            registered.Descriptor,
            registered.ConfiguredPhysicalRoot,
            operation));

        var outsideOperation = CreateOperationRequest(registration.Descriptor.Id, "outside/secret.txt");
        var outside = resolver.Resolve(new WorkspacePathResolutionRequest(
            registered.Descriptor,
            registered.ConfiguredPhysicalRoot,
            outsideOperation));

        Assert.Equal(WorkspacePathResolutionStatus.ResolvedWithinPermittedRoot, inScope.Status);
        Assert.Equal(WorkspacePathResolutionStatus.OutsidePermittedRoot, outside.Status);
        Assert.Equal(WorkspaceResourceBoundary.OutsidePermittedWorkspace, outside.Containment);
    }

    [Fact]
    public async System.Threading.Tasks.Task Competing_registrations_have_one_atomic_winner_and_never_replace_it()
    {
        var registry = new InMemoryWorkspaceRegistry();
        var registrations = Enumerable.Range(0, 16)
            .Select(index => CreateRegistration(
                "workspace-a",
                Path.GetFullPath($"root-{index}"),
                WorkspaceRegistrationOrigin.HostConfiguration))
            .ToArray();

        var results = await System.Threading.Tasks.Task.WhenAll(registrations.Select(registration =>
            System.Threading.Tasks.Task.Run(() => registry.Register(registration))));

        Assert.Single(results, result => result.Status == WorkspaceRegistrationStatus.Registered);
        Assert.Equal(15, results.Count(result => result.Status == WorkspaceRegistrationStatus.Duplicate));

        var winner = registry.Get(new WorkspaceId("workspace-a")).Registration;
        Assert.NotNull(winner);
        Assert.Contains(registrations, registration => ReferenceEquals(registration, winner));
    }

    private static WorkspaceRegistration CreateRegistration(
        string id,
        string physicalRoot,
        WorkspaceRegistrationOrigin origin) =>
        new(CreateDescriptor(id), physicalRoot, origin);

    private static WorkspaceDescriptor CreateDescriptor(string id) =>
        new(
            new WorkspaceId(id),
            new WorkspaceRootId("logical-root"),
            [new WorkspaceResourceId("src")],
            [],
            [],
            new WorkspaceEnvironmentProfileId("test"),
            new TaskScope(["src"]));

    private static WorkspaceOperationRequest CreateOperationRequest(WorkspaceId id, string resource) =>
        new(
            id,
            WorkspaceOperation.Read,
            new WorkspaceResourceId(resource),
            new WorkspaceResourceClassification(
                WorkspaceResourceType.Unknown,
                WorkspaceResourceBoundary.Unknown),
            new OperationId("operation-test"),
            new CorrelationId("correlation-test"));

    private sealed class TemporaryRoot : IDisposable
    {
        public TemporaryRoot()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"ai-game-studio-workstation-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
