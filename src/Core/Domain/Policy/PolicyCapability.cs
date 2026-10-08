namespace AiGameStudioWorkstation.Core.Domain.Policy;

public sealed record PolicyCapability
{
    public PolicyCapability(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A policy capability must not be empty or whitespace.", nameof(value));
        }

        Value = value.Trim().ToLowerInvariant();
    }

    public string Value { get; }
}
