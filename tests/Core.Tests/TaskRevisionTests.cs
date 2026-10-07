using AiGameStudioWorkstation.Core.Domain.Tasks;

namespace AiGameStudioWorkstation.Core.Tests;

public sealed class TaskRevisionTests
{
    [Fact]
    public void Represents_the_initial_revision_explicitly()
    {
        Assert.Equal(0, new TaskRevision(0).Value);
    }

    [Fact]
    public void Rejects_a_negative_revision()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TaskRevision(-1));
    }

    [Fact]
    public void Revisions_have_value_equality()
    {
        Assert.Equal(new TaskRevision(2), new TaskRevision(2));
        Assert.NotEqual(new TaskRevision(2), new TaskRevision(3));
    }

    [Fact]
    public void Next_advances_the_revision_by_one()
    {
        Assert.Equal(new TaskRevision(6), new TaskRevision(5).Next());
    }

    [Fact]
    public void Next_rejects_revision_overflow()
    {
        Assert.Throws<OverflowException>(() => new TaskRevision(int.MaxValue).Next());
    }

    [Fact]
    public void Revision_value_cannot_be_mutated_externally()
    {
        Assert.False(typeof(TaskRevision).GetProperty(nameof(TaskRevision.Value))!.CanWrite);
    }
}
