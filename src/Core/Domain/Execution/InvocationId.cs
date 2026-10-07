namespace AiGameStudioWorkstation.Core.Domain.Execution;

public sealed record InvocationId
{
    public InvocationId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("An invocation identifier must not be empty or whitespace.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
