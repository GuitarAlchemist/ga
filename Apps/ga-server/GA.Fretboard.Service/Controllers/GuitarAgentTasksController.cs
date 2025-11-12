namespace GA.Fretboard.Service.Controllers;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.RateLimiting;
using GA.Fretboard.Service.Models;
using GA.Fretboard.Service.Services;

/// <summary>
///     API controller for managing AI Agent tasks using Proto.Actor
///     Provides async task management with progress tracking and cancellation
/// </summary>
[ApiController]
[Route("api/agents/tasks")]
[EnableRateLimiting("fixed")]
public class GuitarAgentTasksController(
    ActorSystemManager actorSystem,
    ILogger<GuitarAgentTasksController> logger)
    : ControllerBase
{
    /// <summary>
    ///     Start a new spice-up progression task
    /// </summary>
    [HttpPost("spice-up")]
    [ProducesResponseType(typeof(ApiResponse<TaskStartResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> StartSpiceUpTask(
        [FromBody] SpiceUpProgressionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("🎨 Starting spice-up task for progression: {Progression}", request.Progression);

            var taskId = await actorSystem.StartAgentTaskAsync("spice-up", request, cancellationToken);

            var response = new TaskStartResponse
            {
                TaskId = taskId,
                Message = "Spice-up task started",
                StatusUrl = $"/api/agents/tasks/{taskId}/status"
            };

            return Ok(ApiResponse<TaskStartResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting spice-up task");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Start a new reharmonize progression task
    /// </summary>
    [HttpPost("reharmonize")]
    [ProducesResponseType(typeof(ApiResponse<TaskStartResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> StartReharmonizeTask(
        [FromBody] ReharmonizeProgressionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("🎵 Starting reharmonize task for progression: {Progression}", request.Progression);

            var taskId = await actorSystem.StartAgentTaskAsync("reharmonize", request, cancellationToken);

            var response = new TaskStartResponse
            {
                TaskId = taskId,
                Message = "Reharmonize task started",
                StatusUrl = $"/api/agents/tasks/{taskId}/status"
            };

            return Ok(ApiResponse<TaskStartResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting reharmonize task");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Start a new create progression task
    /// </summary>
    [HttpPost("create")]
    [ProducesResponseType(typeof(ApiResponse<TaskStartResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> StartCreateTask(
        [FromBody] CreateProgressionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("✨ Starting create progression task in key: {Key}", request.Key);

            var taskId = await actorSystem.StartAgentTaskAsync("create", request, cancellationToken);

            var response = new TaskStartResponse
            {
                TaskId = taskId,
                Message = "Create progression task started",
                StatusUrl = $"/api/agents/tasks/{taskId}/status"
            };

            return Ok(ApiResponse<TaskStartResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting create task");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Get task status
    /// </summary>
    [HttpGet("{taskId}/status")]
    [ProducesResponseType(typeof(ApiResponse<TaskStatusDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> GetTaskStatus(string taskId)
    {
        try
        {
            var status = await actorSystem.GetTaskStatusAsync(taskId);

            var dto = new TaskStatusDto
            {
                TaskId = status.TaskId,
                State = status.State.ToString(),
                Progress = status.Progress,
                StatusMessage = status.StatusMessage,
                Result = status.Result,
                ErrorMessage = status.ErrorMessage
            };

            return Ok(ApiResponse<TaskStatusDto>.Ok(dto));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Task {TaskId} not found", taskId);
            return NotFound(ApiResponse<object>.Fail("Task not found", ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting task status");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Cancel a running task
    /// </summary>
    [HttpPost("{taskId}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> CancelTask(string taskId)
    {
        try
        {
            await actorSystem.CancelTaskAsync(taskId);
            logger.LogInformation("🛑 Cancelled task {TaskId}", taskId);

            return Ok(ApiResponse<object>.Ok(new { message = "Task cancelled successfully" }));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Task {TaskId} not found", taskId);
            return NotFound(ApiResponse<object>.Fail("Task not found", ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cancelling task");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Get all active tasks
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public IActionResult GetActiveTasks()
    {
        try
        {
            var activeTasks = actorSystem.GetActiveAgentTasks().ToList();
            return Ok(ApiResponse<List<string>>.Ok(activeTasks));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting active tasks");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }
}

// ==================
// Response DTOs
// ==================

public class TaskStartResponse
{
    public required string TaskId { get; init; }
    public required string Message { get; init; }
    public required string StatusUrl { get; init; }
}

public class TaskStatusDto
{
    public required string TaskId { get; init; }
    public required string State { get; init; }
    public required double Progress { get; init; }
    public string? StatusMessage { get; init; }
    public GuitarAgentResponse? Result { get; init; }
    public string? ErrorMessage { get; init; }
}
