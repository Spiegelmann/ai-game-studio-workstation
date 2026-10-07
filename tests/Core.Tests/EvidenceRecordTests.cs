using AiGameStudioWorkstation.Core.Domain.Execution;
using AiGameStudioWorkstation.Core.Domain.Tasks;

namespace AiGameStudioWorkstation.Core.Tests;

public sealed class EvidenceRecordTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 10, 2, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructs_evidence_with_provenance_and_optional_references()
    {
        var withoutAttempt = CreateEvidence();
        var withAttempt = CreateEvidence(
            attemptId: new AttemptId("attempt-1"),
            reference: new EvidenceReference("build-result-1"));

        Assert.Equal(new EvidenceId("evidence-1"), withoutAttempt.Id);
        Assert.Equal(new TaskId("task-1"), withoutAttempt.TaskId);
        Assert.Null(withoutAttempt.AttemptId);
        Assert.Equal(EvidenceType.Build, withoutAttempt.Type);
        Assert.Equal(EvidenceSource.Verifier, withoutAttempt.Source);
        Assert.Equal("Build completed successfully.", withoutAttempt.Summary);
        Assert.Equal(Timestamp, withoutAttempt.Timestamp);

        Assert.Equal(new AttemptId("attempt-1"), withAttempt.AttemptId);
        Assert.Equal("build-result-1", withAttempt.Reference!.Value);
    }

    [Fact]
    public void Rejects_null_required_identifiers_and_undefined_values()
    {
        Assert.Throws<ArgumentNullException>(() => new EvidenceRecord(
            null!, new TaskId("task-1"), EvidenceType.Build, EvidenceSource.Verifier, "summary", Timestamp));
        Assert.Throws<ArgumentNullException>(() => new EvidenceRecord(
            new EvidenceId("evidence-1"), null!, EvidenceType.Build, EvidenceSource.Verifier, "summary", Timestamp));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateEvidence(type: (EvidenceType)int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateEvidence(source: (EvidenceSource)int.MaxValue));
        Assert.Throws<ArgumentException>(() => CreateEvidence(summary: " "));
        Assert.Throws<ArgumentException>(() => new EvidenceRecord(
            new EvidenceId("evidence-1"),
            new TaskId("task-1"),
            EvidenceType.Build,
            EvidenceSource.Verifier,
            "summary",
            default));
    }

    [Fact]
    public void Defines_all_evidence_types_and_sources()
    {
        Assert.Equal(
            [
                EvidenceType.Build,
                EvidenceType.Test,
                EvidenceType.Git,
                EvidenceType.Scope,
                EvidenceType.Artifact,
                EvidenceType.Process,
                EvidenceType.Tool,
                EvidenceType.Task,
                EvidenceType.System
            ],
            Enum.GetValues<EvidenceType>());

        Assert.Equal(
            [
                EvidenceSource.System,
                EvidenceSource.Tool,
                EvidenceSource.Verifier,
                EvidenceSource.Agent,
                EvidenceSource.Human,
                EvidenceSource.External
            ],
            Enum.GetValues<EvidenceSource>());
    }

    [Fact]
    public void Keeps_provenance_separate_from_trust_or_authority()
    {
        var agentEvidence = CreateEvidence(source: EvidenceSource.Agent);
        var toolEvidence = CreateEvidence(source: EvidenceSource.Tool);

        Assert.Equal(EvidenceSource.Agent, agentEvidence.Source);
        Assert.Equal(EvidenceSource.Tool, toolEvidence.Source);
        Assert.DoesNotContain(
            typeof(EvidenceRecord).GetProperties(),
            property => property.Name.Contains("Trust", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Authority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evidence_references_are_immutable_value_objects()
    {
        var reference = new EvidenceReference("result-1");

        Assert.Equal("result-1", reference.Value);
        Assert.Equal(reference, new EvidenceReference("result-1"));
        Assert.Throws<ArgumentException>(() => new EvidenceReference(" "));
    }

    private static EvidenceRecord CreateEvidence(
        AttemptId? attemptId = null,
        EvidenceReference? reference = null,
        EvidenceType type = EvidenceType.Build,
        EvidenceSource source = EvidenceSource.Verifier,
        string summary = "Build completed successfully.",
        DateTimeOffset? timestamp = null) =>
        new(
            new EvidenceId("evidence-1"),
            new TaskId("task-1"),
            type,
            source,
            summary,
            timestamp ?? Timestamp,
            attemptId,
            reference);
}
