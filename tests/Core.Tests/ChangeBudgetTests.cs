using AiGameStudioWorkstation.Core.Domain.Tasks;

namespace AiGameStudioWorkstation.Core.Tests;

public sealed class ChangeBudgetTests
{
    [Fact]
    public void Represents_budget_values()
    {
        var budget = new ChangeBudget(3, 2, 5, 1, 0);

        Assert.Equal(3, budget.MaxProductionFilesChanged);
        Assert.Equal(2, budget.MaxTestFilesChanged);
        Assert.Equal(5, budget.MaxTotalFilesChanged);
        Assert.Equal(1, budget.MaxDependencyAdditions);
        Assert.Equal(0, budget.MaxFileDeletions);
    }

    [Fact]
    public void Allows_zero_values()
    {
        var budget = new ChangeBudget(0, 0, 0, 0, 0);

        Assert.Equal(default, budget);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0, 0)]
    [InlineData(0, -1, 0, 0, 0)]
    [InlineData(0, 0, -1, 0, 0)]
    [InlineData(0, 0, 0, -1, 0)]
    [InlineData(0, 0, 0, 0, -1)]
    public void Rejects_negative_values(
        int maxProductionFilesChanged,
        int maxTestFilesChanged,
        int maxTotalFilesChanged,
        int maxDependencyAdditions,
        int maxFileDeletions)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChangeBudget(
            maxProductionFilesChanged,
            maxTestFilesChanged,
            maxTotalFilesChanged,
            maxDependencyAdditions,
            maxFileDeletions));
    }

    [Fact]
    public void Budgets_have_value_equality()
    {
        Assert.Equal(new ChangeBudget(1, 2, 3, 4, 5), new ChangeBudget(1, 2, 3, 4, 5));
        Assert.NotEqual(new ChangeBudget(1, 2, 3, 4, 5), new ChangeBudget(1, 2, 3, 4, 6));
    }
}
