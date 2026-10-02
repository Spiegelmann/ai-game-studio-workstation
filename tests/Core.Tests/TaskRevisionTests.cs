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
}
