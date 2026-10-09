namespace AiGameStudioWorkstation.Core.Domain.Workspace;

public sealed record WorkspaceResourceId
{
    public WorkspaceResourceId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A workspace resource identifier must not be empty or whitespace.", nameof(value));
        }

        if (value.Any(char.IsControl))
        {
            throw new ArgumentException("A workspace resource identifier must not contain control characters.", nameof(value));
        }

        if (value[0] is '/' or '\\' ||
            (value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':'))
        {
            throw new ArgumentException("A workspace resource identifier must be relative.", nameof(value));
        }

        var segments = value.Split(['/', '\\']);
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            throw new ArgumentException(
                "A workspace resource identifier must not contain empty, current-directory, or parent-directory segments.",
                nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
