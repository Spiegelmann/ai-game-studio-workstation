namespace AiGameStudioWorkstation.Core.Domain.Policy;

public sealed record PolicyCapabilityRegistration
{
    public PolicyCapabilityRegistration(
        PolicyCapability capability,
        PolicyCapabilityProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(provenance);

        Capability = capability;
        Provenance = provenance;
    }

    public PolicyCapability Capability { get; }

    public PolicyCapabilityProvenance Provenance { get; }
}
