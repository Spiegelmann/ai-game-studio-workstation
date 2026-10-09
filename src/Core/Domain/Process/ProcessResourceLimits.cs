namespace AiGameStudioWorkstation.Core.Domain.Process;

public sealed record ProcessResourceLimits
{
    public ProcessResourceLimits(long? memoryLimitBytes = null, TimeSpan? cpuTimeLimit = null)
    {
        if (memoryLimitBytes is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(memoryLimitBytes),
                memoryLimitBytes,
                "A memory limit, when specified, must be greater than zero bytes.");
        }

        if (cpuTimeLimit is { } value && value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cpuTimeLimit),
                cpuTimeLimit,
                "A CPU-time limit, when specified, must be positive.");
        }

        MemoryLimitBytes = memoryLimitBytes;
        CpuTimeLimit = cpuTimeLimit;
    }

    public long? MemoryLimitBytes { get; }

    public TimeSpan? CpuTimeLimit { get; }
}
