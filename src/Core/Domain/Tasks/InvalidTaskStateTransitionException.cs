namespace AiGameStudioWorkstation.Core.Domain.Tasks;

public sealed class InvalidTaskStateTransitionException : InvalidOperationException
{
    public InvalidTaskStateTransitionException(TaskState from, TaskState to)
        : base($"A task cannot transition from '{from}' to '{to}'.")
    {
        From = from;
        To = to;
    }

    public TaskState From { get; }

    public TaskState To { get; }
}
