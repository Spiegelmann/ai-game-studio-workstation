namespace AiGameStudioWorkstation.Core.Domain.Tasks;

public sealed record OperationId
{
    public OperationId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("An operation identifier must not be empty or whitespace.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}