using System.Collections.ObjectModel;

namespace AiGameStudioWorkstation.Core.Domain.Process;

public sealed class ProcessExecutionRequest : IEquatable<ProcessExecutionRequest>
{
    private readonly ReadOnlyCollection<string> _arguments;

    public ProcessExecutionRequest(
        ExecutionDefinitionId executionDefinitionId,
        IEnumerable<string> arguments,
        ProcessWorkingDirectory workingDirectory,
        ProcessEnvironment environment,
        TimeSpan timeout,
        ProcessResourceLimits resourceLimits)
    {
        ArgumentNullException.ThrowIfNull(executionDefinitionId);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(workingDirectory);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(resourceLimits);

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "A process timeout must be positive.");
        }

        var argumentCopy = arguments.ToArray();
        if (argumentCopy.Any(argument => argument is null || argument.Contains('\0')))
        {
            throw new ArgumentException(
                "Process arguments must not contain null entries or null characters.",
                nameof(arguments));
        }

        ExecutionDefinitionId = executionDefinitionId;
        _arguments = Array.AsReadOnly(argumentCopy);
        WorkingDirectory = workingDirectory;
        Environment = environment;
        Timeout = timeout;
        ResourceLimits = resourceLimits;
    }

    public ExecutionDefinitionId ExecutionDefinitionId { get; }

    public IReadOnlyList<string> Arguments => _arguments;

    public ProcessWorkingDirectory WorkingDirectory { get; }

    public ProcessEnvironment Environment { get; }

    public TimeSpan Timeout { get; }

    public ProcessResourceLimits ResourceLimits { get; }

    public bool Equals(ProcessExecutionRequest? other) =>
        other is not null &&
        ExecutionDefinitionId == other.ExecutionDefinitionId &&
        _arguments.SequenceEqual(other._arguments, StringComparer.Ordinal) &&
        WorkingDirectory == other.WorkingDirectory &&
        Environment.Equals(other.Environment) &&
        Timeout == other.Timeout &&
        ResourceLimits == other.ResourceLimits;

    public override bool Equals(object? obj) => obj is ProcessExecutionRequest other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ExecutionDefinitionId);
        foreach (var argument in _arguments)
        {
            hash.Add(argument, StringComparer.Ordinal);
        }

        hash.Add(WorkingDirectory);
        hash.Add(Environment);
        hash.Add(Timeout);
        hash.Add(ResourceLimits);
        return hash.ToHashCode();
    }
}
