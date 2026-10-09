using AiGameStudioWorkstation.Core.Domain.Process;

namespace AiGameStudioWorkstation.Application.Process;

public sealed class ProcessExecutionContext
{
    public ProcessExecutionContext(ProcessExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Request = request;
        CancellationToken = cancellationToken;
    }

    public ProcessExecutionRequest Request { get; }

    public CancellationToken CancellationToken { get; }
}
