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
}
