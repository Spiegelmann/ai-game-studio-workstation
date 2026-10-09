namespace AiGameStudioWorkstation.Core.Domain.Process;

public sealed record ProcessEnvironmentVariable
{
    public ProcessEnvironmentVariable(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);

        if (!IsValidName(name))
        {
            throw new ArgumentException(
                "An environment variable name must start with an ASCII letter or underscore and contain only ASCII letters, digits, or underscores.",
                nameof(name));
        }

        if (value.Length == 0 || value.Contains('\0'))
        {
            throw new ArgumentException(
                "An environment variable value must not be empty or contain a null character.",
                nameof(value));
        }

        Name = name;
        Value = value;
    }

    public string Name { get; }

    public string Value { get; }

    private static bool IsValidName(string value) =>
        (IsAsciiLetter(value[0]) || value[0] == '_') &&
        value.Skip(1).All(character => IsAsciiLetter(character) || character == '_' || character is >= '0' and <= '9');

    private static bool IsAsciiLetter(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
