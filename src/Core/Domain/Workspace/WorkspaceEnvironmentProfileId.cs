namespace AiGameStudioWorkstation.Core.Domain.Workspace;

public sealed record WorkspaceEnvironmentProfileId
{
    public WorkspaceEnvironmentProfileId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("An environment profile identifier must not be empty or whitespace.", nameof(value));
        }

        if (value.Any(char.IsControl))
        {
            throw new ArgumentException("An environment profile identifier must not contain control characters.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
