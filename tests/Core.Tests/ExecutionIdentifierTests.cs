using AiGameStudioWorkstation.Core.Domain.Execution;

namespace AiGameStudioWorkstation.Core.Tests;

public sealed class ExecutionIdentifierTests
{
    [Fact]
    public void Dedicated_identifiers_validate_values_and_have_value_equality()
    {
        var identifiers = new[]
        {
            new AttemptId("attempt-1").Value,
            new InvocationId("invocation-1").Value,
            new TransitionId("transition-1").Value,
            new EvidenceId("evidence-1").Value,
            new CheckpointId("checkpoint-1").Value,
            new RecoveryDecisionId("recovery-1").Value,
            new HumanDecisionId("human-decision-1").Value,
            new CapabilityId("DotNet.Build").Value
        };

        Assert.Equal(
            [
                "attempt-1",
                "invocation-1",
                "transition-1",
                "evidence-1",
                "checkpoint-1",
                "recovery-1",
                "human-decision-1",
                "DotNet.Build"
            ],
            identifiers);
        Assert.Equal(new AttemptId("attempt-1"), new AttemptId("attempt-1"));
            Assert.False(new AttemptId("attempt-1").Equals(new InvocationId("attempt-1")));
        Assert.Equal("attempt-1", new AttemptId("attempt-1").ToString());
    }

    [Fact]
    public void Dedicated_identifiers_reject_empty_and_whitespace_values()
    {
        Assert.Throws<ArgumentException>(() => new AttemptId(""));
        Assert.Throws<ArgumentException>(() => new InvocationId(" "));
        Assert.Throws<ArgumentException>(() => new TransitionId("\t"));
        Assert.Throws<ArgumentException>(() => new EvidenceId(""));
        Assert.Throws<ArgumentException>(() => new CheckpointId(" "));
        Assert.Throws<ArgumentException>(() => new RecoveryDecisionId("\t"));
        Assert.Throws<ArgumentException>(() => new HumanDecisionId(""));
        Assert.Throws<ArgumentException>(() => new CapabilityId(" "));
    }
}
