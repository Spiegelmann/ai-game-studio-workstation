namespace AiGameStudioWorkstation.Core.Domain.Workspace;

public sealed record WorkspaceRootId
{
    public WorkspaceRootId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A workspace root identifier must not be empty or whitespace.", nameof(value));
        }

        if (value.Any(char.IsControl))
        {
            throw new ArgumentException("A workspace root identifier must not contain control characters.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
