namespace AiGameStudioWorkstation.Core.Domain.Tasks;

public readonly record struct TaskRevision
{
    public TaskRevision(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "A task revision cannot be negative.");
        }

        Value = value;
    }

    public int Value { get; }
}
