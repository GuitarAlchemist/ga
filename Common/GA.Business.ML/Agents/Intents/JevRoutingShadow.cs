namespace GA.Business.ML.Agents.Intents;

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// SHADOW classifier: asks TypeSafe Jev (System One, model pinned to
/// <see cref="Model"/>) which intent should handle each scored query, and logs
/// its pick next to production's — WITHOUT ever changing the routing result.
/// <para>
/// Default OFF. Active only when <c>GA_ROUTER_JEV_SHADOW=1</c> AND
/// <c>TYPESAFE_API_KEY</c> is set. <b>Enabling it sends the user's message to
/// api.typesafe.ai</b>, so turning it on is an operator decision.
/// </para>
/// <para>
/// <see cref="Observe"/> snapshots the intents and returns immediately; the call
/// runs in the background with a hard timeout, at most <see cref="MaxInFlight"/>
/// at a time, never retries, and stops for the rest of the process after a 401,
/// 403 or 429. Every outcome (answer, timeout, HTTP error,
/// contract failure, skip) is one JSONL row under
/// <c>{repo-root}/state/telemetry/routing-jev-shadow/</c> (override:
/// <c>GA_ROUTER_JEV_SHADOW_DIR</c>). The API key is never logged.
/// </para>
/// <para>
/// Spend is capped by <c>GA_ROUTER_JEV_SHADOW_BUDGET_USD</c> (default
/// <see cref="DefaultBudgetUsd"/>), cumulative over every row already in the log
/// directory, so a restart does not reset it. A call whose usage is unknown
/// (timeout, network error) is charged a conservative estimate. Requests tagged as
/// the repo's own eval or probe traffic
/// (<see cref="RoutingTelemetryLog.IsSyntheticTrafficSource"/>: <c>theory-qa</c>,
/// <c>probe-*</c>) are never sent: they log a <c>skipped_source</c> row and cost
/// nothing, so a QA run cannot spend the budget meant for real traffic.
/// </para>
/// <para>
/// The question is the one ix Stage 3 measured (ix
/// <c>state/router-spike/jev/options-full.json</c>): the instructions and the
/// <c>__none__</c> criterion are copied verbatim, and the other options are the
/// live intents' <see cref="IIntent.Description"/>s.
/// </para>
/// </summary>
public sealed class JevRoutingShadow
{
    public const string Model = "jev-1.13.0";
    public const double DefaultBudgetUsd = 0.05;
    public const int MaxInFlight = 2;
    internal const string NoneOption = "__none__";
    internal const string QuestionId = "intent";

    internal const string Instructions =
        "Which assistant skill should handle this user message? Choose __none__ if the message is not a request that one of the listed guitar and music-theory skills can answer.";

    internal const string NoneCriterion =
        "None of these skills: the message is off-topic, not about guitar or music theory, or asks for something no listed skill does.";

    /// <summary>Reviewed rate card (docs.typesafe.ai/models, 2026-09-22): USD per 1M input tokens.</summary>
    internal const double InputPricePerMillionUsd = 0.042;

    /// <summary>Estimate for a call whose usage is unknown: one token per body byte,
    /// about twice the worst ratio ix measured (0.49 tokens/byte in Stage 1).</summary>
    internal const double EstimatedTokensPerByte = 1.0;

    /// <summary>Allowed |sum(probabilities) - 1|. Jev rounds to 2 decimals; the
    /// tolerance ix registered for 33 options in Stage 3.</summary>
    internal const double SumTolerance = 0.03;

    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2.5);
    private static readonly Uri Endpoint = new("https://api.typesafe.ai/v1/systemone");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static readonly Lazy<JevRoutingShadow?> LazyInstance = new(TryCreate);

    /// <summary>The shadow classifier, or <c>null</c> when disabled.</summary>
    public static JevRoutingShadow? Instance =>
        Environment.GetEnvironmentVariable("GA_ROUTER_JEV_SHADOW") == "1" ? LazyInstance.Value : null;

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _logDirectory;
    private readonly double _budgetUsd;
    private readonly TimeSpan _timeout;
    private readonly SemaphoreSlim _inFlight = new(MaxInFlight, MaxInFlight);
    private readonly Lock _gate = new();
    private double _spentUsd;
    private volatile string? _stoppedOn;

    internal JevRoutingShadow(HttpClient http, string apiKey, string logDirectory, double budgetUsd, TimeSpan timeout)
    {
        _http = http;
        _apiKey = apiKey;
        _logDirectory = logDirectory;
        _budgetUsd = budgetUsd;
        _timeout = timeout;
        _spentUsd = LoggedSpendUsd(logDirectory);
    }

    /// <summary>Spend already recorded in the log directory (sum of <c>cost_usd</c>).</summary>
    internal double SpentUsd
    {
        get { lock (_gate) return _spentUsd; }
    }

    private static JevRoutingShadow? TryCreate()
    {
        try
        {
            var key = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY");
            if (string.IsNullOrWhiteSpace(key)) return null;

            var budget = double.TryParse(
                Environment.GetEnvironmentVariable("GA_ROUTER_JEV_SHADOW_BUDGET_USD"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var b) && b >= 0 ? b : DefaultBudgetUsd;

            // No redirects: the bearer token must only ever reach the pinned endpoint.
            var http = new HttpClient(new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            })
            {
                Timeout = Timeout.InfiniteTimeSpan,
                MaxResponseContentBufferSize = 1 << 20,
            };
            return new JevRoutingShadow(http, key, ResolveDirectory(), budget, DefaultTimeout);
        }
        catch
        {
            return null;
        }
    }

    internal static string ResolveDirectory()
    {
        var env = Environment.GetEnvironmentVariable("GA_ROUTER_JEV_SHADOW_DIR");
        if (!string.IsNullOrWhiteSpace(env)) return env;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "state")))
                return Path.Combine(dir.FullName, "state", "telemetry", "routing-jev-shadow");
            dir = dir.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "state", "telemetry", "routing-jev-shadow");
    }

    /// <summary>
    /// Fire-and-forget: classify <paramref name="query"/> with Jev in the
    /// background and log the outcome next to production's pick
    /// (<paramref name="prodChosen"/> is <c>null</c> when production fell through).
    /// Returns immediately and never throws.
    /// </summary>
    public void Observe(string query, IReadOnlyList<IIntent> intents, string? prodChosen, double prodConfidence, double? margin)
    {
        try
        {
            // Snapshot now: intents may be scoped to the request, which ends before the call does;
            // so may the request's traffic-source tag.
            var criteria = Criteria(intents);
            var trafficSource = RoutingTelemetryLog.CurrentTrafficSource;
            _ = Task.Run(() => ObserveAsync(query, criteria, prodChosen, prodConfidence, margin, trafficSource));
        }
        catch
        {
            // shadow must never affect routing
        }
    }

    internal static SortedDictionary<string, string> Criteria(IReadOnlyList<IIntent> intents)
    {
        var criteria = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var intent in intents) criteria[intent.Id] = intent.Description;
        criteria[NoneOption] = NoneCriterion;
        return criteria;
    }

    internal async Task<JevShadowRecord> ObserveAsync(
        string query,
        SortedDictionary<string, string> criteria,
        string? prodChosen,
        double prodConfidence,
        double? margin,
        string? trafficSource = null)
    {
        var record = new JevShadowRecord
        {
            Timestamp = DateTime.UtcNow.ToString("o"),
            Query = query,
            ProdChosen = prodChosen,
            ProdConfidence = prodConfidence,
            Margin = margin,
            Status = "error",
        };
        try
        {
            record = RoutingTelemetryLog.IsSyntheticTrafficSource(trafficSource)
                ? record with { Status = "skipped_source", Detail = trafficSource }
                : await ClassifyAsync(record, criteria);
        }
        catch (Exception ex)
        {
            record = record with { Status = "error", Detail = ex.GetType().Name };
        }

        Append(record);
        return record;
    }

    private async Task<JevShadowRecord> ClassifyAsync(JevShadowRecord record, SortedDictionary<string, string> criteria)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["model"] = Model,
            ["state"] = new Dictionary<string, string> { ["user_message"] = record.Query },
            ["questions"] = new Dictionary<string, object>
            {
                [QuestionId] = new Dictionary<string, object>
                {
                    ["type"] = "choice",
                    ["instructions"] = Instructions,
                    ["criteria"] = criteria,
                },
            },
        });
        record = record with { OptionsSha256 = OptionsSha256(criteria) };

        if (_stoppedOn is { } stoppedOn) return record with { Status = "skipped_stopped", Detail = stoppedOn };
        if (!_inFlight.Wait(0)) return record with { Status = "skipped_busy" };
        try
        {
            // Reserve the estimate before calling, so concurrent calls cannot overshoot the cap.
            var estimate = body.Length * EstimatedTokensPerByte / 1e6 * InputPricePerMillionUsd;
            lock (_gate)
            {
                if (_spentUsd + estimate > _budgetUsd) return record with { Status = "skipped_budget" };
                _spentUsd += estimate;
            }

            var (result, cost) = await SendAsync(record, body, criteria.Keys.ToHashSet(StringComparer.Ordinal));
            var charged = cost ?? estimate;
            lock (_gate) _spentUsd += charged - estimate;
            return result with { CostUsd = charged, CostKnown = cost is not null };
        }
        finally
        {
            _inFlight.Release();
        }
    }

    private async Task<(JevShadowRecord Record, double? CostUsd)> SendAsync(
        JevShadowRecord record, byte[] body, HashSet<string> options)
    {
        var sw = Stopwatch.StartNew();
        using var cts = new CancellationTokenSource(_timeout);
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new ByteArrayContent(body),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        string text;
        try
        {
            using var response = await _http.SendAsync(request, cts.Token);
            text = await response.Content.ReadAsStringAsync(cts.Token);
            record = record with { LatencyMs = sw.Elapsed.TotalMilliseconds };
            if (!response.IsSuccessStatusCode)
            {
                var status = $"http_{(int)response.StatusCode}";
                // After a bad or revoked key or a rate limit, calling again only adds
                // load: stop for the rest of the process, like ix's live runner.
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    or HttpStatusCode.TooManyRequests)
                    _stoppedOn = status;
                return (record with { Status = status }, null);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return (record with { Status = "timeout", LatencyMs = sw.Elapsed.TotalMilliseconds }, null);
        }
        catch (Exception ex)
        {
            return (record with { Status = "error", Detail = ex.GetType().Name, LatencyMs = sw.Elapsed.TotalMilliseconds }, null);
        }

        Parsed parsed;
        try
        {
            using var doc = JsonDocument.Parse(text);
            parsed = Parse(doc.RootElement, options);
        }
        catch (JsonException)
        {
            return (record with { Status = "invalid", Detail = "response is not JSON" }, null);
        }

        // Output tokens are charged at the input rate too, as jevAdvice.ts does: the
        // published rate card prices input only, and a budget cap must not undercount.
        double? cost = parsed.InputTokens is { } t
            ? (t + (parsed.OutputTokens ?? 0)) / 1e6 * InputPricePerMillionUsd
            : null;
        record = record with { InputTokens = parsed.InputTokens, OutputTokens = parsed.OutputTokens };
        if (parsed.Error is not null)
            return (record with { Status = parsed.Status, Detail = parsed.Error }, cost);

        var agree = string.Equals(record.ProdChosen ?? NoneOption, parsed.Choice, StringComparison.Ordinal);
        return (record with
        {
            Status = "ok",
            JevChosen = parsed.Choice,
            JevConfidence = parsed.Confidence,
            Agree = agree,
        }, cost);
    }

    internal sealed record Parsed(
        string Status,
        string? Error,
        string? Choice = null,
        double? Confidence = null,
        long? InputTokens = null,
        long? OutputTokens = null);

    /// <summary>Fail-closed validation of a System One response, mirroring ix
    /// <c>jev_router.rs::validate</c>. Usage is read even when the answer is
    /// rejected, so a billed call is still charged at its reported cost.</summary>
    internal static Parsed Parse(JsonElement root, IReadOnlySet<string> options)
    {
        long? Tokens(string name) =>
            root.TryGetProperty("usage", out var u) && u.ValueKind == JsonValueKind.Object
            && u.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            && v.TryGetInt64(out var n) && n >= 0 ? n : null;
        var input = Tokens("input_tokens");
        var output = Tokens("output_tokens");
        Parsed Invalid(string why) => new("invalid", why, InputTokens: input, OutputTokens: output);

        if (root.ValueKind != JsonValueKind.Object) return Invalid("response is not an object");
        if (!root.TryGetProperty("model", out var model) || model.ValueKind != JsonValueKind.String)
            return Invalid("model missing");
        if (model.GetString() != Model)
            return new("wrong_model", model.GetString(), InputTokens: input, OutputTokens: output);

        if (!root.TryGetProperty("answers", out var answers) || answers.ValueKind != JsonValueKind.Object)
            return Invalid("answers missing");
        if (answers.EnumerateObject().Count() != 1 || !answers.TryGetProperty(QuestionId, out var a))
            return Invalid("answers must contain exactly the intent question");
        if (a.ValueKind != JsonValueKind.Object
            || !a.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String
            || type.GetString() != "choice")
            return Invalid("intent is not a choice answer");
        if (!a.TryGetProperty("choice", out var choiceEl) || choiceEl.ValueKind != JsonValueKind.String)
            return Invalid("choice missing");
        var choice = choiceEl.GetString()!;
        if (!options.Contains(choice)) return Invalid("choice is not one of the requested options");

        if (!a.TryGetProperty("probabilities", out var probs) || probs.ValueKind != JsonValueKind.Object)
            return Invalid("probabilities missing");
        var p = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var prop in probs.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Number || !p.TryAdd(prop.Name, prop.Value.GetDouble()))
                return Invalid("probabilities must be one number per option");
        }
        if (!p.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(options))
            return Invalid("probability keys must exactly match the requested options");
        if (p.Values.Any(x => !double.IsFinite(x) || x is < 0 or > 1))
            return Invalid("probability outside [0,1]");
        if (Math.Abs(p.Values.Sum() - 1.0) > SumTolerance) return Invalid("probabilities must sum to 1");
        if (p[choice] < p.Values.Max()) return Invalid("choice must carry the maximum probability");

        if (!a.TryGetProperty("confidence", out var conf) || conf.ValueKind != JsonValueKind.Number
            || conf.GetDouble() is var c && (!double.IsFinite(c) || c is < 0 or > 1))
            return Invalid("confidence missing or outside [0,1]");
        // Missing usage is a contract failure, never an implicit zero cost.
        if (input is null || output is null) return Invalid("usage token count missing");

        return new("ok", null, choice, conf.GetDouble(), input, output);
    }

    internal static string OptionsSha256(SortedDictionary<string, string> criteria)
    {
        var canonical = JsonSerializer.SerializeToUtf8Bytes(new { instructions = Instructions, criteria });
        return Convert.ToHexStringLower(SHA256.HashData(canonical));
    }

    private void Append(JevShadowRecord record)
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);
            var path = Path.Combine(_logDirectory, $"{DateTime.UtcNow:yyyy-MM-dd}.jsonl");
            var json = JsonSerializer.Serialize(record, JsonOpts);
            lock (_gate)
            {
                File.AppendAllText(path, json + "\n");
            }
        }
        catch
        {
            // shadow logging failure must be invisible to the routing caller
        }
    }

    private static double LoggedSpendUsd(string directory)
    {
        double total = 0;
        try
        {
            if (!Directory.Exists(directory)) return 0;
            foreach (var file in Directory.EnumerateFiles(directory, "*.jsonl"))
            foreach (var line in File.ReadLines(file))
            {
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    if (doc.RootElement.TryGetProperty("cost_usd", out var c) && c.ValueKind == JsonValueKind.Number)
                        total += c.GetDouble();
                }
                catch (JsonException)
                {
                    // a torn line costs nothing we can read
                }
            }
        }
        catch
        {
            // unreadable log: fall back to what we could sum
        }

        return total;
    }
}

/// <summary>One Jev shadow outcome per scored routing decision.</summary>
public sealed record JevShadowRecord
{
    [JsonPropertyName("ts")] public required string Timestamp { get; init; }
    [JsonPropertyName("q")] public required string Query { get; init; }

    /// <summary>Intent the PRODUCTION router chose (null = fell through).</summary>
    [JsonPropertyName("prod")] public string? ProdChosen { get; init; }

    [JsonPropertyName("prod_conf")] public double ProdConfidence { get; init; }
    [JsonPropertyName("margin")] public double? Margin { get; init; }

    /// <summary><c>ok</c>, <c>timeout</c>, <c>http_NNN</c>, <c>invalid</c>,
    /// <c>wrong_model</c>, <c>error</c>, <c>skipped_busy</c>, <c>skipped_budget</c>,
    /// <c>skipped_stopped</c> (after a 401, 403 or 429) or <c>skipped_source</c>
    /// (eval or probe traffic; <see cref="Detail"/> holds the tag).</summary>
    [JsonPropertyName("status")] public required string Status { get; init; }

    /// <summary>Why a response was rejected, the unexpected model id, an exception type name,
    /// or the skipped traffic-source tag.</summary>
    [JsonPropertyName("detail")] public string? Detail { get; init; }

    /// <summary>Jev's choice; <c>__none__</c> means it declined.</summary>
    [JsonPropertyName("jev")] public string? JevChosen { get; init; }

    [JsonPropertyName("jev_conf")] public double? JevConfidence { get; init; }

    /// <summary>True when Jev and production agree; a production fall-through agrees with <c>__none__</c>.</summary>
    [JsonPropertyName("agree")] public bool? Agree { get; init; }

    [JsonPropertyName("latency_ms")] public double? LatencyMs { get; init; }
    [JsonPropertyName("input_tokens")] public long? InputTokens { get; init; }
    [JsonPropertyName("output_tokens")] public long? OutputTokens { get; init; }

    /// <summary>Charged against the budget: the reported input and output tokens at
    /// the input rate, or the estimate when usage is unknown.</summary>
    [JsonPropertyName("cost_usd")] public double? CostUsd { get; init; }

    [JsonPropertyName("cost_known")] public bool? CostKnown { get; init; }

    /// <summary>SHA-256 of the instructions + options sent, so rows from different intent sets are not pooled.</summary>
    [JsonPropertyName("options_sha256")] public string? OptionsSha256 { get; init; }
}
