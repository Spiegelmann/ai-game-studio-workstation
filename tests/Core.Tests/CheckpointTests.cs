using System.Collections.Generic;
using AiGameStudioWorkstation.Core.Domain.Execution;
using AiGameStudioWorkstation.Core.Domain.Tasks;

namespace AiGameStudioWorkstation.Core.Tests;

public sealed class CheckpointTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 10, 2, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructs_a_checkpoint_with_recovery_and_workflow_references()
    {
        var evidenceReferences = new[] { new EvidenceId("evidence-1") };
        var checkpoint = CreateCheckpoint(
            evidenceReferences,
            lastCompletedInvocationId: new InvocationId("invocation-1"),
            activeInvocationId: new InvocationId("invocation-2"));

        Assert.Equal(new CheckpointId("checkpoint-1"), checkpoint.Id);
        Assert.Equal(new TaskId("task-1"), checkpoint.TaskId);
        Assert.Equal(new AttemptId("attempt-1"), checkpoint.AttemptId);
        Assert.Equal(TaskState.InProgress, checkpoint.TaskState);
        Assert.Equal("compile", checkpoint.StepIdentifier);
        Assert.Equal("revision-42", checkpoint.RepositoryRevision);
        Assert.Equal("workspace-state-1", checkpoint.WorkspaceStateReference);
        Assert.Equal(new InvocationId("invocation-1"), checkpoint.LastCompletedInvocationId);
        Assert.Equal(new InvocationId("invocation-2"), checkpoint.ActiveInvocationId);
        Assert.Equal([new EvidenceId("evidence-1")], checkpoint.EvidenceReferences);
        Assert.Equal("Build step was interrupted.", checkpoint.RecoveryContext.Reason);
        Assert.Equal(Timestamp, checkpoint.Timestamp);
    }

    [Fact]
    public void Requires_valid_identities_state_references_and_context()
    {
        Assert.Throws<ArgumentNullException>(() => new Checkpoint(
            null!, new TaskId("task-1"), new AttemptId("attempt-1"), TaskState.InProgress,
            "step", "revision", "workspace", new RecoveryContext("reason"), Timestamp, []));
        Assert.Throws<ArgumentNullException>(() => new Checkpoint(
            new CheckpointId("checkpoint-1"), null!, new AttemptId("attempt-1"), TaskState.InProgress,
            "step", "revision", "workspace", new RecoveryContext("reason"), Timestamp, []));
        Assert.Throws<ArgumentNullException>(() => new Checkpoint(
            new CheckpointId("checkpoint-1"), new TaskId("task-1"), null!, TaskState.InProgress,
            "step", "revision", "workspace", new RecoveryContext("reason"), Timestamp, []));
        Assert.Throws<ArgumentNullException>(() => new Checkpoint(
            new CheckpointId("checkpoint-1"), new TaskId("task-1"), new AttemptId("attempt-1"), TaskState.InProgress,
            "step", "revision", "workspace", null!, Timestamp, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateCheckpoint(taskState: (TaskState)int.MaxValue));
        Assert.Throws<ArgumentException>(() => CreateCheckpoint(stepIdentifier: " "));
        Assert.Throws<ArgumentException>(() => CreateCheckpoint(repositoryRevision: ""));
        Assert.Throws<ArgumentException>(() => CreateCheckpoint(workspaceStateReference: " "));
        Assert.Throws<ArgumentException>(() => new Checkpoint(
            new CheckpointId("checkpoint-1"),
            new TaskId("task-1"),
            new AttemptId("attempt-1"),
            TaskState.InProgress,
            "step",
            "revision",
            "workspace",
            new RecoveryContext("reason"),
            default,
            []));
    }

    [Fact]
    public void Defensively_copies_and_exposes_evidence_references_read_only()
    {
        var evidenceReferences = new List<EvidenceId> { new("evidence-1") };
        var checkpoint = CreateCheckpoint(evidenceReferences);
        evidenceReferences.Add(new EvidenceId("evidence-2"));

        Assert.Single(checkpoint.EvidenceReferences);
        Assert.Throws<NotSupportedException>(
            () => ((IList<EvidenceId>)checkpoint.EvidenceReferences).Add(new EvidenceId("evidence-3")));
    }

    [Fact]
    public void Rejects_null_evidence_references()
    {
        Assert.Throws<ArgumentNullException>(() => new Checkpoint(
            new CheckpointId("checkpoint-1"),
            new TaskId("task-1"),
            new AttemptId("attempt-1"),
            TaskState.InProgress,
            "step",
            "revision",
            "workspace",
            new RecoveryContext("reason"),
            Timestamp,
            null!));
        Assert.Throws<ArgumentException>(() => CreateCheckpoint([null!]));
    }

    private static Checkpoint CreateCheckpoint(
        IEnumerable<EvidenceId>? evidenceReferences = null,
        TaskState taskState = TaskState.InProgress,
        string stepIdentifier = "compile",
        string repositoryRevision = "revision-42",
        string workspaceStateReference = "workspace-state-1",
        DateTimeOffset? timestamp = null,
        InvocationId? lastCompletedInvocationId = null,
        InvocationId? activeInvocationId = null) =>
        new(
            new CheckpointId("checkpoint-1"),
            new TaskId("task-1"),
            new AttemptId("attempt-1"),
            taskState,
            stepIdentifier,
            repositoryRevision,
            workspaceStateReference,
            new RecoveryContext(
                "Build step was interrupted.",
                FailureClassification.ProcessFailure,
                activeInvocationId),
            timestamp ?? Timestamp,
            evidenceReferences ?? [new EvidenceId("evidence-1")],
            lastCompletedInvocationId,
            activeInvocationId);
}
