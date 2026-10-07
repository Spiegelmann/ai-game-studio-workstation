namespace AiGameStudioWorkstation.Application.Tasks.Persistence;

public class TaskPersistenceException : Exception
{
    public TaskPersistenceException(string message)
        : base(message)
    {
    }

    public TaskPersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class TaskConcurrencyException : TaskPersistenceException
{
    public TaskConcurrencyException(string message)
        : base(message)
    {
    }
}

public sealed class TaskDataCorruptionException : TaskPersistenceException
{
    public TaskDataCorruptionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class TaskNotFoundException : Exception
{
    public TaskNotFoundException(string taskId)
        : base($"Task '{taskId}' does not exist.")
    {
    }
}
