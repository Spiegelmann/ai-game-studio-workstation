using AiGameStudioWorkstation.Core.Domain.Execution;
using AiGameStudioWorkstation.Core.Domain.Tasks;
using DomainTask = AiGameStudioWorkstation.Core.Domain.Tasks.Task;

namespace AiGameStudioWorkstation.Application.Tasks.Persistence;

public interface ITaskRepository
{
    System.Threading.Tasks.Task AddAsync(DomainTask task, CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task<DomainTask?> GetByIdAsync(
        TaskId taskId,
        CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task<TaskOperationResult> ApplyTransitionAsync(
        TaskTransitionOperation operation,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task<TaskOperationResult> ApplyTransitionWithHistoryAsync(
        TaskTransitionOperation operation,
        StateTransition transition,
        TaskAttempt? attempt = null,
        CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task AppendAttemptAsync(
        TaskAttempt attempt,
        CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task<IReadOnlyList<TaskAttempt>> GetAttemptsAsync(
        TaskId taskId,
        CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task<IReadOnlyList<TaskStateTransitionRecord>> GetStateTransitionsAsync(
        TaskId taskId,
        CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task AddEvidenceAsync(
        EvidenceRecord evidence,
        CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task<IReadOnlyList<EvidenceRecord>> GetEvidenceAsync(
        TaskId taskId,
        CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task<IReadOnlyList<EvidenceRecord>> GetEvidenceForAttemptAsync(
        TaskId taskId,
        AttemptId attemptId,
        CancellationToken cancellationToken = default);
}
