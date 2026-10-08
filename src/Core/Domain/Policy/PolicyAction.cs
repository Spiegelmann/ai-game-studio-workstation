namespace AiGameStudioWorkstation.Core.Domain.Policy;

public sealed record PolicyAction
{
    public PolicyAction(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("A policy action category must not be empty or whitespace.", nameof(category));
        }

        Category = category;
    }

    public string Category { get; }
}
