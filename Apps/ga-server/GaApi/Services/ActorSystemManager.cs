namespace GaApi.Services;

using System.Collections.Concurrent;
using Actors;
using Actors.Messages;
using Proto;

/// <summary>
///     Manages the Proto.Actor system and provides access to player session and AI agent actors
///     Singleton service that coordinates all actor lifecycle management
/// </summary>
public sealed class ActorSystemManager : IDisposable
{
    private readonly ActorSystem _actorSystem;

    // Track active AI agent task actors
    private readonly ConcurrentDictionary<string, PID> _agentTasks = new();
    private readonly ILogger<ActorSystemManager> _logger;
    private readonly ILoggerFactory _loggerFactory;

    // Track active player session actors
    private readonly ConcurrentDictionary<string, PID> _playerSessions = new();
    private readonly IServiceProvider _serviceProvider;

    public ActorSystemManager(
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider,
        ILogger<ActorSystemManager> logger)
    {
        _loggerFactory = loggerFactory;
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Create the actor system
        _actorSystem = new ActorSystem();

        _logger.LogInformation("Actor System Manager initialized");
    }

    public void Dispose()
    {
        _logger.LogInformation("Shutting down Actor System Manager");

        // Stop all player sessions
        foreach (var playerId in _playerSessions.Keys.ToList())
        {
            StopPlayerSession(playerId).Wait(TimeSpan.FromSeconds(5));
        }

        // Stop all agent tasks
        foreach (var taskId in _agentTasks.Keys.ToList())
        {
            StopAgentTask(taskId).Wait(TimeSpan.FromSeconds(5));
        }

        // Shutdown the actor system
        _actorSystem.ShutdownAsync().Wait(TimeSpan.FromSeconds(10));
    }

    #region Player Session Actors

    /// <summary>
    ///     Get or create a player session actor
    /// </summary>
    public PID GetOrCreatePlayerSession(string playerId)
    {
        return _playerSessions.GetOrAdd(playerId, id =>
        {
            _logger.LogInformation("Creating player session actor for {PlayerId}", id);

            var props = Props.FromProducer(() => new PlayerSessionActor(id, _loggerFactory));
            var pid = _actorSystem.Root.Spawn(props);

            return pid;
        });
    }

    /// <summary>
    ///     Send a message to a player session actor and wait for response
    /// </summary>
    public async Task<TResponse> AskPlayerSession<TResponse>(
        string playerId,
        PlayerSessionMessage message,
        TimeSpan? timeout = null)
    {
        var pid = GetOrCreatePlayerSession(playerId);
        var response = await _actorSystem.Root.RequestAsync<TResponse>(
            pid,
            message,
            timeout ?? TimeSpan.FromSeconds(5));

        return response;
    }

    /// <summary>
    ///     Send a message to a player session actor (fire and forget)
    /// </summary>
    public void TellPlayerSession(string playerId, PlayerSessionMessage message)
    {
        var pid = GetOrCreatePlayerSession(playerId);
        _actorSystem.Root.Send(pid, message);
    }

    /// <summary>
    ///     Stop a player session actor
    /// </summary>
    public async Task StopPlayerSession(string playerId)
    {
        if (_playerSessions.TryRemove(playerId, out var pid))
        {
            _logger.LogInformation("Stopping player session actor for {PlayerId}", playerId);
            await _actorSystem.Root.StopAsync(pid);
        }
    }

    /// <summary>
    ///     Get all active player session IDs
    /// </summary>
    public IReadOnlyCollection<string> GetActivePlayerSessions()
    {
        return _playerSessions.Keys.ToList();
    }

    #endregion

    #region AI Agent Task Actors

    /// <summary>
    ///     Create a new AI agent task actor
    /// </summary>
    public PID CreateAgentTask(string taskId)
    {
        if (_agentTasks.ContainsKey(taskId))
        {
            throw new InvalidOperationException($"Task {taskId} already exists");
        }

        _logger.LogInformation("Creating AI agent task actor for {TaskId}", taskId);

        var props = Props.FromProducer(() => new AIAgentTaskActor(
            taskId,
            _serviceProvider,
            _loggerFactory.CreateLogger<AIAgentTaskActor>()));

        var pid = _actorSystem.Root.Spawn(props);
        _agentTasks[taskId] = pid;

        return pid;
    }

    /// <summary>
    ///     Get an existing AI agent task actor
    /// </summary>
    public PID? GetAgentTask(string taskId)
    {
        _agentTasks.TryGetValue(taskId, out var pid);
        return pid;
    }

    /// <summary>
    ///     Send a message to an AI agent task actor and wait for response
    /// </summary>
    public async Task<TResponse> AskAgentTask<TResponse>(
        string taskId,
        AIAgentMessage message,
        TimeSpan? timeout = null)
    {
        var pid = GetAgentTask(taskId);
        if (pid == null)
        {
            throw new InvalidOperationException($"Task {taskId} not found");
        }

        var response = await _actorSystem.Root.RequestAsync<TResponse>(
            pid,
            message,
            timeout ?? TimeSpan.FromSeconds(30));

        return response;
    }

    /// <summary>
    ///     Send a message to an AI agent task actor (fire and forget)
    /// </summary>
    public void TellAgentTask(string taskId, AIAgentMessage message)
    {
        var pid = GetAgentTask(taskId);
        if (pid != null)
        {
            _actorSystem.Root.Send(pid, message);
        }
    }

    /// <summary>
    ///     Stop an AI agent task actor
    /// </summary>
    public async Task StopAgentTask(string taskId)
    {
        if (_agentTasks.TryRemove(taskId, out var pid))
        {
            _logger.LogInformation("Stopping AI agent task actor for {TaskId}", taskId);
            await _actorSystem.Root.StopAsync(pid);
        }
    }

    /// <summary>
    ///     Get all active agent task IDs
    /// </summary>
    public IReadOnlyCollection<string> GetActiveAgentTasks()
    {
        return _agentTasks.Keys.ToList();
    }

    /// <summary>
    ///     Start a new AI agent task
    /// </summary>
    public async Task<string> StartAgentTaskAsync(
        string agentType,
        object request,
        CancellationToken cancellationToken = default)
    {
        var taskId = Guid.NewGuid().ToString("N");
        var pid = CreateAgentTask(taskId);

        var startMessage = new StartAgentTask(taskId, agentType, request, cancellationToken);
        await _actorSystem.Root.RequestAsync<AgentAck>(pid, startMessage, TimeSpan.FromSeconds(5));

        return taskId;
    }

    /// <summary>
    ///     Get task status
    /// </summary>
    public async Task<TaskStatusResponse> GetTaskStatusAsync(string taskId)
    {
        return await AskAgentTask<TaskStatusResponse>(
            taskId,
            new GetTaskStatus(taskId),
            TimeSpan.FromSeconds(5));
    }

    /// <summary>
    ///     Cancel a running task
    /// </summary>
    public async Task CancelTaskAsync(string taskId)
    {
        await AskAgentTask<AgentAck>(
            taskId,
            new CancelTask(taskId),
            TimeSpan.FromSeconds(5));
    }

    #endregion
}
