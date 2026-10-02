namespace AiGameStudioWorkstation.Core.Domain.Tasks;

public sealed record TaskId
{
    public TaskId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A task identifier must not be empty or whitespace.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
