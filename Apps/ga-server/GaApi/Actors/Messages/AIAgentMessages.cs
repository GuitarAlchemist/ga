namespace GaApi.Actors.Messages;

using Models;

/// <summary>
///     Base message for AI agent task actor
/// </summary>
public abstract record AIAgentMessage;

/// <summary>
///     Start a new AI agent task
/// </summary>
public sealed record StartAgentTask(
    string TaskId,
    string AgentType, // "spice-up", "reharmonize", "create"
    object Request,
    CancellationToken CancellationToken
) : AIAgentMessage;

/// <summary>
///     Get task status
/// </summary>
public sealed record GetTaskStatus(string TaskId) : AIAgentMessage;

/// <summary>
///     Task status response
/// </summary>
public sealed record TaskStatusResponse(
    string TaskId,
    TaskState State,
    double Progress, // 0.0 to 1.0
    string? StatusMessage = null,
    GuitarAgentResponse? Result = null,
    string? ErrorMessage = null
);

/// <summary>
///     Task state enumeration
/// </summary>
public enum TaskState
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
///     Cancel a running task
/// </summary>
public sealed record CancelTask(string TaskId) : AIAgentMessage;

/// <summary>
///     Task completed successfully
/// </summary>
public sealed record TaskCompleted(
    string TaskId,
    GuitarAgentResponse Result
) : AIAgentMessage;

/// <summary>
///     Task failed
/// </summary>
public sealed record TaskFailed(
    string TaskId,
    string ErrorMessage,
    Exception? Exception = null
) : AIAgentMessage;

/// <summary>
///     Update task progress
/// </summary>
public sealed record UpdateProgress(
    string TaskId,
    double Progress,
    string StatusMessage
) : AIAgentMessage;

/// <summary>
///     Get all active tasks
/// </summary>
public sealed record GetActiveTasks : AIAgentMessage;

/// <summary>
///     Response containing all active tasks
/// </summary>
public sealed record ActiveTasksResponse(
    Dictionary<string, TaskStatusResponse> Tasks
);

/// <summary>
///     Acknowledgment message
/// </summary>
public sealed record AgentAck(string Message = "OK");
