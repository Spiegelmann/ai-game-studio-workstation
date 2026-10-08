namespace AiGameStudioWorkstation.Core.Domain.Policy;

public enum PolicyContextStatus
{
    Complete,
    Incomplete,
    Unknown
}

public sealed record PolicyContext
{
    public PolicyContext(PolicyContextStatus status)
        : this(status, Array.Empty<PolicyCapabilityRegistration>(), Array.Empty<PolicyRule>())
    {
    }

    public PolicyContext(PolicyContextStatus status, IEnumerable<PolicyCapability> capabilities)
        : this(status, capabilities, Array.Empty<PolicyRule>())
    {
    }

    public PolicyContext(
        PolicyContextStatus status,
        IEnumerable<PolicyCapability> capabilities,
        IEnumerable<PolicyRule> rules)
        : this(status, CreateUnknownRegistrations(capabilities), rules)
    {
    }

    private PolicyContext(
        PolicyContextStatus status,
        IEnumerable<PolicyCapabilityRegistration> registrations,
        IEnumerable<PolicyRule> rules)
    {
        if (!Enum.IsDefined(typeof(PolicyContextStatus), status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "The policy context status is not defined.");
        }

        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(rules);

        var registrationValues = registrations.ToArray();
        if (registrationValues.Any(registration => registration is null))
        {
            throw new ArgumentException("A capability registration collection must not contain null values.", nameof(registrations));
        }

        var conflictingRegistration = registrationValues
            .GroupBy(registration => registration.Capability.Value, StringComparer.Ordinal)
            .FirstOrDefault(group => group
                .Select(registration => registration.Provenance)
                .Distinct()
                .Skip(1)
                .Any());
        if (conflictingRegistration is not null)
        {
            throw new ArgumentException(
                $"Capability '{conflictingRegistration.Key}' has conflicting provenance registrations.",
                nameof(registrations));
        }

        var policyRules = rules.ToArray();
        if (policyRules.Any(rule => rule is null))
        {
            throw new ArgumentException("A policy rule collection must not contain null values.", nameof(rules));
        }

        Status = status;
        Registrations = Array.AsReadOnly(registrationValues
            .OrderBy(registration => registration.Capability.Value, StringComparer.Ordinal)
            .ThenBy(registration => registration.Provenance.Source)
            .ThenBy(registration => registration.Provenance.Trust)
            .ThenBy(registration => registration.Scope.Kind)
            .ThenBy(registration => registration.Scope.BoundaryId?.Value, StringComparer.Ordinal)
            .Distinct()
            .ToArray());
        Capabilities = Array.AsReadOnly(Registrations
            .Select(registration => registration.Capability)
            .DistinctBy(capability => capability.Value, StringComparer.Ordinal)
            .ToArray());
        Rules = Array.AsReadOnly(policyRules
            .OrderBy(rule => rule.Action.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(rule => rule.Action.Category, StringComparer.Ordinal)
            .ThenBy(rule => rule.Decision.Outcome)
            .ThenBy(rule => rule.Decision.Reason.Code, StringComparer.Ordinal)
            .ThenBy(rule => rule.Decision.Reason.Explanation, StringComparer.Ordinal)
            .ToArray());
    }

    public static PolicyContext FromRegistrations(
        PolicyContextStatus status,
        IEnumerable<PolicyCapabilityRegistration> registrations) =>
        new(status, registrations, Array.Empty<PolicyRule>());

    public static PolicyContext FromRegistrations(
        PolicyContextStatus status,
        IEnumerable<PolicyCapabilityRegistration> registrations,
        IEnumerable<PolicyRule> rules) =>
        new(status, registrations, rules);

    public PolicyContextStatus Status { get; }

    public IReadOnlyList<PolicyCapability> Capabilities { get; }

    public IReadOnlyList<PolicyCapabilityRegistration> Registrations { get; }

    public IReadOnlyList<PolicyRule> Rules { get; }

    private static IEnumerable<PolicyCapabilityRegistration> CreateUnknownRegistrations(
        IEnumerable<PolicyCapability> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        var capabilityValues = capabilities.ToArray();
        if (capabilityValues.Any(capability => capability is null))
        {
            throw new ArgumentException("A policy capability collection must not contain null values.", nameof(capabilities));
        }

        return capabilityValues.Select(capability =>
            new PolicyCapabilityRegistration(capability, PolicyCapabilityProvenance.Unknown));
    }
}
