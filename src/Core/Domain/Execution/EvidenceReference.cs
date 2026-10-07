namespace AiGameStudioWorkstation.Core.Domain.Execution;

public sealed record EvidenceReference
{
    public EvidenceReference(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("An evidence reference must not be empty or whitespace.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }
}
