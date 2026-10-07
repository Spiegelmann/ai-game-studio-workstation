using AiGameStudioWorkstation.Core.Domain.Tasks;
using DomainTask = AiGameStudioWorkstation.Core.Domain.Tasks.Task;

namespace AiGameStudioWorkstation.Core.Tests;

public sealed class TaskOperationTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 2, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Operation_ids_are_validated_value_objects()
    {
        var first = new OperationId("operation-1");
        var equal = new OperationId("operation-1");

        Assert.Equal(first, equal);
        Assert.Equal(first.GetHashCode(), equal.GetHashCode());
        Assert.NotEqual(first, new OperationId("operation-2"));
        Assert.Equal("operation-1", first.ToString());
        Assert.False(typeof(OperationId).GetProperty(nameof(OperationId.Value))!.CanWrite);
        Assert.Throws<ArgumentException>(() => new OperationId(" "));
    }

    [Fact]
    public void Correlation_ids_are_validated_value_objects()
    {
        var first = new CorrelationId("correlation-1");
        var equal = new CorrelationId("correlation-1");

        Assert.Equal(first, equal);
        Assert.Equal(first.GetHashCode(), equal.GetHashCode());
        Assert.NotEqual(first, new CorrelationId("correlation-2"));
        Assert.Equal("correlation-1", first.ToString());
        Assert.False(typeof(CorrelationId).GetProperty(nameof(CorrelationId.Value))!.CanWrite);
        Assert.Throws<ArgumentException>(() => new CorrelationId("\t"));
    }

    [Fact]
    public void Reapplying_an_operation_returns_already_applied_without_mutation()
    {
        var original = CreateTask(TaskState.InProgress, new TaskRevision(4));
        var operation = CreateOperation(original, "operation-1", TaskState.Verification);
        var appliedAt = CreatedAt.AddMinutes(1);
        var applied = original.ApplyTransition(operation, appliedAt);
        var repeated = applied.Task.ApplyTransition(operation, CreatedAt.AddMinutes(20));

        Assert.Equal(TaskOperationStatus.Applied, applied.Status);
        Assert.Equal(TaskOperationStatus.AlreadyApplied, repeated.Status);
        Assert.Same(applied.Task, repeated.Task);
        Assert.Equal(original.Id, repeated.Task.Id);
        Assert.Equal(TaskState.Verification, repeated.Task.State);
        Assert.Equal(new TaskRevision(5), repeated.Task.Revision);
        Assert.Equal(appliedAt, repeated.Task.UpdatedAt);
    }

    [Fact]
    public void Reapplying_an_older_operation_after_a_later_operation_is_still_idempotent()
    {
        var original = CreateTask(TaskState.InProgress, new TaskRevision(4));
        var firstOperation = CreateOperation(original, "operation-1", TaskState.Verification);
        var firstResult = original.ApplyTransition(firstOperation, CreatedAt.AddMinutes(1));
        var secondOperation = CreateOperation(firstResult.Task, "operation-2", TaskState.Accepted);
        var secondResult = firstResult.Task.ApplyTransition(secondOperation, CreatedAt.AddMinutes(2));

        var replay = secondResult.Task.ApplyTransition(firstOperation, CreatedAt.AddMinutes(3));

        Assert.Equal(TaskOperationStatus.Applied, secondResult.Status);
        Assert.Equal(TaskOperationStatus.AlreadyApplied, replay.Status);
        Assert.Same(secondResult.Task, replay.Task);
        Assert.Equal(TaskState.Accepted, replay.Task.State);
        Assert.Equal(new TaskRevision(6), replay.Task.Revision);
        Assert.Equal(CreatedAt.AddMinutes(2), replay.Task.UpdatedAt);
    }

    [Fact]
    public void A_different_operation_requesting_the_same_edge_is_not_a_replay()
    {
        var task = CreateTask(TaskState.InProgress, new TaskRevision(4));
        var firstOperation = CreateOperation(task, "operation-1", TaskState.Verification);
        var firstResult = task.ApplyTransition(firstOperation, CreatedAt.AddMinutes(1));
        var repeatedEdgeOperation = CreateOperation(firstResult.Task, "operation-2", TaskState.Verification);
        var repeatedEdgeResult = firstResult.Task.ApplyTransition(
            repeatedEdgeOperation,
            CreatedAt.AddMinutes(2));
        var nextOperation = CreateOperation(firstResult.Task, "operation-3", TaskState.Accepted);
        var nextResult = firstResult.Task.ApplyTransition(nextOperation, CreatedAt.AddMinutes(3));

        Assert.Equal(TaskOperationStatus.Applied, firstResult.Status);
        Assert.Equal(TaskOperationStatus.Rejected, repeatedEdgeResult.Status);
        Assert.Equal(TaskOperationRejection.InvalidTransition, repeatedEdgeResult.Rejection);
        Assert.Equal(TaskOperationStatus.Applied, nextResult.Status);
        Assert.Equal(TaskState.Accepted, nextResult.Task.State);
        Assert.Equal(new TaskRevision(6), nextResult.Task.Revision);
    }

    [Fact]
    public void An_operation_cannot_be_applied_to_a_different_task()
    {
        var intendedTask = CreateTask(TaskState.InProgress, new TaskRevision(4), "task-a");
        var otherTask = CreateTask(TaskState.InProgress, new TaskRevision(4), "task-b");
        var operation = CreateOperation(intendedTask, "operation-1", TaskState.Verification);

        var result = otherTask.ApplyTransition(operation, CreatedAt.AddMinutes(1));

        Assert.Equal(TaskOperationStatus.Rejected, result.Status);
        Assert.Equal(TaskOperationRejection.TaskMismatch, result.Rejection);
        Assert.Same(otherTask, result.Task);
        Assert.Equal(TaskState.InProgress, otherTask.State);
        Assert.Equal(new TaskRevision(4), otherTask.Revision);
        Assert.Equal(CreatedAt, otherTask.UpdatedAt);
    }

    [Fact]
    public void Reusing_an_operation_id_for_different_command_data_is_rejected()
    {
        var task = CreateTask(TaskState.InProgress, new TaskRevision(4));
        var operation = CreateOperation(task, "operation-1", TaskState.Verification);
        var applied = task.ApplyTransition(operation, CreatedAt.AddMinutes(1));
        var conflictingOperation = new TaskTransitionOperation(
            task.Id,
            operation.OperationId,
            operation.ExpectedRevision,
            TaskState.Blocked,
            operation.CorrelationId);

        var result = applied.Task.ApplyTransition(conflictingOperation, CreatedAt.AddMinutes(2));

        Assert.Equal(TaskOperationStatus.Rejected, result.Status);
        Assert.Equal(TaskOperationRejection.OperationIdConflict, result.Rejection);
        Assert.Same(applied.Task, result.Task);
        Assert.Equal(new TaskRevision(5), result.Task.Revision);
        Assert.Equal(TaskState.Verification, result.Task.State);
        Assert.Equal(CreatedAt.AddMinutes(1), result.Task.UpdatedAt);
    }

    [Fact]
    public void A_stale_revision_is_rejected_without_mutation()
    {
        var task = CreateTask(TaskState.InProgress, new TaskRevision(5));
        var operation = new TaskTransitionOperation(
            task.Id,
            new OperationId("operation-stale"),
            new TaskRevision(4),
            TaskState.Verification);

        var result = task.ApplyTransition(operation, CreatedAt.AddMinutes(1));

        Assert.Equal(TaskOperationStatus.Rejected, result.Status);
        Assert.Equal(TaskOperationRejection.StaleRevision, result.Rejection);
        Assert.Same(task, result.Task);
        Assert.Equal(TaskState.InProgress, task.State);
        Assert.Equal(new TaskRevision(5), task.Revision);
        Assert.Equal(CreatedAt, task.UpdatedAt);
    }

    [Fact]
    public void A_current_revision_does_not_bypass_lifecycle_validation()
    {
        var task = CreateTask(TaskState.InProgress, new TaskRevision(5));
        var operation = CreateOperation(task, "operation-illegal", TaskState.Accepted);

        var result = task.ApplyTransition(operation, CreatedAt.AddMinutes(1));

        Assert.Equal(TaskOperationStatus.Rejected, result.Status);
        Assert.Equal(TaskOperationRejection.InvalidTransition, result.Rejection);
        Assert.Same(task, result.Task);
        Assert.Equal(TaskState.InProgress, task.State);
        Assert.Equal(new TaskRevision(5), task.Revision);
    }

    [Fact]
    public void Revision_overflow_rejects_the_operation_atomically()
    {
        var task = CreateTask(TaskState.Proposed, new TaskRevision(int.MaxValue));
        var operation = CreateOperation(task, "operation-overflow", TaskState.Ready);

        var result = task.ApplyTransition(operation, CreatedAt.AddMinutes(1));

        Assert.Equal(TaskOperationStatus.Rejected, result.Status);
        Assert.Equal(TaskOperationRejection.RevisionExhausted, result.Rejection);
        Assert.Same(task, result.Task);
        Assert.Equal(TaskState.Proposed, task.State);
        Assert.Equal(new TaskRevision(int.MaxValue), task.Revision);
        Assert.Equal(CreatedAt, task.UpdatedAt);
    }

    private static TaskTransitionOperation CreateOperation(
        DomainTask task,
        string operationId,
        TaskState targetState) =>
        new(task.Id, new OperationId(operationId), task.Revision, targetState);

    private static DomainTask CreateTask(
        TaskState state,
        TaskRevision revision,
        string taskId = "task-identity") =>
        new(
            new TaskId(taskId),
            "Task intent",
            "Task description",
            [],
            new TaskScope(["src/Core"]),
            [],
            [],
            new ChangeBudget(1, 1, 1, 0, 0),
            state,
            CreatedAt,
            revision);
}