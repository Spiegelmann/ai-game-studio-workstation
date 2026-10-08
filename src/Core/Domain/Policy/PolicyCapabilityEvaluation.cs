namespace AiGameStudioWorkstation.Core.Domain.Policy;

public sealed class PolicyCapabilityEvaluation
{
    internal PolicyCapabilityEvaluation(
        PolicyCapability? requiredCapability,
        PolicyCapabilityAvailability availability,
        PolicyEvaluationOutcome outcome,
        PolicyReason reason,
        IEnumerable<PolicyRegistrationEvaluation> registrations)
    {
        if (!Enum.IsDefined(typeof(PolicyCapabilityAvailability), availability))
        {
            throw new ArgumentOutOfRangeException(nameof(availability), availability, "The capability availability is not defined.");
        }

        if (!Enum.IsDefined(typeof(PolicyEvaluationOutcome), outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The evaluation outcome is not defined.");
        }

        ArgumentNullException.ThrowIfNull(reason);
        ArgumentNullException.ThrowIfNull(registrations);

        var registrationValues = registrations.ToArray();
        if (registrationValues.Any(registration => registration is null))
        {
            throw new ArgumentException("A registration evaluation collection must not contain null values.", nameof(registrations));
        }

        RequiredCapability = requiredCapability;
        Availability = availability;
        Outcome = outcome;
        Reason = reason;
        Registrations = Array.AsReadOnly(registrationValues);
    }

    public PolicyCapability? RequiredCapability { get; }

    public PolicyCapabilityAvailability Availability { get; }

    public PolicyEvaluationOutcome Outcome { get; }

    public PolicyReason Reason { get; }

    public IReadOnlyList<PolicyRegistrationEvaluation> Registrations { get; }
}
