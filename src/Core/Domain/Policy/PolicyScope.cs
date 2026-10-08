namespace AiGameStudioWorkstation.Core.Domain.Policy;

public enum PolicyScopeKind
{
    Unknown,
    Bounded,
    Global
}

public enum PolicyScopeMatch
{
    Match,
    NoMatch,
    Unknown
}

public sealed record PolicyScope
{
    private PolicyScope(PolicyScopeKind kind, PolicyScopeId? boundaryId)
    {
        if (!Enum.IsDefined(typeof(PolicyScopeKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The policy scope kind is not defined.");
        }

        if ((kind == PolicyScopeKind.Bounded) != (boundaryId is not null))
        {
            throw new ArgumentException("Only a bounded policy scope may have a boundary identifier.", nameof(boundaryId));
        }

        Kind = kind;
        BoundaryId = boundaryId;
    }

    public static PolicyScope Unknown { get; } = new(PolicyScopeKind.Unknown, null);

    public static PolicyScope Global { get; } = new(PolicyScopeKind.Global, null);

    public PolicyScopeKind Kind { get; }

    public PolicyScopeId? BoundaryId { get; }

    public static PolicyScope Bounded(PolicyScopeId boundaryId)
    {
        ArgumentNullException.ThrowIfNull(boundaryId);
        return new PolicyScope(PolicyScopeKind.Bounded, boundaryId);
    }

    public PolicyScopeMatch CompareTo(PolicyScope requestedScope)
    {
        ArgumentNullException.ThrowIfNull(requestedScope);

        if (Kind == PolicyScopeKind.Unknown || requestedScope.Kind == PolicyScopeKind.Unknown)
        {
            return PolicyScopeMatch.Unknown;
        }

        if (Kind == PolicyScopeKind.Global)
        {
            return PolicyScopeMatch.Match;
        }

        if (requestedScope.Kind == PolicyScopeKind.Global)
        {
            return PolicyScopeMatch.NoMatch;
        }

        return BoundaryId == requestedScope.BoundaryId
            ? PolicyScopeMatch.Match
            : PolicyScopeMatch.NoMatch;
    }
}
