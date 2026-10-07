namespace AiGameStudioWorkstation.Core.Domain.Execution;

public sealed record AttemptId
{
    public AttemptId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("An attempt identifier must not be empty or whitespace.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
