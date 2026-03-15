namespace GA.Business.ML.Agents.Hooks;

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// Captures orchestration events and writes TARS-compatible TraceArtifact JSON files
/// to <c>~/.ga/traces/</c> for cross-repo pattern discovery by TARS/MachinDeOuf.
/// </summary>
/// <remarks>
/// Registration order: after MemoryHook and ObservabilityHook so it observes final state.
/// All file I/O is fire-and-forget to avoid blocking the response pipeline.
/// </remarks>
public sealed class TraceBridgeHook(ILogger<TraceBridgeHook> logger) : IChatHook
{
    private const int MaxTraceFiles = 1000;

    private static readonly string TraceDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ga", "traces");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Per-request tracking state accumulated across lifecycle points.</summary>
    private sealed record BridgeState(
        string OriginalMessage,
        string? MatchedSkillName = null,
        AgentResponse? Response = null);

    private readonly ConcurrentDictionary<Guid, BridgeState> _inflight = new();

    public Task<HookResult> OnRequestReceived(ChatHookContext ctx, CancellationToken ct = default)
    {
        _inflight[ctx.CorrelationId] = new BridgeState(ctx.OriginalMessage);
        return Task.FromResult(HookResult.Continue);
    }

    public Task<HookResult> OnAfterSkill(ChatHookContext ctx, CancellationToken ct = default)
    {
        if (_inflight.TryGetValue(ctx.CorrelationId, out var state))
        {
            _inflight[ctx.CorrelationId] = state with
            {
                MatchedSkillName = ctx.MatchedSkillName,
                Response = ctx.Response,
            };

            // Emit a skill-specific trace immediately
            if (ctx.MatchedSkillName is not null && ctx.Response is not null)
            {
                EmitTrace(ctx.CorrelationId, new TraceArtifactDto
                {
                    TaskId = $"ga-{ctx.CorrelationId:N}",
                    PatternName = $"ga.skill.{ctx.MatchedSkillName}",
                    PatternTemplate = $"ga.skill.{ctx.MatchedSkillName}",
                    Context = $"Skill '{ctx.MatchedSkillName}' handled query",
                    Score = ctx.Response.Confidence,
                    Timestamp = DateTimeOffset.UtcNow,
                    EventType = "skill_execution",
                    GaMetadata = new GaMetadataDto
                    {
                        AgentId = ctx.Response.AgentId,
                        Confidence = ctx.Response.Confidence,
                        SkillName = ctx.MatchedSkillName,
                        OriginalQuery = Truncate(ctx.OriginalMessage, 200),
                    },
                });
            }
        }
        return Task.FromResult(HookResult.Continue);
    }

    public Task<HookResult> OnResponseSent(ChatHookContext ctx, CancellationToken ct = default)
    {
        if (!_inflight.TryRemove(ctx.CorrelationId, out var state)) return Task.FromResult(HookResult.Continue);
        if (ctx.Response is null) return Task.FromResult(HookResult.Continue);

        var response = ctx.Response;
        var wasSkill = state.MatchedSkillName is not null;
        var routingMethod = wasSkill ? "orchestrator-skill" : "agent-routed";

        // Emit routing/agent response trace
        EmitTrace(ctx.CorrelationId, new TraceArtifactDto
        {
            TaskId = $"ga-{ctx.CorrelationId:N}",
            PatternName = wasSkill
                ? $"ga.routing.skill.{state.MatchedSkillName}"
                : $"ga.routing.{response.AgentId}",
            PatternTemplate = $"ga.routing.{response.AgentId}",
            Context = $"Query routed to {response.AgentId} via {routingMethod}",
            Score = response.Confidence,
            Timestamp = DateTimeOffset.UtcNow,
            RollbackExpansion = $"query: {Truncate(state.OriginalMessage, 300)} | agent: {response.AgentId} | confidence: {response.Confidence:F2}",
            EventType = "routing_decision",
            GaMetadata = new GaMetadataDto
            {
                AgentId = response.AgentId,
                Confidence = response.Confidence,
                RoutingMethod = routingMethod,
                SkillName = state.MatchedSkillName,
                OriginalQuery = Truncate(state.OriginalMessage, 200),
            },
        });

        // Emit agent response quality trace (non-skill only — skill already emitted in OnAfterSkill)
        if (!wasSkill && response.Result.Length > 50)
        {
            EmitTrace(ctx.CorrelationId, new TraceArtifactDto
            {
                TaskId = $"ga-{ctx.CorrelationId:N}-resp",
                PatternName = $"ga.agent.{response.AgentId}.response",
                PatternTemplate = $"ga.agent.{response.AgentId}.response",
                Context = $"Agent {response.AgentId} produced response (confidence {response.Confidence:F2})",
                Score = response.Confidence,
                Timestamp = DateTimeOffset.UtcNow,
                RollbackExpansion = response.Evidence.Count > 0
                    ? $"evidence: [{string.Join("; ", response.Evidence)}]"
                    : null,
                EventType = "agent_response",
                GaMetadata = new GaMetadataDto
                {
                    AgentId = response.AgentId,
                    Confidence = response.Confidence,
                    OriginalQuery = Truncate(state.OriginalMessage, 200),
                },
            });
        }

        return Task.FromResult(HookResult.Continue);
    }

    // ── File I/O (fire-and-forget) ───────────────────────────────────────

    private void EmitTrace(Guid correlationId, TraceArtifactDto artifact)
    {
        _ = Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists(TraceDir)) Directory.CreateDirectory(TraceDir);

                var filename = $"{artifact.Timestamp:yyyyMMddTHHmmss}_{correlationId:N}_{artifact.EventType}.json";
                var path = Path.Combine(TraceDir, filename);
                var json = JsonSerializer.Serialize(artifact, JsonOpts);
                File.WriteAllText(path, json);

                PruneOldTraces();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "TraceBridgeHook: failed to write trace file");
            }
        });
    }

    private static void PruneOldTraces()
    {
        try
        {
            var files = Directory.GetFiles(TraceDir, "*.json");
            if (files.Length <= MaxTraceFiles) return;

            Array.Sort(files); // lexicographic = chronological (timestamp prefix)
            var toDelete = files.Length - MaxTraceFiles;
            for (var i = 0; i < toDelete; i++)
                File.Delete(files[i]);
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    private static string Truncate(string s, int maxLen)
        => s.Length <= maxLen ? s : s[..maxLen] + "...";

    // ── JSON DTOs (match TARS TraceArtifact schema + GA extensions) ─────

    private sealed class TraceArtifactDto
    {
        public required string TaskId { get; init; }
        public required string PatternName { get; init; }
        public required string PatternTemplate { get; init; }
        public required string Context { get; init; }
        public required double Score { get; init; }
        public required DateTimeOffset Timestamp { get; init; }
        public string? RollbackExpansion { get; init; }
        public string Source { get; init; } = "ga-trace-bridge";
        public required string EventType { get; init; }
        public GaMetadataDto? GaMetadata { get; init; }
    }

    private sealed class GaMetadataDto
    {
        public string? AgentId { get; init; }
        public float? Confidence { get; init; }
        public string? RoutingMethod { get; init; }
        public string? SkillName { get; init; }
        public string? OriginalQuery { get; init; }
    }
}
