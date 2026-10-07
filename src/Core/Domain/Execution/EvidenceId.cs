namespace AiGameStudioWorkstation.Core.Domain.Execution;

public sealed record EvidenceId
{
    public EvidenceId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("An evidence identifier must not be empty or whitespace.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
