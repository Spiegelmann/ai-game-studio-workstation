namespace AiGameStudioWorkstation.Core.Domain.Tasks;

public sealed record AcceptanceCriterion
{
    public AcceptanceCriterion(string id, string description, bool isMandatory)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("An acceptance criterion identifier must not be empty or whitespace.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("An acceptance criterion description must not be empty or whitespace.", nameof(description));
        }

        Id = id;
        Description = description;
        IsMandatory = isMandatory;
    }

    public string Id { get; }

    public string Description { get; }

    public bool IsMandatory { get; }
}
