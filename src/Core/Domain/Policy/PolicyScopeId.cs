namespace AiGameStudioWorkstation.Core.Domain.Policy;

public sealed record PolicyScopeId
{
    public PolicyScopeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A policy scope identifier must not be empty or whitespace.", nameof(value));
        }

        if (value.Any(char.IsControl))
        {
            throw new ArgumentException("A policy scope identifier must not contain control characters.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }
}
