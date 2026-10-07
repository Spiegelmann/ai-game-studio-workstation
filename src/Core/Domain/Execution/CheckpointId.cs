namespace AiGameStudioWorkstation.Core.Domain.Execution;

public sealed record CheckpointId
{
    public CheckpointId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A checkpoint identifier must not be empty or whitespace.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
