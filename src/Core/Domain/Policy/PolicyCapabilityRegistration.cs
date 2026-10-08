namespace AiGameStudioWorkstation.Core.Domain.Policy;

public sealed record PolicyCapabilityRegistration
{
    public PolicyCapabilityRegistration(
        PolicyCapability capability,
        PolicyCapabilityProvenance provenance)
        : this(capability, provenance, PolicyScope.Unknown)
    {
    }

    public PolicyCapabilityRegistration(
        PolicyCapability capability,
        PolicyCapabilityProvenance provenance,
        PolicyScope scope)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(scope);

        Capability = capability;
        Provenance = provenance;
        Scope = scope;
    }

    public PolicyCapability Capability { get; }

    public PolicyCapabilityProvenance Provenance { get; }

    public PolicyScope Scope { get; }
}
