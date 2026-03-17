namespace GA.Business.ML.Agents;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

/// <summary>
/// A request to spawn a subagent.
/// </summary>
public sealed record SubagentRequest(
    string Goal,
    int MaxDurationMinutes = 5,
    IReadOnlyList<string>? AllowedAgentIds = null,
    string? ParentSessionId = null,
    string? AgentHint = null);

/// <summary>
/// The result of a completed subagent.
/// </summary>
public sealed record SubagentResult(
    Guid Id,
    bool Success,
    string Output,
    IReadOnlyList<string> Artifacts,
    TimeSpan Duration,
    string? Error = null);

/// <summary>
/// Status information for an active subagent.
/// </summary>
public sealed record SubagentInfo(
    Guid Id,
    string Goal,
    string Status,
    double Progress,
    DateTime StartedAt);

/// <summary>
/// Manages spawned subagent tasks — spawn, cancel, wait, and query status.
/// Ported from TARS SubagentManager.fs.
/// </summary>
public sealed class SubagentManager(
    Func<SubagentRequest, CancellationToken, Task<SubagentResult>> runSubagent,
    ILogger<SubagentManager> logger)
{
    private readonly ConcurrentDictionary<Guid, (Task<SubagentResult> Task, CancellationTokenSource Cts, SubagentInfo Info)> _active = new();
    private readonly ConcurrentDictionary<Guid, SubagentResult> _completed = new();

    /// <summary>
    /// Spawns a new subagent to work on the given goal.
    /// </summary>
    public Guid Spawn(SubagentRequest request)
    {
        var id = Guid.NewGuid();
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(request.MaxDurationMinutes));
        var info = new SubagentInfo(id, request.Goal, "running", 0.0, DateTime.UtcNow);

        var task = Task.Run(async () =>
        {
            try
            {
                var result = await runSubagent(request, cts.Token);
                var completed = result with { Id = id };
                _completed[id] = completed;
                _active.TryRemove(id, out _);
                logger.LogInformation("Subagent {Id} completed: {Success}", id, completed.Success);
                return completed;
            }
            catch (OperationCanceledException)
            {
                var cancelled = new SubagentResult(id, false, "", [], TimeSpan.Zero, "Cancelled");
                _completed[id] = cancelled;
                _active.TryRemove(id, out _);
                logger.LogWarning("Subagent {Id} cancelled", id);
                return cancelled;
            }
            catch (Exception ex)
            {
                var failed = new SubagentResult(id, false, "", [], TimeSpan.Zero, ex.Message);
                _completed[id] = failed;
                _active.TryRemove(id, out _);
                logger.LogError(ex, "Subagent {Id} failed", id);
                return failed;
            }
        }, cts.Token);

        _active[id] = (task, cts, info);
        logger.LogInformation("Subagent {Id} spawned: {Goal}", id, request.Goal);
        return id;
    }

    /// <summary>
    /// Cancels an active subagent.
    /// </summary>
    public bool Cancel(Guid id)
    {
        if (!_active.TryGetValue(id, out var entry)) return false;
        entry.Cts.Cancel();
        return true;
    }

    /// <summary>
    /// Gets the current status of a subagent (active or completed).
    /// </summary>
    public SubagentInfo? GetStatus(Guid id)
    {
        if (_active.TryGetValue(id, out var entry))
            return entry.Info;
        if (_completed.TryGetValue(id, out var result))
            return new SubagentInfo(id, "", result.Success ? "completed" : "failed", 1.0, DateTime.MinValue);
        return null;
    }

    /// <summary>
    /// Gets the result of a completed subagent.
    /// </summary>
    public SubagentResult? GetResult(Guid id)
        => _completed.TryGetValue(id, out var result) ? result : null;

    /// <summary>
    /// Lists all currently active subagents.
    /// </summary>
    public IReadOnlyList<SubagentInfo> ListActive()
        => _active.Values.Select(v => v.Info).ToList();

    /// <summary>
    /// Waits for a subagent to complete.
    /// </summary>
    public async Task<SubagentResult> WaitAsync(Guid id, CancellationToken ct = default)
    {
        if (_completed.TryGetValue(id, out var result))
            return result;

        if (!_active.TryGetValue(id, out var entry))
            throw new InvalidOperationException($"Subagent {id} not found");

        return await entry.Task.WaitAsync(ct);
    }

    /// <summary>
    /// Cancels all active subagents.
    /// </summary>
    public void CancelAll()
    {
        foreach (var (id, entry) in _active)
        {
            entry.Cts.Cancel();
            logger.LogInformation("Subagent {Id} cancel-all", id);
        }
    }
}
