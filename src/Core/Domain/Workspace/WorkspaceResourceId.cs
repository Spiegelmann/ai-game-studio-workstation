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

        if (segments.Any(IsAmbiguousWindowsSegment))
        {
            throw new ArgumentException(
                "A workspace resource identifier must not contain Windows-ambiguous path segments.",
                nameof(value));
        }

        Value = value;
        NormalizedValue = value.Replace('\\', '/');
    }

    public string Value { get; }

    public string NormalizedValue { get; }

    public override string ToString() => Value;

    private static bool IsAmbiguousWindowsSegment(string segment)
    {
        if (segment.IndexOfAny(['<', '>', ':', '"', '|', '?', '*']) >= 0 ||
            segment[^1] is '.' or ' ')
        {
            return true;
        }

        var deviceName = segment.Split('.')[0].TrimEnd(' ', '.');
        return deviceName.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            IsNumberedDeviceName(deviceName, "COM") ||
            IsNumberedDeviceName(deviceName, "LPT");
    }

    private static bool IsNumberedDeviceName(string value, string prefix) =>
        value.Length == prefix.Length + 1 &&
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
        value[^1] is >= '1' and <= '9';
}
