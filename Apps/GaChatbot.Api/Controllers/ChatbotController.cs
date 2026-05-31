namespace GaChatbot.Api.Controllers;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Trace;
using GaChatbot.Api.Helpers;
using GaChatbot.Api.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/chatbot")]
public sealed class ChatbotController(
    ILogger<ChatbotController> logger,
    ILlmConcurrencyGate concurrencyGate,
    IChatApplicationService chatApplicationService) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpPost("chat/stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task ChatStream([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteSseErrorAsync("Message cannot be empty.", cancellationToken);
            return;
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        await Response.StartAsync(cancellationToken);

        if (!await concurrencyGate.TryEnterAsync(cancellationToken))
        {
            await WriteSseErrorAsync("Service is busy. Please try again in a few seconds.", cancellationToken);
            return;
        }

        try
        {
            var chatRequest = new ChatExecutionRequest(message, ToConversationTurns(request.ConversationHistory));

            await foreach (var update in chatApplicationService.ChatStreamAsync(chatRequest, cancellationToken))
            {
                if (update.Routing is not null)
                {
                    var routingPayload = JsonSerializer.Serialize(new
                    {
                        type = "routing",
                        agentId = update.Routing.AgentId,
                        confidence = update.Routing.Confidence,
                        routingMethod = update.Routing.RoutingMethod,
                        trace = update.Trace,
                        grounding = update.Grounding is null
                            ? null
                            : new
                            {
                                source = update.Grounding.Source,
                                revision = update.Grounding.Revision,
                                queryType = update.Grounding.QueryType
                            }
                    }, JsonOptions);

                    await WriteSseLineAsync(routingPayload, cancellationToken);
                }

                if (!string.IsNullOrWhiteSpace(update.Chunk))
                {
                    await WriteSseLineAsync(update.Chunk, cancellationToken);
                }

                if (update.IsCompleted)
                {
                    await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Chat stream cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error streaming chatbot response");
            await WriteSseErrorAsync("Failed to process message. Please try again.", cancellationToken);
        }
        finally
        {
            concurrencyGate.Release();
        }
    }

    [HttpPost("chat")]
    [ProducesResponseType(typeof(ChatJsonResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ChatJsonResponse>> Chat(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return BadRequest(new { error = "Message cannot be empty." });
        }

        if (!await concurrencyGate.TryEnterAsync(cancellationToken))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Service is busy. Please try again in a few seconds." });
        }

        try
        {
            var sw = Stopwatch.StartNew();
            var response = await chatApplicationService.ChatAsync(
                new ChatExecutionRequest(message, ToConversationTurns(request.ConversationHistory)),
                cancellationToken);
            sw.Stop();

            return Ok(new ChatJsonResponse(
                response.NaturalLanguageAnswer,
                response.Routing.AgentId,
                response.Routing.Confidence,
                response.Routing.RoutingMethod,
                response.Grounding,
                sw.ElapsedMilliseconds,
                Activity.Current?.TraceId.ToString(),
                response.Trace));
        }
        finally
        {
            concurrencyGate.Release();
        }
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(ChatbotStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatbotStatus>> GetStatus(CancellationToken cancellationToken)
    {
        var status = await chatApplicationService.GetStatusAsync(cancellationToken);
        return Ok(status);
    }

    [HttpGet("examples")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public ActionResult<List<string>> GetExamples() =>
        Ok((List<string>)
        [
            "Show me some easy beginner chords",
            "Explain voice leading in jazz",
            "What are the modes of the major scale?",
            "Generate a mellow ii-V-I in C",
            "How do I make this progression sound darker?"
        ]);

    [HttpGet("demo")]
    [ProducesResponseType(typeof(ChatbotDemoScript), StatusCodes.Status200OK)]
    public ActionResult<ChatbotDemoScript> GetDemo() =>
        Ok(new ChatbotDemoScript(
            Version: "1.1",
            Categories:
            [
                new ChatbotDemoCategory(
                    Id: "theory",
                    Name: "Music Theory",
                    Icon: "music_note",
                    Description: "Foundational questions about modes, keys, and the circle of fifths.",
                    Prompts:
                    [
                        new("Explain the circle of fifths", "Key signatures, perfect-fifth relationships, and enharmonics."),
                        new("What are the modes of the major scale", "Ionian through Locrian with formulas and character."),
                        new("What is the difference between major and minor", "Quality contrast with audible examples.")
                    ]),
                new ChatbotDemoCategory(
                    Id: "scales-keys",
                    Name: "Scales & Keys",
                    Icon: "queue_music",
                    Description: "Notes, relative keys, and the diatonic chords of any major key.",
                    Prompts:
                    [
                        new("Show me the notes in C major", "Seven scale notes plus the relative minor."),
                        new("What is the relative minor of G major", "Relative-key pairing with shared key signature."),
                        new("What are the diatonic chords in G major", "Seven diatonic chords with Roman-numeral quality.")
                    ]),
                new ChatbotDemoCategory(
                    Id: "progressions",
                    Name: "Progressions & Substitution",
                    Icon: "timeline",
                    Description: "Identify the key of a progression and reharmonize with substitutions.",
                    Prompts:
                    [
                        new("Identify the key of Am F C G", "Key detection from a four-chord progression."),
                        new("Suggest substitutions for G7 in a ii-V-I", "Harmonic substitutions ranked by ICV distance."),
                        new("Substitutions for C major", "Relative-minor and parallel options with voice-leading cost.")
                    ]),
                new ChatbotDemoCategory(
                    Id: "operations",
                    Name: "Chord Operations",
                    Icon: "code",
                    Description: "Transposition and set-theory analysis on chords.",
                    Prompts:
                    [
                        new("Transpose C E G to D", "Interval calculation plus resulting chord."),
                        new("What are the common tones between Cmaj7 and Am7", "Pitch-class intersection with role-per-chord.")
                    ]),
                new ChatbotDemoCategory(
                    Id: "getting-started",
                    Name: "Getting Started",
                    Icon: "guitar",
                    Description: "Practical guitar starting points for new players.",
                    Prompts:
                    [
                        new("Show me some easy beginner chords", "Open-position chords with frettings in low-to-high notation.")
                    ])
            ]));

    // ── QA summary for showcase prompts ──────────────────────────────────────
    // Surfaces per-prompt validation signal in the chatbot UI: which prompts
    // have a recorded golden trace, their median response time, and which
    // skill agent handled them. Data source: state/quality/chatbot-qa/
    // golden-traces/<slug>/{_meta.json, run-*.json}.
    //
    // Fallback: when _meta.json is absent (fresh checkouts + CI — the local
    // recorder script is the only producer and its output is gitignored),
    // synthesize the summary from _canonical.json, which IS committed to
    // git as the curated baseline. Local recorder workflows that produce
    // _meta.json still win — the canonical fallback only fills the gap.
    //
    // Cached for 60s to avoid disk scans on every chatbot page load. Reads
    // are best-effort: malformed JSON or missing fields silently drop that
    // entry rather than returning an error.

    private static readonly object QaSummaryLock = new();
    private static Dictionary<string, ChatbotQaSummary>? _qaSummaryCache;
    private static DateTimeOffset _qaSummaryCachedAt = DateTimeOffset.MinValue;
    private static readonly TimeSpan QaSummaryCacheTtl = TimeSpan.FromSeconds(60);

    [HttpGet("qa-summary")]
    [ProducesResponseType(typeof(Dictionary<string, ChatbotQaSummary>), StatusCodes.Status200OK)]
    public ActionResult<Dictionary<string, ChatbotQaSummary>> GetQaSummary()
    {
        lock (QaSummaryLock)
        {
            if (_qaSummaryCache is not null && DateTimeOffset.UtcNow - _qaSummaryCachedAt < QaSummaryCacheTtl)
            {
                return Ok(_qaSummaryCache);
            }
            _qaSummaryCache = BuildQaSummary();
            _qaSummaryCachedAt = DateTimeOffset.UtcNow;
            return Ok(_qaSummaryCache);
        }
    }

    private static Dictionary<string, ChatbotQaSummary> BuildQaSummary()
    {
        var result = new Dictionary<string, ChatbotQaSummary>(StringComparer.OrdinalIgnoreCase);
        var repoRoot = FindRepoRoot();
        if (repoRoot is null) return result;

        var goldenDir = Path.Combine(repoRoot, "state", "quality", "chatbot-qa", "golden-traces");
        if (!Directory.Exists(goldenDir)) return result;

        // Aggregate warning prompts from the latest last.json so we can mark
        // entries that are validated-but-slow (e.g. modes prompt that
        // exceeded soft budget).
        var warningPrompts = LoadWarningPrompts(Path.Combine(repoRoot, "state", "quality", "chatbot-qa", "last.json"));

        foreach (var promptDir in Directory.EnumerateDirectories(goldenDir))
        {
            var metaPath = Path.Combine(promptDir, "_meta.json");
            var canonicalPath = Path.Combine(promptDir, "_canonical.json");

            ChatbotQaMeta? meta = null;
            if (System.IO.File.Exists(metaPath))
            {
                try { meta = JsonSerializer.Deserialize<ChatbotQaMeta>(System.IO.File.ReadAllText(metaPath), JsonOptions); }
                catch { meta = null; }
            }

            // Fallback to canonical.json (the committed baseline) when no
            // local _meta.json is present. Canonical fields cover
            // promptId/prompt/category/extractedAt; runCount comes from how
            // many run-*.json files contributed to the baseline.
            ChatbotCanonicalFallback? canonical = null;
            if (meta is null && System.IO.File.Exists(canonicalPath))
            {
                try { canonical = JsonSerializer.Deserialize<ChatbotCanonicalFallback>(System.IO.File.ReadAllText(canonicalPath), JsonOptions); }
                catch { canonical = null; }
            }

            if (meta is null && canonical is null) continue;

            // When falling back to canonical, synthesize a meta shape so the
            // rest of the loop reads identically.
            if (meta is null && canonical is not null)
            {
                if (string.IsNullOrWhiteSpace(canonical.Prompt)) continue;
                meta = new ChatbotQaMeta(
                    PromptId: canonical.PromptId,
                    Prompt: canonical.Prompt,
                    Category: canonical.Category,
                    FirstRecordedAt: canonical.ExtractedAt);
            }
            if (meta is null || string.IsNullOrWhiteSpace(meta.Prompt)) continue;

            var elapsedMsSamples = new List<double>();
            string? lastAgent = null;
            double? lastConfidence = null;
            DateTimeOffset? lastRunAt = null;

            foreach (var runPath in Directory.EnumerateFiles(promptDir, "run-*.json"))
            {
                ChatbotQaRun? run;
                try { run = JsonSerializer.Deserialize<ChatbotQaRun>(System.IO.File.ReadAllText(runPath), JsonOptions); }
                catch { continue; }
                if (run?.Response is null) continue;

                if (run.Response.ElapsedMs is { } ms) elapsedMsSamples.Add(ms);
                if (!string.IsNullOrWhiteSpace(run.Response.AgentId)) lastAgent = run.Response.AgentId;
                if (run.Response.Confidence is { } c) lastConfidence = c;
                if (run.RecordedAt is { } recordedAt && (lastRunAt is null || recordedAt > lastRunAt))
                {
                    lastRunAt = recordedAt;
                }
            }

            double? median = null;
            if (elapsedMsSamples.Count > 0)
            {
                elapsedMsSamples.Sort();
                median = elapsedMsSamples[elapsedMsSamples.Count / 2];
            }

            // If no run-*.json files contributed (fresh checkout / canonical
            // fallback path), derive runCount + agentId from the canonical
            // baseline so the UI still renders "QA verified" instead of "○
            // recorded, not yet measured." MedianElapsedMs stays null —
            // canonical doesn't capture timing.
            var effectiveRunCount = elapsedMsSamples.Count;
            if (effectiveRunCount == 0 && canonical is not null)
            {
                effectiveRunCount = canonical.RunCount;
                lastAgent ??= ExtractAgentIdFromCanonicalSteps(canonical.CanonicalSteps);
            }

            var summary = new ChatbotQaSummary(
                PromptId: meta.PromptId ?? Path.GetFileName(promptDir),
                Prompt: meta.Prompt,
                Category: meta.Category,
                LastValidated: lastRunAt ?? meta.FirstRecordedAt,
                RunCount: effectiveRunCount,
                MedianElapsedMs: median,
                AgentId: lastAgent,
                Confidence: lastConfidence,
                HasWarning: warningPrompts.Contains(meta.Prompt));

            // Index by normalized prompt text so the frontend can do a direct
            // case-insensitive lookup on the showcase prompt label.
            result[NormalizePromptKey(meta.Prompt)] = summary;
        }
        return result;
    }

    // Walks canonicalSteps[*].invariantAttributes looking for an "agent.id"
    // (orchestration.answer / orchestration.route / agent.semantic_result
    // steps carry it). Returns the first match — the canonical pipeline
    // produces one agent per prompt.
    private static string? ExtractAgentIdFromCanonicalSteps(List<ChatbotCanonicalStep>? steps)
    {
        if (steps is null) return null;
        foreach (var step in steps)
        {
            if (step.InvariantAttributes is null) continue;
            if (step.InvariantAttributes.TryGetValue("agent.id", out var idElement)
                && idElement.ValueKind == JsonValueKind.String)
            {
                var id = idElement.GetString();
                if (!string.IsNullOrWhiteSpace(id)) return id;
            }
        }
        return null;
    }

    private static string NormalizePromptKey(string prompt) =>
        prompt.Trim().TrimEnd('.', '?', '!');

    private static HashSet<string> LoadWarningPrompts(string lastJsonPath)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!System.IO.File.Exists(lastJsonPath)) return set;
        try
        {
            using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(lastJsonPath));
            if (!doc.RootElement.TryGetProperty("warnings", out var warnings)) return set;
            foreach (var w in warnings.EnumerateArray())
            {
                var text = w.GetString();
                if (string.IsNullOrWhiteSpace(text)) continue;
                // Warning format: "[category] 'prompt text' → 40716ms exceeded soft budget 30000ms"
                var firstQuote = text.IndexOf('\'');
                if (firstQuote < 0) continue;
                var lastQuote = text.LastIndexOf('\'');
                if (lastQuote <= firstQuote) continue;
                var prompt = text.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                set.Add(NormalizePromptKey(prompt));
            }
        }
        catch { /* ignore malformed last.json */ }
        return set;
    }

    private static string? _cachedRepoRoot;
    private static string? FindRepoRoot()
    {
        if (_cachedRepoRoot is not null) return _cachedRepoRoot;
        // Walk up from ContentRootPath looking for .git. Accepts both a .git
        // directory (normal checkout) and a .git file (git worktrees — see
        // `git worktree add`, which writes a one-line "gitdir: ..." pointer
        // file at the worktree root). Falls back to Environment.CurrentDirectory
        // which is where `dotnet run` was invoked (typically the repo root
        // per the runbook).
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var gitPath = Path.Combine(dir.FullName, ".git");
                if (Directory.Exists(gitPath) || System.IO.File.Exists(gitPath))
                {
                    _cachedRepoRoot = dir.FullName;
                    return _cachedRepoRoot;
                }
                dir = dir.Parent;
            }
        }
        return null;
    }

    private async Task WriteSseLineAsync(string data, CancellationToken cancellationToken)
    {
        // Per the SSE spec, each line of an event body must be prefixed with
        // `data: ` and the event is terminated by a single blank line. If the
        // payload itself contains '\n' (markdown tables, multi-paragraph
        // prose, code blocks), naively emitting `data: <multi-line>\n\n`
        // makes everything after the first embedded '\n' look like
        // unprefixed lines to the parser — they get silently dropped, and
        // the first embedded '\n\n' terminates the event prematurely.
        //
        // Symptom: chatbot responses with markdown tables rendered only
        // their leading paragraph in the UI even though `/chat` returned
        // the full text. Caught 2026-05-13 via chrome-devtools click-test
        // on "Diatonic chords in G major".
        //
        // Strip '\r' first so CRLF-on-Windows doesn't double-terminate.
        var normalized = data.Replace("\r", string.Empty);
        foreach (var line in normalized.Split('\n'))
        {
            await Response.WriteAsync("data: ", cancellationToken);
            await Response.WriteAsync(line, cancellationToken);
            await Response.WriteAsync("\n", cancellationToken);
        }
        await Response.WriteAsync("\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private Task WriteSseErrorAsync(string message, CancellationToken cancellationToken) =>
        WriteSseLineAsync(JsonSerializer.Serialize(new { error = message }, JsonOptions), cancellationToken);

    private static List<ConversationTurn>? ToConversationTurns(List<ChatMessage>? history) =>
        history?
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => new ConversationTurn(m.Role, m.Content, DateTimeOffset.UtcNow))
            .ToList();
}

// ── DTOs for /api/chatbot/qa-summary ────────────────────────────────────────
public sealed record ChatbotQaSummary(
    string PromptId,
    string Prompt,
    string? Category,
    DateTimeOffset? LastValidated,
    int RunCount,
    double? MedianElapsedMs,
    string? AgentId,
    double? Confidence,
    bool HasWarning);

internal sealed record ChatbotQaMeta(
    string? PromptId,
    string Prompt,
    string? Category,
    DateTimeOffset? FirstRecordedAt);

internal sealed record ChatbotQaRun(
    DateTimeOffset? RecordedAt,
    ChatbotQaResponse? Response);

internal sealed record ChatbotQaResponse(
    string? AgentId,
    double? Confidence,
    double? ElapsedMs);

// Minimal projection of _canonical.json — only the fields BuildQaSummary
// needs when _meta.json is absent. Canonical files always carry promptId,
// prompt, category, runCount, extractedAt; canonicalSteps is mined for
// agent.id. Schema source: Scripts/record-golden-traces.ps1.
internal sealed record ChatbotCanonicalFallback(
    string? PromptId,
    string? Prompt,
    string? Category,
    int RunCount,
    DateTimeOffset? ExtractedAt,
    List<ChatbotCanonicalStep>? CanonicalSteps);

internal sealed record ChatbotCanonicalStep(
    string? Name,
    string? Status,
    Dictionary<string, JsonElement>? InvariantAttributes);

public sealed class ChatRequest
{
    /// <summary>
    /// The user's current message. Capped at 2 KB so a single request
    /// can't ship a megabyte of prompt-injection or DoS payload to the
    /// local LLM.
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Prior conversation turns. Capped at 50 turns total per request
    /// AND at 2 KB per turn content so the public chatbot endpoint
    /// (demos.guitaralchemist.com/chatbot) can't be used as a free
    /// LLM-resource-exhaustion proxy. Without these caps an unauthenticated
    /// attacker could ship 28 MB (Kestrel default body limit) of context
    /// to local Ollama, parking it behind LlmConcurrencyGate and starving
    /// real users — exactly the abuse vector flagged in PR #111 review.
    /// </summary>
    [MaxLength(MaxConversationHistoryTurns)]
    public List<ChatMessage>? ConversationHistory { get; set; }

    public bool UseSemanticSearch { get; set; } = true;

    public const int MaxConversationHistoryTurns = 50;
    public const int MaxConversationTurnContentChars = 2000;
}

public sealed record ChatJsonResponse(
    string NaturalLanguageAnswer,
    string AgentId,
    float Confidence,
    string RoutingMethod,
    GroundingMetadata? Grounding = null,
    long ElapsedMs = 0,
    string? TraceId = null,
    AgenticTrace? Trace = null);

public sealed class ChatMessage
{
    [MaxLength(32)]
    public string Role { get; set; } = string.Empty;

    [MaxLength(ChatRequest.MaxConversationTurnContentChars)]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Readiness probe response. <see cref="IsAvailable"/> requires the configured
/// chat AND embedding models to be installed in the provider — was previously
/// just "HTTP reachable", which silently reported healthy while live API was
/// wedged. The new checks distinguish "Ollama process up" (transport-level)
/// from "chatbot can actually serve a request" (path-level).
/// </summary>
public sealed class ChatbotStatus
{
    /// <summary>
    /// True iff the chatbot can serve a request: provider HTTP reachable AND
    /// configured chat model installed AND configured embedding model installed.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>Human-readable summary; surface this to operators directly.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Provider name (<c>"ollama"</c>, <c>"github"</c>, <c>"docker"</c>).</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Configured chat model name (e.g. <c>"llama3.2:3b"</c>).</summary>
    public string? ChatModel { get; set; }

    /// <summary>True iff the chat model is installed in the provider.</summary>
    public bool? ChatModelInstalled { get; set; }

    /// <summary>Configured embedding model name (e.g. <c>"nomic-embed-text"</c>).</summary>
    public string? EmbeddingModel { get; set; }

    /// <summary>True iff the embedding model is installed in the provider.</summary>
    public bool? EmbeddingModelInstalled { get; set; }

    /// <summary>
    /// True iff the provider HTTP endpoint responds. Distinct from
    /// <see cref="IsAvailable"/>: we may be HTTP-reachable but missing models.
    /// </summary>
    public bool ProviderReachable { get; set; }

    /// <summary>
    /// True iff a synthetic catalog-skill query round-trips through the
    /// orchestrator within the probe's timeout. Catches process-level
    /// wedges (DI registry empty, hosted services deadlocked, orchestrator
    /// hung) that the provider-level probe cannot see. Independent of
    /// <see cref="ProviderReachable"/>: catalog skills don't need Ollama.
    /// </summary>
    public bool? OrchestratorRoundTripOk { get; set; }

    public DateTime Timestamp { get; set; }
}
