namespace AiGameStudioWorkstation.Core.Domain.Execution;

public enum FailureClassification
{
    ValidationFailure,
    PolicyFailure,
    ScopeFailure,
    EnvironmentFailure,
    ToolFailure,
    ProcessFailure,
    Timeout,
    Cancellation,
    BuildFailure,
    TestFailure,
    DependencyFailure,
    StateFailure,
    GitFailure,
    ExternalFailure,
    SecurityFailure,
    UnknownFailure
}
