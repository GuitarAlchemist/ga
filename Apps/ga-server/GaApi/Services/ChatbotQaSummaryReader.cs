namespace GaApi.Services;

using System.Text.Json;
using Path = System.IO.Path;

/// <summary>
///     Builds the per-prompt QA badges that the public chatbot page shows on its showcase
///     (<c>GET /api/chatbot/qa-summary</c>), from the golden traces under
///     <c>state/quality/chatbot-qa/golden-traces/&lt;slug&gt;/</c> in the repository checkout.
/// </summary>
/// <remarks>
///     A prompt directory holds <c>_meta.json</c> and <c>run-*.json</c> files written by
///     <c>Scripts/record-golden-traces.ps1</c>. That output is gitignored, so on a fresh
///     checkout the summary falls back to <c>_canonical.json</c>, the committed baseline.
///     Reads are best effort: a malformed file drops its entry instead of failing the request.
///     The result is cached for 60 s so page loads do not rescan the disk.
///     Ported from GaChatbot.Api, which ADR-0005 retires.
/// </remarks>
public static class ChatbotQaSummaryReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private static readonly Lock CacheLock = new();
    private static Dictionary<string, ChatbotQaSummary>? _cache;
    private static DateTimeOffset _cachedAt = DateTimeOffset.MinValue;
    private static string? _cachedRepoRoot;

    /// <summary>The summary keyed by normalized prompt text, built at most once a minute.</summary>
    public static Dictionary<string, ChatbotQaSummary> GetCached()
    {
        lock (CacheLock)
        {
            if (_cache is not null && DateTimeOffset.UtcNow - _cachedAt < CacheTtl) return _cache;
            _cache = Build(FindRepoRoot());
            _cachedAt = DateTimeOffset.UtcNow;
            return _cache;
        }
    }

    /// <summary>Builds the summary from the golden traces under <paramref name="repoRoot"/>.</summary>
    public static Dictionary<string, ChatbotQaSummary> Build(string? repoRoot)
    {
        var result = new Dictionary<string, ChatbotQaSummary>(StringComparer.OrdinalIgnoreCase);
        if (repoRoot is null) return result;

        var goldenDir = Path.Combine(repoRoot, "state", "quality", "chatbot-qa", "golden-traces");
        if (!Directory.Exists(goldenDir)) return result;

        // Prompts listed as validated-but-slow in the latest run summary.
        var warningPrompts = LoadWarningPrompts(Path.Combine(repoRoot, "state", "quality", "chatbot-qa", "last.json"));

        foreach (var promptDir in Directory.EnumerateDirectories(goldenDir))
        {
            var meta = TryRead<ChatbotQaMeta>(Path.Combine(promptDir, "_meta.json"));
            var canonical = meta is null ? TryRead<ChatbotCanonicalFallback>(Path.Combine(promptDir, "_canonical.json")) : null;

            if (meta is null && canonical is not null && !string.IsNullOrWhiteSpace(canonical.Prompt))
            {
                meta = new ChatbotQaMeta(canonical.PromptId, canonical.Prompt, canonical.Category, canonical.ExtractedAt);
            }
            if (meta is null || string.IsNullOrWhiteSpace(meta.Prompt)) continue;

            var elapsedMsSamples = new List<double>();
            string? lastAgent = null;
            double? lastConfidence = null;
            DateTimeOffset? lastRunAt = null;

            foreach (var runPath in Directory.EnumerateFiles(promptDir, "run-*.json"))
            {
                var run = TryRead<ChatbotQaRun>(runPath);
                if (run?.Response is null) continue;

                if (run.Response.ElapsedMs is { } ms) elapsedMsSamples.Add(ms);
                if (!string.IsNullOrWhiteSpace(run.Response.AgentId)) lastAgent = run.Response.AgentId;
                if (run.Response.Confidence is { } confidence) lastConfidence = confidence;
                if (run.RecordedAt is { } recordedAt && (lastRunAt is null || recordedAt > lastRunAt)) lastRunAt = recordedAt;
            }

            double? median = null;
            if (elapsedMsSamples.Count > 0)
            {
                elapsedMsSamples.Sort();
                median = elapsedMsSamples[elapsedMsSamples.Count / 2];
            }

            // Without run files (the canonical fallback), the run count and agent come from the
            // baseline so the page still shows "QA verified"; it carries no timing.
            var runCount = elapsedMsSamples.Count;
            if (runCount == 0 && canonical is not null)
            {
                runCount = canonical.RunCount;
                lastAgent ??= ExtractAgentId(canonical.CanonicalSteps);
            }

            result[NormalizePromptKey(meta.Prompt)] = new ChatbotQaSummary(
                PromptId: meta.PromptId ?? Path.GetFileName(promptDir),
                Prompt: meta.Prompt,
                Category: meta.Category,
                LastValidated: lastRunAt ?? meta.FirstRecordedAt,
                RunCount: runCount,
                MedianElapsedMs: median,
                AgentId: lastAgent,
                Confidence: lastConfidence,
                HasWarning: warningPrompts.Contains(NormalizePromptKey(meta.Prompt)));
        }
        return result;
    }

    /// <summary>The page looks badges up by prompt text trimmed of trailing <c>.?!</c>.</summary>
    public static string NormalizePromptKey(string prompt) => prompt.Trim().TrimEnd('.', '?', '!');

    private static T? TryRead<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions); }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) { return null; }
    }

    // canonicalSteps[*].invariantAttributes carries "agent.id" on the routing and answer steps;
    // the canonical pipeline produces one agent per prompt.
    private static string? ExtractAgentId(List<ChatbotCanonicalStep>? steps) =>
        steps?
            .Select(step => step.InvariantAttributes is not null
                            && step.InvariantAttributes.TryGetValue("agent.id", out var id)
                            && id.ValueKind == JsonValueKind.String
                ? id.GetString()
                : null)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));

    // Warning format: "[category] 'prompt text' → 40716ms exceeded soft budget 30000ms".
    private static HashSet<string> LoadWarningPrompts(string lastJsonPath)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(lastJsonPath)) return set;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(lastJsonPath));
            if (!doc.RootElement.TryGetProperty("warnings", out var warnings)) return set;
            foreach (var warning in warnings.EnumerateArray())
            {
                var text = warning.GetString();
                if (string.IsNullOrWhiteSpace(text)) continue;
                var firstQuote = text.IndexOf('\'');
                var lastQuote = text.LastIndexOf('\'');
                if (firstQuote < 0 || lastQuote <= firstQuote) continue;
                set.Add(NormalizePromptKey(text.Substring(firstQuote + 1, lastQuote - firstQuote - 1)));
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // A malformed last.json only loses the slow-run warnings.
        }
        return set;
    }

    // The checkout root: the first directory above the working directory or the app's base
    // directory that holds `.git` (a directory, or the pointer file of a git worktree).
    private static string? FindRepoRoot()
    {
        if (_cachedRepoRoot is not null) return _cachedRepoRoot;
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                var gitPath = Path.Combine(dir.FullName, ".git");
                if (Directory.Exists(gitPath) || File.Exists(gitPath)) return _cachedRepoRoot = dir.FullName;
            }
        }
        return null;
    }
}

/// <summary>QA badge for one showcase prompt.</summary>
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

internal sealed record ChatbotQaMeta(string? PromptId, string Prompt, string? Category, DateTimeOffset? FirstRecordedAt);

internal sealed record ChatbotQaRun(DateTimeOffset? RecordedAt, ChatbotQaResponse? Response);

internal sealed record ChatbotQaResponse(string? AgentId, double? Confidence, double? ElapsedMs);

// The fields of _canonical.json the fallback needs; schema source: Scripts/record-golden-traces.ps1.
internal sealed record ChatbotCanonicalFallback(
    string? PromptId,
    string? Prompt,
    string? Category,
    int RunCount,
    DateTimeOffset? ExtractedAt,
    List<ChatbotCanonicalStep>? CanonicalSteps);

internal sealed record ChatbotCanonicalStep(string? Name, string? Status, Dictionary<string, JsonElement>? InvariantAttributes);
