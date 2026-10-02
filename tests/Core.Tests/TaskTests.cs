using System.Collections.Generic;
using AiGameStudioWorkstation.Core.Domain.Tasks;
using DomainTask = AiGameStudioWorkstation.Core.Domain.Tasks.Task;

namespace AiGameStudioWorkstation.Core.Tests;

public sealed class TaskTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 2, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructs_a_task_with_all_declared_domain_concepts()
    {
        var dependency = new TaskId("foundation-001");
        var criterion = new AcceptanceCriterion("AC-001", "Core builds.", true);
        var stopCondition = new StopCondition("SC-001", "Stop if the scope is unclear.", true);
        var scope = new TaskScope(["src/Core", "tests/Core.Tests"]);
        var budget = new ChangeBudget(4, 3, 7, 0, 1);

        var task = CreateTask(
            dependencies: [dependency],
            scope: scope,
            acceptanceCriteria: [criterion],
            stopConditions: [stopCondition],
            changeBudget: budget);

        Assert.Equal(new TaskId("task-002"), task.Id);
        Assert.Equal("Establish Core domain primitives", task.Intent);
        Assert.Equal("Add the minimum immutable domain model.", task.Description);
        Assert.Equal([dependency], task.Dependencies);
        Assert.Equal(scope, task.Scope);
        Assert.Equal([criterion], task.AcceptanceCriteria);
        Assert.Equal([stopCondition], task.StopConditions);
        Assert.Equal(budget, task.ChangeBudget);
        Assert.Equal(TaskState.Proposed, task.State);
        Assert.Equal(CreatedAt, task.CreatedAt);
        Assert.Equal(CreatedAt, task.UpdatedAt);
        Assert.Equal(new TaskRevision(0), task.Revision);
    }

    [Fact]
    public void Rejects_a_null_identity()
    {
        Assert.Throws<ArgumentNullException>(() => new DomainTask(
            null!,
            "Establish Core domain primitives",
            "Add the minimum immutable domain model.",
            [],
            new TaskScope(["src/Core"]),
            [],
            [],
            new ChangeBudget(5, 5, 10, 0, 0),
            TaskState.Proposed,
            CreatedAt,
            new TaskRevision(0)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Rejects_an_empty_description(string description)
    {
        Assert.Throws<ArgumentException>(() => CreateTask(description: description));
    }

    [Fact]
    public void Rejects_an_empty_intent()
    {
        Assert.Throws<ArgumentException>(() => CreateTask(intent: " "));
    }

    [Fact]
    public void Rejects_an_invalid_task_revision()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TaskRevision(-1));
    }

    [Fact]
    public void Transition_returns_a_new_task_and_updates_state_explicitly()
    {
        var original = CreateTask();
        var updatedAt = CreatedAt.AddMinutes(5);

        var transitioned = original.TransitionTo(TaskState.Ready, updatedAt);

        Assert.Equal(TaskState.Proposed, original.State);
        Assert.Equal(TaskState.Ready, transitioned.State);
        Assert.Equal(CreatedAt, transitioned.CreatedAt);
        Assert.Equal(updatedAt, transitioned.UpdatedAt);
        Assert.Equal(original.Revision, transitioned.Revision);
    }

    [Fact]
    public void Rejects_invalid_transitions_from_a_task()
    {
        var task = CreateTask(state: TaskState.Accepted);

        Assert.Throws<InvalidTaskStateTransitionException>(
            () => task.TransitionTo(TaskState.InProgress, CreatedAt.AddMinutes(1)));
    }

    [Fact]
    public void Preserves_dependencies_and_scope_as_declared_values()
    {
        var dependency = new TaskId("dependency-1");
        var sourceEntries = new[] { "src/Core" };
        var scope = new TaskScope(sourceEntries);
        var task = CreateTask(dependencies: [dependency], scope: scope);
        sourceEntries[0] = "src/Changed";

        Assert.Equal(dependency, task.Dependencies[0]);
        Assert.Equal("src/Core", task.Scope.Entries[0]);
    }

    [Fact]
    public void Task_collections_cannot_be_mutated_through_returned_values()
    {
        var task = CreateTask(
            dependencies: [new TaskId("dependency-1")],
            acceptanceCriteria: [new AcceptanceCriterion("AC-1", "Criterion.", true)],
            stopConditions: [new StopCondition("SC-1", "Stop condition.", false)]);

        Assert.Throws<NotSupportedException>(
            () => ((IList<TaskId>)task.Dependencies).Add(new TaskId("dependency-2")));
        Assert.Throws<NotSupportedException>(
            () => ((IList<AcceptanceCriterion>)task.AcceptanceCriteria)[0] =
                new AcceptanceCriterion("AC-2", "Changed.", false));
        Assert.Throws<NotSupportedException>(
            () => ((IList<StopCondition>)task.StopConditions).Clear());
    }

    [Fact]
    public void Task_scope_is_immutable_and_compares_entries_by_value()
    {
        var entries = new[] { "src/Core", "tests/Core.Tests" };
        var scope = new TaskScope(entries);
        entries[0] = "src/Changed";

        Assert.Equal("src/Core", scope.Entries[0]);
        Assert.Equal(scope, new TaskScope(["src/Core", "tests/Core.Tests"]));
        Assert.NotEqual(scope, new TaskScope(["tests/Core.Tests", "src/Core"]));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)scope.Entries)[0] = "src/Changed");
    }

    private static DomainTask CreateTask(
        TaskId? id = null,
        string intent = "Establish Core domain primitives",
        string description = "Add the minimum immutable domain model.",
        IEnumerable<TaskId>? dependencies = null,
        TaskScope? scope = null,
        IEnumerable<AcceptanceCriterion>? acceptanceCriteria = null,
        IEnumerable<StopCondition>? stopConditions = null,
        ChangeBudget? changeBudget = null,
        TaskState state = TaskState.Proposed,
        TaskRevision? revision = null) =>
        new(
            id ?? new TaskId("task-002"),
            intent,
            description,
            dependencies ?? [],
            scope ?? new TaskScope(["src/Core"]),
            acceptanceCriteria ?? [],
            stopConditions ?? [],
            changeBudget ?? new ChangeBudget(5, 5, 10, 0, 0),
            state,
            CreatedAt,
            revision ?? new TaskRevision(0));
}
