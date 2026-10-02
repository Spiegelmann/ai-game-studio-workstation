namespace AiGameStudioWorkstation.Core.Domain.Tasks;

public readonly record struct ChangeBudget
{
    public ChangeBudget(
        int maxProductionFilesChanged,
        int maxTestFilesChanged,
        int maxTotalFilesChanged,
        int maxDependencyAdditions,
        int maxFileDeletions)
    {
        ValidateNonNegative(maxProductionFilesChanged, nameof(maxProductionFilesChanged));
        ValidateNonNegative(maxTestFilesChanged, nameof(maxTestFilesChanged));
        ValidateNonNegative(maxTotalFilesChanged, nameof(maxTotalFilesChanged));
        ValidateNonNegative(maxDependencyAdditions, nameof(maxDependencyAdditions));
        ValidateNonNegative(maxFileDeletions, nameof(maxFileDeletions));

        MaxProductionFilesChanged = maxProductionFilesChanged;
        MaxTestFilesChanged = maxTestFilesChanged;
        MaxTotalFilesChanged = maxTotalFilesChanged;
        MaxDependencyAdditions = maxDependencyAdditions;
        MaxFileDeletions = maxFileDeletions;
    }

    public int MaxProductionFilesChanged { get; }

    public int MaxTestFilesChanged { get; }

    public int MaxTotalFilesChanged { get; }

    public int MaxDependencyAdditions { get; }

    public int MaxFileDeletions { get; }

    private static void ValidateNonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "A change budget value cannot be negative.");
        }
    }
}
