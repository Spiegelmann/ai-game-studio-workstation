using AiGameStudioWorkstation.Core.Domain.Tasks;

namespace AiGameStudioWorkstation.Core.Domain.Execution;

public sealed record EvidenceRecord
{
    public EvidenceRecord(
        EvidenceId id,
        TaskId taskId,
        EvidenceType type,
        EvidenceSource source,
        string summary,
        DateTimeOffset timestamp,
        AttemptId? attemptId = null,
        EvidenceReference? reference = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(taskId);

        if (!Enum.IsDefined(typeof(EvidenceType), type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "The evidence type is not defined.");
        }

        if (!Enum.IsDefined(typeof(EvidenceSource), source))
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "The evidence source is not defined.");
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("An evidence summary must not be empty or whitespace.", nameof(summary));
        }

        if (timestamp == default)
        {
            throw new ArgumentException("An evidence timestamp must be specified.", nameof(timestamp));
        }

        Id = id;
        TaskId = taskId;
        AttemptId = attemptId;
        Type = type;
        Source = source;
        Summary = summary;
        Timestamp = timestamp;
        Reference = reference;
    }

    public EvidenceId Id { get; }

    public TaskId TaskId { get; }

    public AttemptId? AttemptId { get; }

    public EvidenceType Type { get; }

    public EvidenceSource Source { get; }

    public string Summary { get; }

    public DateTimeOffset Timestamp { get; }

    public EvidenceReference? Reference { get; }
}
