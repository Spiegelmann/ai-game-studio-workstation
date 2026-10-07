using AiGameStudioWorkstation.Core.Domain.Execution;
using AiGameStudioWorkstation.Core.Domain.Tasks;

namespace AiGameStudioWorkstation.Core.Tests;

public sealed class TaskAttemptTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 10, 2, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructs_an_attempt_with_its_own_identity_and_task_reference()
    {
        var attempt = CreateAttempt();

        Assert.Equal(new AttemptId("attempt-1"), attempt.Id);
        Assert.Equal(new TaskId("task-1"), attempt.TaskId);
        Assert.Equal(1, attempt.Number);
        Assert.Equal(TaskAttemptState.Created, attempt.State);
        Assert.Equal(StartedAt, attempt.StartedAt);
        Assert.Null(attempt.CompletedAt);
    }

    [Fact]
    public void Rejects_null_identifiers()
    {
        Assert.Throws<ArgumentNullException>(() => new TaskAttempt(
            null!, new TaskId("task-1"), 1, TaskAttemptState.Created, StartedAt));
        Assert.Throws<ArgumentNullException>(() => new TaskAttempt(
            new AttemptId("attempt-1"), null!, 1, TaskAttemptState.Created, StartedAt));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_non_positive_attempt_numbers(int number)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateAttempt(number: number));
    }

    [Fact]
    public void Rejects_an_undefined_state()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateAttempt(state: (TaskAttemptState)int.MaxValue));
    }

    [Fact]
    public void Rejects_missing_or_default_start_timestamps()
    {
        Assert.Throws<ArgumentException>(() => new TaskAttempt(
            new AttemptId("attempt-1"),
            new TaskId("task-1"),
            1,
            TaskAttemptState.Created,
            default));
    }

    [Fact]
    public void Rejects_completion_before_start()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateAttempt(completedAt: StartedAt.AddTicks(-1)));
    }

    [Theory]
    [InlineData(TaskAttemptState.Completed)]
    [InlineData(TaskAttemptState.Failed)]
    [InlineData(TaskAttemptState.Cancelled)]
    [InlineData(TaskAttemptState.Unknown)]
    public void Terminal_attempts_cannot_return_to_running(TaskAttemptState terminalState)
    {
        var attempt = CreateAttempt(state: terminalState);

        Assert.Throws<InvalidOperationException>(() => attempt.TransitionTo(TaskAttemptState.Running));
    }

    [Fact]
    public void Unknown_remains_distinct_from_failed_and_completed()
    {
        var unknown = CreateAttempt(state: TaskAttemptState.Unknown);
        var failed = CreateAttempt(state: TaskAttemptState.Failed);
        var completed = CreateAttempt(state: TaskAttemptState.Completed);

        Assert.NotEqual(unknown.State, failed.State);
        Assert.NotEqual(unknown.State, completed.State);
    }

    [Fact]
    public void Explicit_state_transition_returns_a_new_attempt()
    {
        var original = CreateAttempt();
        var updated = original.TransitionTo(TaskAttemptState.Running);

        Assert.Equal(TaskAttemptState.Created, original.State);
        Assert.Equal(TaskAttemptState.Running, updated.State);
    }

    [Fact]
    public void Defines_all_attempt_states()
    {
        Assert.Equal(
            [
                TaskAttemptState.Created,
                TaskAttemptState.Running,
                TaskAttemptState.Completed,
                TaskAttemptState.Failed,
                TaskAttemptState.Cancelled,
                TaskAttemptState.Unknown
            ],
            Enum.GetValues<TaskAttemptState>());
    }

    private static TaskAttempt CreateAttempt(
        int number = 1,
        TaskAttemptState state = TaskAttemptState.Created,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? completedAt = null) =>
        new(
            new AttemptId("attempt-1"),
            new TaskId("task-1"),
            number,
            state,
            startedAt ?? StartedAt,
            completedAt);
}
