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
        : this(status, Array.Empty<PolicyCapability>(), Array.Empty<PolicyRule>())
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
    {
        if (!Enum.IsDefined(typeof(PolicyContextStatus), status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "The policy context status is not defined.");
        }

        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(rules);

        var capabilityValues = capabilities.ToArray();
        if (capabilityValues.Any(capability => capability is null))
        {
            throw new ArgumentException("A policy capability collection must not contain null values.", nameof(capabilities));
        }

        var policyRules = rules.ToArray();
        if (policyRules.Any(rule => rule is null))
        {
            throw new ArgumentException("A policy rule collection must not contain null values.", nameof(rules));
        }

        Status = status;
        Capabilities = Array.AsReadOnly(capabilityValues
            .OrderBy(capability => capability.Value, StringComparer.Ordinal)
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

    public PolicyContextStatus Status { get; }

    public IReadOnlyList<PolicyCapability> Capabilities { get; }

    public IReadOnlyList<PolicyRule> Rules { get; }
}
