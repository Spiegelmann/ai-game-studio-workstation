using System.Collections.ObjectModel;
using AiGameStudioWorkstation.Core.Domain.Tasks;

namespace AiGameStudioWorkstation.Core.Domain.Execution;

public sealed record Checkpoint
{
    private readonly ReadOnlyCollection<EvidenceId> _evidenceReferences;

    public Checkpoint(
        CheckpointId id,
        TaskId taskId,
        AttemptId attemptId,
        TaskState taskState,
        string stepIdentifier,
        string repositoryRevision,
        string workspaceStateReference,
        RecoveryContext recoveryContext,
        DateTimeOffset timestamp,
        IEnumerable<EvidenceId> evidenceReferences,
        InvocationId? lastCompletedInvocationId = null,
        InvocationId? activeInvocationId = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(taskId);
        ArgumentNullException.ThrowIfNull(attemptId);
        ArgumentNullException.ThrowIfNull(recoveryContext);

        if (!Enum.IsDefined(typeof(TaskState), taskState))
        {
            throw new ArgumentOutOfRangeException(nameof(taskState), taskState, "The task state is not defined.");
        }

        ValidateText(stepIdentifier, nameof(stepIdentifier), "A step identifier");
        ValidateText(repositoryRevision, nameof(repositoryRevision), "A repository revision reference");
        ValidateText(workspaceStateReference, nameof(workspaceStateReference), "A workspace state reference");

        if (timestamp == default)
        {
            throw new ArgumentException("A checkpoint timestamp must be specified.", nameof(timestamp));
        }

        ArgumentNullException.ThrowIfNull(evidenceReferences);
        var evidenceCopy = evidenceReferences.ToArray();
        if (evidenceCopy.Any(reference => reference is null))
        {
            throw new ArgumentException("Checkpoint evidence references cannot contain null entries.", nameof(evidenceReferences));
        }

        Id = id;
        TaskId = taskId;
        AttemptId = attemptId;
        TaskState = taskState;
        StepIdentifier = stepIdentifier;
        RepositoryRevision = repositoryRevision;
        WorkspaceStateReference = workspaceStateReference;
        RecoveryContext = recoveryContext;
        Timestamp = timestamp;
        _evidenceReferences = Array.AsReadOnly(evidenceCopy);
        LastCompletedInvocationId = lastCompletedInvocationId;
        ActiveInvocationId = activeInvocationId;
    }

    public CheckpointId Id { get; }

    public TaskId TaskId { get; }

    public AttemptId AttemptId { get; }

    public TaskState TaskState { get; }

    public string StepIdentifier { get; }

    public string RepositoryRevision { get; }

    public string WorkspaceStateReference { get; }

    public InvocationId? LastCompletedInvocationId { get; }

    public InvocationId? ActiveInvocationId { get; }

    public IReadOnlyList<EvidenceId> EvidenceReferences => _evidenceReferences;

    public RecoveryContext RecoveryContext { get; }

    public DateTimeOffset Timestamp { get; }

    private static void ValidateText(string value, string parameterName, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{label} must not be empty or whitespace.", parameterName);
        }
    }
}
