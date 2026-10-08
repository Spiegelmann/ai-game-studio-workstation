using AiGameStudioWorkstation.Core.Domain.Execution;
using AiGameStudioWorkstation.Core.Domain.Tasks;

namespace AiGameStudioWorkstation.Application.Tasks.Persistence;

public sealed record TaskStateTransitionRecord(
    StateTransition Transition,
    OperationId OperationId,
    TaskRevision ExpectedRevision,
    TaskRevision ResultingRevision,
    AttemptId? AssociatedAttemptId,
    int? AssociatedAttemptSnapshotNumber);
