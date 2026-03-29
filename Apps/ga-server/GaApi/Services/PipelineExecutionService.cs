namespace GaApi.Services;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using GaApi.Hubs;
using Microsoft.AspNetCore.SignalR;
using Path = System.IO.Path;

/// <summary>
///     Pipeline stage identifier.
/// </summary>
public enum PipelineStage
{
    Brainstorm,
    Plan,
    Implement,
    Review,
    Compound
}

/// <summary>
///     Tracks a single pipeline run.
/// </summary>
public sealed record PipelineRun(
    string Id,
    string Title,
    string? Source,
    PipelineStage CurrentStage,
    List<string> Log,
    DateTime StartedAt,
    bool Autopilot,
    CancellationTokenSource Cts);

/// <summary>
///     Request to start a pipeline stage or full run.
/// </summary>
public sealed record PipelineRequest(
    string Stage,
    string Title,
    string? Source,
    bool Autopilot = false);

/// <summary>
///     Result of a pipeline stage execution.
/// </summary>
public sealed record PipelineStageResult(
    string Stage,
    bool Success,
    string Output,
    int DurationMs);

/// <summary>
///     Executes pipeline stages by invoking Claude Code CLI as a subprocess.
///     Each stage maps to a specific Claude Code prompt that runs the appropriate
///     skill (/octo:brainstorm, /ce:plan, /ce:work, /ce:review, /ce:compound).
///     Progress is broadcast via SignalR to the Prime Radiant BrainstormPanel.
/// </summary>
public sealed class PipelineExecutionService(
    ILogger<PipelineExecutionService> logger,
    IHubContext<PipelineHub> hubContext,
    IConfiguration configuration)
{
    private readonly ConcurrentDictionary<string, PipelineRun> _activeRuns = new();
    private readonly string _repoRoot = configuration["Pipeline:RepoRoot"]
        ?? FindRepoRoot();

    private static readonly PipelineStage[] AllStages =
        [PipelineStage.Brainstorm, PipelineStage.Plan, PipelineStage.Implement, PipelineStage.Review, PipelineStage.Compound];

    /// <summary>
    ///     Run a single pipeline stage.
    /// </summary>
    public async Task<PipelineStageResult> RunStageAsync(
        string title,
        string? source,
        PipelineStage stage,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var prompt = BuildPrompt(stage, title, source);

        await BroadcastAsync("StageStarted", new { stage = stage.ToString().ToLowerInvariant(), title });

        try
        {
            var output = await ExecuteClaudeAsync(prompt, ct);
            sw.Stop();

            var result = new PipelineStageResult(
                stage.ToString().ToLowerInvariant(),
                true,
                output,
                (int)sw.ElapsedMilliseconds);

            await BroadcastAsync("StageCompleted", result);
            return result;
        }
        catch (OperationCanceledException)
        {
            await BroadcastAsync("PipelineAborted", new { stage = stage.ToString().ToLowerInvariant(), title });
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "Pipeline stage {Stage} failed for {Title}", stage, title);
            await BroadcastAsync("PipelineError", new { stage = stage.ToString().ToLowerInvariant(), error = ex.Message });
            return new PipelineStageResult(stage.ToString().ToLowerInvariant(), false, ex.Message, (int)sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    ///     Run the full pipeline (all 5 stages) with progress tracking.
    /// </summary>
    public async Task<List<PipelineStageResult>> RunFullPipelineAsync(
        string title,
        string? source,
        CancellationToken ct)
    {
        var runId = $"run-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString()[..8]}";
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var run = new PipelineRun(runId, title, source, PipelineStage.Brainstorm, [], DateTime.UtcNow, true, cts);
        _activeRuns[runId] = run;

        await BroadcastAsync("PipelineStarted", new { runId, title, stages = AllStages.Length });

        var results = new List<PipelineStageResult>();
        try
        {
            foreach (var stage in AllStages)
            {
                cts.Token.ThrowIfCancellationRequested();
                _activeRuns[runId] = run with { CurrentStage = stage };

                var result = await RunStageAsync(title, source, stage, cts.Token);
                results.Add(result);
                run.Log.Add($"[{stage.ToString().ToLowerInvariant()}] {(result.Success ? result.Output : $"FAILED: {result.Output}")}");

                if (!result.Success)
                {
                    logger.LogWarning("Pipeline stage {Stage} failed, stopping pipeline", stage);
                    break;
                }
            }

            await BroadcastAsync("PipelineCompleted", new { runId, title, stagesCompleted = results.Count });
        }
        finally
        {
            _activeRuns.TryRemove(runId, out _);
        }

        return results;
    }

    /// <summary>
    ///     Abort an active pipeline run.
    /// </summary>
    public bool Abort(string runId)
    {
        if (!_activeRuns.TryGetValue(runId, out var run)) return false;
        run.Cts.Cancel();
        return true;
    }

    /// <summary>
    ///     Get active pipeline runs.
    /// </summary>
    public IReadOnlyList<PipelineRun> GetActiveRuns() =>
        _activeRuns.Values.ToList();

    // ─── Private ───

    private static string BuildPrompt(PipelineStage stage, string title, string? source) =>
        stage switch
        {
            PipelineStage.Brainstorm => $"""
                You are Demerzel, a governance AI. Brainstorm about this item from the project backlog:

                Title: {title}
                Source: {source ?? "unknown"}

                Provide 3-5 concrete ideas for how to approach this. Consider technical feasibility,
                dependencies, and what infrastructure already exists. Be specific and actionable.
                Keep your response under 500 words.
                """,

            PipelineStage.Plan => $"""
                You are Demerzel. Create a concise implementation plan for:

                Title: {title}
                Source: {source ?? "unknown"}

                Structure as phases with clear deliverables. Identify critical files to modify,
                dependencies, and testing approach. Keep under 500 words.
                """,

            PipelineStage.Implement => $"""
                You are Demerzel. Describe the key implementation steps for:

                Title: {title}
                Source: {source ?? "unknown"}

                List the specific files to create or modify, the code changes needed,
                and the order of operations. Keep under 500 words.
                """,

            PipelineStage.Review => $"""
                You are Demerzel. Review the approach for:

                Title: {title}
                Source: {source ?? "unknown"}

                Check for: security concerns, performance issues, missing edge cases,
                governance compliance, and testing gaps. Keep under 300 words.
                """,

            PipelineStage.Compound => $"""
                You are Demerzel. Document what was learned from working on:

                Title: {title}
                Source: {source ?? "unknown"}

                Capture: key decisions made, patterns discovered, what worked/didn't,
                and what should be done differently next time. Keep under 300 words.
                """,

            _ => throw new ArgumentOutOfRangeException(nameof(stage))
        };

    private async Task<string> ExecuteClaudeAsync(string prompt, CancellationToken ct)
    {
        // Try Claude Code CLI first
        var claudePath = FindClaudeCli();
        if (claudePath != null)
        {
            return await RunProcessAsync(claudePath, $"--print \"{EscapeArg(prompt)}\"", ct);
        }

        // Fallback: try Ollama
        var ollamaUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        return await RunOllamaAsync(ollamaUrl, prompt, ct);
    }

    private async Task<string> RunProcessAsync(string fileName, string arguments, CancellationToken ct)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _repoRoot,
            }
        };

        process.Start();
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var readOut = process.StandardOutput.ReadToEndAsync(ct);
        var readErr = process.StandardError.ReadToEndAsync(ct);

        await Task.WhenAll(readOut, readErr);
        stdout.Append(await readOut);
        stderr.Append(await readErr);

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = stderr.ToString().Trim();
            if (!string.IsNullOrEmpty(error))
                logger.LogWarning("Claude CLI stderr: {Error}", error);
        }

        return stdout.ToString().Trim();
    }

    private async Task<string> RunOllamaAsync(string baseUrl, string prompt, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        var body = JsonSerializer.Serialize(new { model = "llama3.2", prompt, stream = false });
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await http.PostAsync($"{baseUrl}/api/generate", content, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("response").GetString() ?? "";
    }

    private async Task BroadcastAsync(string method, object payload) =>
        await hubContext.Clients.Group(PipelineHub.GroupName).SendAsync(method, payload);

    private static string EscapeArg(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");

    private static string? FindClaudeCli()
    {
        // Check common locations
        var candidates = new[]
        {
            "claude",                                               // on PATH
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "local", "claude.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "claude-code", "claude.exe"),
        };

        foreach (var path in candidates)
        {
            try
            {
                var psi = new ProcessStartInfo(path, "--version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    p.WaitForExit(3000);
                    if (p.ExitCode == 0) return path;
                }
            }
            catch { /* not found */ }
        }

        return null;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git"))) return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return AppContext.BaseDirectory;
    }
}
