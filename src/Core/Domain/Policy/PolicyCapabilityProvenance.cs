namespace AiGameStudioWorkstation.Core.Domain.Policy;

public sealed record PolicyCapabilityProvenance
{
    public PolicyCapabilityProvenance(
        PolicyCapabilitySource source,
        PolicyCapabilityTrust trust)
    {
        if (!Enum.IsDefined(typeof(PolicyCapabilitySource), source))
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "The policy capability source is not defined.");
        }

        if (!Enum.IsDefined(typeof(PolicyCapabilityTrust), trust))
        {
            throw new ArgumentOutOfRangeException(nameof(trust), trust, "The policy capability trust classification is not defined.");
        }

        if (source == PolicyCapabilitySource.Unknown && trust != PolicyCapabilityTrust.Unknown)
        {
            throw new ArgumentException(
                "A capability with an unknown source must have unknown trust.",
                nameof(trust));
        }

        Source = source;
        Trust = trust;
    }

    public PolicyCapabilitySource Source { get; }

    public PolicyCapabilityTrust Trust { get; }

    public static PolicyCapabilityProvenance Unknown { get; } =
        new(PolicyCapabilitySource.Unknown, PolicyCapabilityTrust.Unknown);
}
