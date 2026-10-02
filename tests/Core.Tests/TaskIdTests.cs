using AiGameStudioWorkstation.Core.Domain.Tasks;

namespace AiGameStudioWorkstation.Core.Tests;

public sealed class TaskIdTests
{
    [Fact]
    public void Accepts_a_valid_identifier_and_preserves_its_representation()
    {
        var id = new TaskId("foundation-002");

        Assert.Equal("foundation-002", id.Value);
        Assert.Equal("foundation-002", id.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Rejects_an_empty_or_whitespace_identifier(string value)
    {
        Assert.Throws<ArgumentException>(() => new TaskId(value));
    }

    [Fact]
    public void Identifiers_have_value_equality()
    {
        Assert.Equal(new TaskId("task-1"), new TaskId("task-1"));
        Assert.NotEqual(new TaskId("task-1"), new TaskId("task-2"));
    }
}
