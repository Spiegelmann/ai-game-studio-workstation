namespace AiGameStudioWorkstation.Core.Domain.Policy;

public sealed record PolicyAction
{
    public PolicyAction(string category)
        : this(category, PolicyScope.Unknown)
    {
    }

    public PolicyAction(string category, PolicyScope scope)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("A policy action category must not be empty or whitespace.", nameof(category));
        }

        ArgumentNullException.ThrowIfNull(scope);

        Category = category;
        Scope = scope;
    }

    public string Category { get; }

    public PolicyScope Scope { get; }
}
