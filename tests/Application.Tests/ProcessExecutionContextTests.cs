using AiGameStudioWorkstation.Application.Process;
using AiGameStudioWorkstation.Core.Domain.Process;
using AiGameStudioWorkstation.Core.Domain.Workspace;

namespace AiGameStudioWorkstation.Application.Tests;

public sealed class ProcessExecutionContextTests
{
    [Fact]
    public void Context_carries_request_and_cancellation_intent_separately()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var request = new ProcessExecutionRequest(
            new ExecutionDefinitionId("dotnet-test"),
            [],
            new ProcessWorkingDirectory(
                new WorkspaceId("workspace-a"),
                new WorkspaceResourceId("src")),
            ProcessEnvironment.Empty,
            TimeSpan.FromMinutes(1),
            new ProcessResourceLimits());

        var context = new ProcessExecutionContext(request, cancellationSource.Token);

        Assert.Same(request, context.Request);
        Assert.Equal(cancellationSource.Token, context.CancellationToken);
        Assert.True(context.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public void Context_rejects_a_missing_request()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ProcessExecutionContext(null!, CancellationToken.None));
    }
}
