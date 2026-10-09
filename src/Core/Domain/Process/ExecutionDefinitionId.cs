namespace AiGameStudioWorkstation.Core.Domain.Process;

public sealed record ExecutionDefinitionId
{
    public ExecutionDefinitionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "An execution definition identifier must not be empty or whitespace.",
                nameof(value));
        }

        if (value.Any(char.IsControl))
        {
            throw new ArgumentException(
                "An execution definition identifier must not contain control characters.",
                nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
