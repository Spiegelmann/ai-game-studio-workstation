namespace AiGameStudioWorkstation.Core.Domain.Tasks;

public sealed record StopCondition
{
    public StopCondition(string id, string description, bool isMandatory)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A stop condition identifier must not be empty or whitespace.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("A stop condition description must not be empty or whitespace.", nameof(description));
        }

        Id = id;
        Description = description;
        IsMandatory = isMandatory;
    }

    public string Id { get; }

    public string Description { get; }

    public bool IsMandatory { get; }
}
