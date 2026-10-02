using System.Collections.ObjectModel;

namespace AiGameStudioWorkstation.Core.Domain.Tasks;

public sealed class TaskScope : IEquatable<TaskScope>
{
    private readonly ReadOnlyCollection<string> _entries;

    public TaskScope(IEnumerable<string> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var copy = entries.ToArray();
        if (copy.Length == 0)
        {
            throw new ArgumentException("A task scope must contain at least one entry.", nameof(entries));
        }

        if (copy.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Task scope entries must not be empty or whitespace.", nameof(entries));
        }

        _entries = Array.AsReadOnly(copy);
    }

    public IReadOnlyList<string> Entries => _entries;

    public bool Equals(TaskScope? other) =>
        other is not null && _entries.SequenceEqual(other._entries, StringComparer.Ordinal);

    public override bool Equals(object? obj) => obj is TaskScope other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var entry in _entries)
        {
            hash.Add(entry, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
