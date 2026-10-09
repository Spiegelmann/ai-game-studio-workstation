using AiGameStudioWorkstation.Core.Domain.Process;
using AiGameStudioWorkstation.Core.Domain.Workspace;

namespace AiGameStudioWorkstation.Core.Tests;

public sealed class ProcessExecutionRequestTests
{
    [Fact]
    public void Request_preserves_executable_identity_argument_boundaries_and_working_directory()
    {
        var arguments = new List<string> { "--define", "A=one two", string.Empty };
        var workingDirectory = new ProcessWorkingDirectory(
            new WorkspaceId("workspace-a"),
            new WorkspaceResourceId("src/project"));
        var environment = new ProcessEnvironment([new ProcessEnvironmentVariable("LANG", "en_US.UTF-8")]);

        var request = new ProcessExecutionRequest(
            new ExecutionDefinitionId("dotnet-test"),
            arguments,
            workingDirectory,
            environment,
            TimeSpan.FromMinutes(5),
            new ProcessResourceLimits(memoryLimitBytes: 1024, cpuTimeLimit: TimeSpan.FromMinutes(4)));

        arguments[1] = "rewritten";

        Assert.Equal("dotnet-test", request.ExecutionDefinitionId.Value);
        Assert.Equal(["--define", "A=one two", string.Empty], request.Arguments);
        Assert.Same(workingDirectory, request.WorkingDirectory);
        Assert.Same(environment, request.Environment);
        Assert.Equal(TimeSpan.FromMinutes(5), request.Timeout);
        Assert.Equal(1024, request.ResourceLimits.MemoryLimitBytes);
        Assert.Equal(TimeSpan.FromMinutes(4), request.ResourceLimits.CpuTimeLimit);
    }

    [Fact]
    public void Request_argument_collection_is_read_only()
    {
        var request = CreateRequest(["--no-restore"]);

        Assert.Throws<NotSupportedException>(
            () => ((IList<string>)request.Arguments)[0] = "--shell");
    }

    [Fact]
    public void Requests_have_structural_equality_and_hash_codes()
    {
        var first = CreateRequest(["--filter", "Name=One"]);
        var second = CreateRequest(["--filter", "Name=One"]);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Execution_definition_identity_rejects_missing_or_control_values()
    {
        Assert.Throws<ArgumentException>(() => new ExecutionDefinitionId(" "));
        Assert.Throws<ArgumentException>(() => new ExecutionDefinitionId("dotnet\nrun"));
        Assert.Equal("dotnet-test", new ExecutionDefinitionId(" dotnet-test ").Value);
    }

    [Fact]
    public void Request_rejects_missing_required_metadata_and_invalid_timeout()
    {
        var valid = CreateRequest([]);

        Assert.Throws<ArgumentNullException>(() => new ProcessExecutionRequest(
            null!,
            [],
            valid.WorkingDirectory,
            valid.Environment,
            valid.Timeout,
            valid.ResourceLimits));
        Assert.Throws<ArgumentNullException>(() => new ProcessExecutionRequest(
            valid.ExecutionDefinitionId,
            [],
            null!,
            valid.Environment,
            valid.Timeout,
            valid.ResourceLimits));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessExecutionRequest(
            valid.ExecutionDefinitionId,
            [],
            valid.WorkingDirectory,
            valid.Environment,
            TimeSpan.Zero,
            valid.ResourceLimits));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessExecutionRequest(
            valid.ExecutionDefinitionId,
            [],
            valid.WorkingDirectory,
            valid.Environment,
            TimeSpan.FromSeconds(-1),
            valid.ResourceLimits));
    }

    [Fact]
    public void Request_rejects_null_argument_entries_and_null_characters()
    {
        var valid = CreateRequest([]);

        Assert.Throws<ArgumentException>(() => new ProcessExecutionRequest(
            valid.ExecutionDefinitionId,
            [null!],
            valid.WorkingDirectory,
            valid.Environment,
            valid.Timeout,
            valid.ResourceLimits));
        Assert.Throws<ArgumentException>(() => new ProcessExecutionRequest(
            valid.ExecutionDefinitionId,
            ["invalid\0argument"],
            valid.WorkingDirectory,
            valid.Environment,
            valid.Timeout,
            valid.ResourceLimits));
    }

    [Fact]
    public void Working_directory_requires_workspace_identity_and_relative_resource_identifier()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ProcessWorkingDirectory(null!, new WorkspaceResourceId("src")));
        Assert.Throws<ArgumentNullException>(() =>
            new ProcessWorkingDirectory(new WorkspaceId("workspace-a"), null!));
        Assert.Throws<ArgumentException>(() => new WorkspaceResourceId("../outside"));

        var directory = new ProcessWorkingDirectory(
            new WorkspaceId("workspace-a"),
            new WorkspaceResourceId("src"));

        Assert.Equal("workspace-a", directory.WorkspaceId.Value);
        Assert.Equal("src", directory.ResourceId.Value);
    }

    [Fact]
    public void Environment_is_explicit_order_independent_and_defensively_copied()
    {
        var variables = new List<ProcessEnvironmentVariable>
        {
            new("PATH", "trusted-path"),
            new("LANG", "en_US.UTF-8")
        };
        var environment = new ProcessEnvironment(variables);
        var equivalent = new ProcessEnvironment(
        [
            new ProcessEnvironmentVariable("LANG", "en_US.UTF-8"),
            new ProcessEnvironmentVariable("PATH", "trusted-path")
        ]);

        variables.Clear();

        Assert.Equal(equivalent, environment);
        Assert.Equal(["LANG", "PATH"], environment.Variables.Select(variable => variable.Name));
        Assert.Throws<NotSupportedException>(
            () => ((IList<ProcessEnvironmentVariable>)environment.Variables).Clear());
    }

    [Fact]
    public void Environment_rejects_duplicate_names_ignoring_case_and_invalid_entries()
    {
        Assert.Throws<ArgumentException>(() => new ProcessEnvironment(
        [
            new ProcessEnvironmentVariable("PATH", "a"),
            new ProcessEnvironmentVariable("path", "b")
        ]));
        Assert.Throws<ArgumentException>(() => new ProcessEnvironment([null!]));
        Assert.Throws<ArgumentException>(() => new ProcessEnvironmentVariable("BAD=NAME", "value"));
        Assert.Throws<ArgumentException>(() => new ProcessEnvironmentVariable("1INVALID", "value"));
        Assert.Throws<ArgumentException>(() => new ProcessEnvironmentVariable("VALID", string.Empty));
        Assert.Throws<ArgumentException>(() => new ProcessEnvironmentVariable("VALID", "invalid\0value"));
    }

    [Fact]
    public void Resource_limits_validate_positive_values_and_use_explicit_units()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessResourceLimits(memoryLimitBytes: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessResourceLimits(memoryLimitBytes: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProcessResourceLimits(cpuTimeLimit: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProcessResourceLimits(cpuTimeLimit: TimeSpan.FromTicks(-1)));

        var limits = new ProcessResourceLimits(memoryLimitBytes: 4096, cpuTimeLimit: TimeSpan.FromSeconds(2));

        Assert.Equal(4096, limits.MemoryLimitBytes);
        Assert.Equal(TimeSpan.FromSeconds(2), limits.CpuTimeLimit);
        Assert.Null(new ProcessResourceLimits().MemoryLimitBytes);
        Assert.Null(new ProcessResourceLimits().CpuTimeLimit);
    }

    private static ProcessExecutionRequest CreateRequest(IEnumerable<string> arguments) =>
        new(
            new ExecutionDefinitionId("dotnet-test"),
            arguments,
            new ProcessWorkingDirectory(
                new WorkspaceId("workspace-a"),
                new WorkspaceResourceId("src/project")),
            ProcessEnvironment.Empty,
            TimeSpan.FromMinutes(5),
            new ProcessResourceLimits());
}
