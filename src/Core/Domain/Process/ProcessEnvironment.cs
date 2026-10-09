using System.Collections.ObjectModel;

namespace AiGameStudioWorkstation.Core.Domain.Process;

public sealed class ProcessEnvironment : IEquatable<ProcessEnvironment>
{
    private readonly ReadOnlyCollection<ProcessEnvironmentVariable> _variables;

    public ProcessEnvironment(IEnumerable<ProcessEnvironmentVariable> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);

        var copy = variables.ToArray();
        if (copy.Any(variable => variable is null))
        {
            throw new ArgumentException("Environment variables must not contain null entries.", nameof(variables));
        }

        var duplicateName = copy
            .GroupBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateName is not null)
        {
            throw new ArgumentException(
                $"The environment contains duplicate variable name '{duplicateName.Key}'.",
                nameof(variables));
        }

        _variables = Array.AsReadOnly(copy
            .OrderBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(variable => variable.Name, StringComparer.Ordinal)
            .ToArray());
    }

    public static ProcessEnvironment Empty { get; } = new([]);

    public IReadOnlyList<ProcessEnvironmentVariable> Variables => _variables;

    public bool Equals(ProcessEnvironment? other) =>
        other is not null && _variables.SequenceEqual(other._variables);

    public override bool Equals(object? obj) => obj is ProcessEnvironment other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var variable in _variables)
        {
            hash.Add(variable);
        }

        return hash.ToHashCode();
    }
}
