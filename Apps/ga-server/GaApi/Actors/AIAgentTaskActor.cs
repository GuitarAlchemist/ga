namespace GaApi.Actors;

using Messages;
using Models;
using Proto;
using Services;

/// <summary>
///     Actor that manages a single AI agent task (spice-up, reharmonize, create)
///     Provides isolation, cancellation, and progress tracking for long-running AI operations
/// </summary>
public class AiAgentTaskActor : IActor
{
    private readonly ILogger<AiAgentTaskActor> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _taskId;
    private CancellationTokenSource? _cancellationTokenSource;
    private string? _errorMessage;
    private double _progress;
    private GuitarAgentResponse? _result;
    private Task? _runningTask;
    private PID? _self;

    private TaskState _state = TaskState.Pending;
    private string? _statusMessage;

    public AiAgentTaskActor(
        string taskId,
        IServiceProvider serviceProvider,
        ILogger<AiAgentTaskActor> logger)
    {
        _taskId = taskId;
        _serviceProvider = serviceProvider;
        _logger = logger;

        _logger.LogInformation("AI Agent Task Actor created for task {TaskId}", taskId);
    }

    public Task ReceiveAsync(IContext context)
    {
        return context.Message switch
        {
            StartAgentTask msg => HandleStartTask(context, msg),
            GetTaskStatus msg => HandleGetStatus(context, msg),
            CancelTask msg => HandleCancelTask(context, msg),
            UpdateProgress msg => HandleUpdateProgress(context, msg),
            TaskCompleted msg => HandleTaskCompleted(context, msg),
            TaskFailed msg => HandleTaskFailed(context, msg),
            Started => HandleStarted(context),
            Stopping => HandleStopping(context),
            Stopped => HandleStopped(context),
            _ => Task.CompletedTask
        };
    }

    private Task HandleStartTask(IContext context, StartAgentTask msg)
    {
        if (_state != TaskState.Pending)
        {
            _logger.LogWarning("Task {TaskId} already started (state: {State})", _taskId, _state);
            context.Respond(new AgentAck($"Task already in state: {_state}"));
            return Task.CompletedTask;
        }

        _logger.LogInformation("Starting AI agent task {TaskId} of type {AgentType}", _taskId, msg.AgentType);

        _state = TaskState.Running;
        _progress = 0.0;
        _statusMessage = "Starting AI agent task...";
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(msg.CancellationToken);
        _self = context.Self;

        // Start the task asynchronously
        _runningTask = ExecuteTaskAsync(msg);

        context.Respond(new AgentAck("Task started"));
        return Task.CompletedTask;
    }

    private async Task ExecuteTaskAsync(StartAgentTask msg)
    {
        try
        {
            _logger.LogInformation("Executing {AgentType} task {TaskId}", msg.AgentType, _taskId);

            // Create a scope to resolve the scoped orchestrator
            using var scope = _serviceProvider.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IGuitarAgentOrchestrator>();

            var result = msg.AgentType.ToLowerInvariant() switch
            {
                "spice-up" => await orchestrator.SpiceUpProgressionAsync(
                    (SpiceUpProgressionRequest)msg.Request,
                    _cancellationTokenSource!.Token),

                "reharmonize" => await orchestrator.ReharmonizeProgressionAsync(
                    (ReharmonizeProgressionRequest)msg.Request,
                    _cancellationTokenSource!.Token),

                "create" => await orchestrator.CreateProgressionAsync(
                    (CreateProgressionRequest)msg.Request,
                    _cancellationTokenSource!.Token),

                _ => throw new ArgumentException($"Unknown agent type: {msg.AgentType}")
            };

            // Update state directly (we're in the same actor)
            _state = TaskState.Completed;
            _progress = 1.0;
            _statusMessage = "Task completed successfully";
            _result = result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Task {TaskId} was cancelled", _taskId);
            _state = TaskState.Cancelled;
            _statusMessage = "Task cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task {TaskId} failed with error", _taskId);
            _state = TaskState.Failed;
            _statusMessage = "Task failed";
            _errorMessage = ex.Message;
        }
    }

    private Task HandleGetStatus(IContext context, GetTaskStatus msg)
    {
        var response = new TaskStatusResponse(
            _taskId,
            _state,
            _progress,
            _statusMessage,
            _result,
            _errorMessage
        );

        context.Respond(response);
        return Task.CompletedTask;
    }

    private Task HandleCancelTask(IContext context, CancelTask msg)
    {
        if (_state != TaskState.Running)
        {
            _logger.LogWarning("Cannot cancel task {TaskId} in state {State}", _taskId, _state);
            context.Respond(new AgentAck($"Task not running (state: {_state})"));
            return Task.CompletedTask;
        }

        _logger.LogInformation("Cancelling task {TaskId}", _taskId);

        _cancellationTokenSource?.Cancel();
        _state = TaskState.Cancelled;
        _statusMessage = "Task cancelled by user";

        context.Respond(new AgentAck("Task cancelled"));
        return Task.CompletedTask;
    }

    private Task HandleUpdateProgress(IContext context, UpdateProgress msg)
    {
        _progress = msg.Progress;
        _statusMessage = msg.StatusMessage;

        _logger.LogDebug("Task {TaskId} progress: {Progress:P0} - {Status}",
            _taskId, _progress, _statusMessage);

        context.Respond(new AgentAck("Progress updated"));
        return Task.CompletedTask;
    }

    private Task HandleTaskCompleted(IContext context, TaskCompleted msg)
    {
        _logger.LogInformation("Task {TaskId} completed successfully", _taskId);

        _state = TaskState.Completed;
        _progress = 1.0;
        _statusMessage = "Task completed successfully";
        _result = msg.Result;

        context.Respond(new AgentAck("Task completed"));
        return Task.CompletedTask;
    }

    private Task HandleTaskFailed(IContext context, TaskFailed msg)
    {
        _logger.LogError("Task {TaskId} failed: {Error}", _taskId, msg.ErrorMessage);

        _state = TaskState.Failed;
        _statusMessage = "Task failed";
        _errorMessage = msg.ErrorMessage;

        context.Respond(new AgentAck("Task failed"));
        return Task.CompletedTask;
    }

    private Task HandleStarted(IContext context)
    {
        _logger.LogInformation("AI Agent Task Actor started for task {TaskId}", _taskId);
        return Task.CompletedTask;
    }

    private Task HandleStopping(IContext context)
    {
        _logger.LogInformation("AI Agent Task Actor stopping for task {TaskId}", _taskId);

        // Cancel any running task
        _cancellationTokenSource?.Cancel();

        return Task.CompletedTask;
    }

    private Task HandleStopped(IContext context)
    {
        _logger.LogInformation("AI Agent Task Actor stopped for task {TaskId}", _taskId);

        _cancellationTokenSource?.Dispose();

        return Task.CompletedTask;
    }
}
