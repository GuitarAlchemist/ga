namespace GaChatbot.Api.Services;

using System.Diagnostics;
using System.Text.Json;
using GA.Business.ML.Agents.Intents;
using GA.Business.ML.Extensions;
using GaChatbot.Api.Controllers;
using Microsoft.Extensions.AI;

public interface IChatProviderReadinessProbe
{
    Task<ChatbotStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Readiness probe that distinguishes "provider reachable" from "chatbot can
/// serve a request". Was previously shallow — just an HTTP GET to /api/tags —
/// which let /status report healthy while live API was wedged because the
/// configured chat / embedding models were not installed. Deepened in PR #96
/// to verify model presence end-to-end via the provider's installed-model list,
/// and again to run one real embedding: a provider can list its models while
/// being unable to load any of them.
/// </summary>
public sealed class ChatProviderReadinessProbe(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    IServiceProvider serviceProvider,
    ILogger<ChatProviderReadinessProbe> logger) : IChatProviderReadinessProbe
{
    /// <summary>
    /// Synthetic query for the orchestrator round-trip probe. Routes to a
    /// catalog skill (no LLM call, no embedding call) so a wedged Ollama
    /// can't false-positive nor false-negative the result.
    /// </summary>
    private const string RoundTripQuery = "show me beginner chords";

    /// <summary>
    /// Hard ceiling on the round-trip probe. /status callers expect a fast
    /// response; a hung orchestrator must surface as a failed probe rather
    /// than block the response. 3s is comfortably above any healthy
    /// catalog-skill execution (<10ms in unit tests) but well under typical
    /// HTTP-client timeouts.
    /// </summary>
    private static readonly TimeSpan RoundTripTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Text embedded by the embedding probe. Its content is irrelevant; the
    /// call only has to make the provider load the model and run it.
    /// </summary>
    private const string EmbeddingProbeText = "readiness probe";

    /// <summary>
    /// How long an embedding probe outcome is reused. /status is public and the
    /// chat page refreshes it after every answer, so a status call must not cost
    /// a model run each time. A recovered provider shows green within this window.
    /// </summary>
    private static readonly TimeSpan EmbeddingProbeCacheTtl = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _embeddingProbeGate = new(1, 1);
    private (DateTime At, bool Success, string Diagnostic)? _lastEmbeddingProbe;

    /// <summary>
    /// Ceiling on one embedding call. A warm call takes well under a second; a
    /// cold model load takes seconds (10 s measured right after an Ollama
    /// reinstall), so the ceiling is sized for a cold load.
    /// </summary>
    public TimeSpan EmbeddingProbeTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public async Task<ChatbotStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var provider = (configuration["AI:ChatProvider"] ?? "ollama").ToLowerInvariant();

        var providerStatus = provider switch
        {
            "github" => TokenBasedStatus("GITHUB_TOKEN", "GitHub Models", provider),
            "ollama" => await GetOllamaStatusAsync(cancellationToken),
            "docker" => await GetDockerStatusAsync(cancellationToken),
            _ => new ChatbotStatus
            {
                IsAvailable = false,
                Provider = provider,
                Message = $"Unsupported chat provider '{provider}'.",
                Timestamp = DateTime.UtcNow,
            },
        };

        // Round-trip probe: independent of the provider check above, so a
        // wedged orchestrator process is caught even when the provider is
        // healthy. Catalog-skill execution doesn't need Ollama, so this
        // probe truly tests the chatbot pipeline without re-testing the
        // upstream dep.
        var roundTrip = await TryRoundTripAsync(cancellationToken);
        providerStatus.OrchestratorRoundTripOk = roundTrip.Success;
        if (!roundTrip.Success)
        {
            providerStatus.IsAvailable = false;
            providerStatus.Message = string.IsNullOrEmpty(providerStatus.Message)
                ? roundTrip.Diagnostic
                : $"{providerStatus.Message} | Round-trip: {roundTrip.Diagnostic}";
        }

        // Embedding probe: /api/tags only proves the provider process answers.
        // On 2026-09-27 an interrupted Ollama update deleted its model runtime;
        // /api/tags stayed 200 while every embed and generate call failed, and
        // /status reported ready. One real embedding catches that.
        var embedding = await TryEmbeddingAsync(cancellationToken);
        providerStatus.EmbeddingRoundTripOk = embedding?.Success;
        if (embedding is { Success: false } failed)
        {
            providerStatus.IsAvailable = false;
            providerStatus.Message = string.IsNullOrEmpty(providerStatus.Message)
                ? failed.Diagnostic
                : $"{providerStatus.Message} | Embedding: {failed.Diagnostic}";
        }

        return providerStatus;
    }

    /// <summary>
    /// Embeds one short text with the generator the semantic router uses
    /// (the <c>routing</c> purpose, else the default generator). Returns
    /// <see langword="null"/> when no embedder is registered: embeddings are
    /// optional in this host, so there is nothing to probe. Outcomes are
    /// cached for <see cref="EmbeddingProbeCacheTtl"/> and concurrent callers
    /// share one call.
    /// </summary>
    private async Task<(bool Success, string Diagnostic)?> TryEmbeddingAsync(CancellationToken outerCt)
    {
        var generator = serviceProvider.GetService<IEmbeddingGeneratorFactory>()?.Create("routing")
            ?? serviceProvider.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
        if (generator is null)
        {
            return null;
        }

        await _embeddingProbeGate.WaitAsync(outerCt);
        try
        {
            if (_lastEmbeddingProbe is { } cached && DateTime.UtcNow - cached.At < EmbeddingProbeCacheTtl)
            {
                return (cached.Success, cached.Diagnostic);
            }

            var result = await RunEmbeddingProbeAsync(generator, outerCt);
            _lastEmbeddingProbe = (DateTime.UtcNow, result.Success, result.Diagnostic);
            return result;
        }
        finally
        {
            _embeddingProbeGate.Release();
        }
    }

    private async Task<(bool Success, string Diagnostic)> RunEmbeddingProbeAsync(
        IEmbeddingGenerator<string, Embedding<float>> generator,
        CancellationToken outerCt)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
            timeoutCts.CancelAfter(EmbeddingProbeTimeout);

            var sw = Stopwatch.StartNew();
            var embeddings = await generator.GenerateAsync([EmbeddingProbeText], cancellationToken: timeoutCts.Token);
            sw.Stop();

            var dimensions = embeddings.Count > 0 ? embeddings[0].Vector.Length : 0;
            return dimensions > 0
                ? (true, $"ok, {dimensions} dims in {sw.ElapsedMilliseconds}ms")
                : (false, $"embedding call returned no vector in {sw.ElapsedMilliseconds}ms");
        }
        catch (OperationCanceledException) when (!outerCt.IsCancellationRequested)
        {
            return (false, $"embedding timed out after {EmbeddingProbeTimeout.TotalSeconds}s");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Embedding probe threw");
            return (false, $"embedding threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Issues a synthetic query through a catalog <see cref="IIntent"/> and
    /// confirms the orchestrator pipeline returns a non-empty answer within
    /// <see cref="RoundTripTimeout"/>. Catches process-level wedges (DI
    /// resolution stuck, hosted services deadlocked, intent registry empty)
    /// that the provider-level probe cannot see.
    /// </summary>
    private async Task<(bool Success, string Diagnostic)> TryRoundTripAsync(CancellationToken outerCt)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var intents = scope.ServiceProvider.GetServices<IIntent>().ToList();
            if (intents.Count == 0)
            {
                logger.LogWarning("Round-trip probe: no IIntent registrations resolved");
                return (false, "no IIntent registrations — orchestrator registry empty");
            }

            // Prefer a catalog skill (no LLM, no embedding). Fall back to
            // the first registration if none looks catalog-shaped.
            var probe = intents.FirstOrDefault(IsCatalogIntent) ?? intents[0];

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
            timeoutCts.CancelAfter(RoundTripTimeout);

            var sw = Stopwatch.StartNew();
            var result = await probe.ExecuteAsync(RoundTripQuery, timeoutCts.Token);
            sw.Stop();

            if (string.IsNullOrEmpty(result.Answer))
            {
                return (false, $"intent '{probe.Id}' returned empty answer in {sw.ElapsedMilliseconds}ms");
            }

            logger.LogDebug(
                "Round-trip probe: intent={Intent} elapsed={Elapsed}ms",
                probe.Id, sw.ElapsedMilliseconds);
            return (true, $"ok via {probe.Id} in {sw.ElapsedMilliseconds}ms");
        }
        catch (OperationCanceledException) when (!outerCt.IsCancellationRequested)
        {
            return (false, $"timed out after {RoundTripTimeout.TotalSeconds}s — orchestrator likely wedged");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Round-trip probe threw");
            return (false, $"threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Heuristic for picking a catalog skill over a hybrid one. Catalog
    /// skills don't make LLM/embedding calls, so they round-trip without
    /// touching the upstream provider. Identified by the
    /// <c>OrchestratorSkillIntent</c> ID prefix and a known set of catalog
    /// skill names.
    /// </summary>
    private static bool IsCatalogIntent(IIntent intent)
    {
        var id = intent.Id ?? string.Empty;
        return id.Contains("beginnerchords",   StringComparison.OrdinalIgnoreCase)
            || id.Contains("progressionmood",  StringComparison.OrdinalIgnoreCase)
            || id.Contains("modes",            StringComparison.OrdinalIgnoreCase);
    }

    private static ChatbotStatus TokenBasedStatus(string envVarName, string providerName, string providerKey)
    {
        var configured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envVarName));
        return new ChatbotStatus
        {
            IsAvailable = configured,
            ProviderReachable = configured,
            Provider = providerKey,
            Message = configured
                ? $"{providerName} is configured."
                : $"{providerName} is not configured. Set {envVarName}.",
            Timestamp = DateTime.UtcNow,
        };
    }

    private async Task<ChatbotStatus> GetOllamaStatusAsync(CancellationToken cancellationToken)
    {
        // Mirror OllamaChatService's precedence: `Chatbot:Model` wins over
        // `Ollama:ChatModel` when set. Reading only the latter would
        // green-light a misconfiguration where Chatbot:Model points at a
        // non-installed model. Per PR #96 review.
        var chatModel      = configuration["Chatbot:Model"]
                             ?? configuration["Ollama:ChatModel"];
        var embeddingModel = configuration["Ollama:EmbeddingModel"];
        var status = new ChatbotStatus
        {
            Provider       = "ollama",
            ChatModel      = chatModel,
            EmbeddingModel = embeddingModel,
            Timestamp      = DateTime.UtcNow,
        };

        try
        {
            var client = httpClientFactory.CreateClient("Ollama");
            var response = await client.GetAsync("/api/tags", cancellationToken);
            status.ProviderReachable = response.IsSuccessStatusCode;

            if (!response.IsSuccessStatusCode)
            {
                status.IsAvailable = false;
                status.Message     = $"Ollama returned {(int)response.StatusCode} from /api/tags.";
                return status;
            }

            // Parse the installed-model list. Ollama returns:
            //   { "models": [ { "name": "llama3.2:3b", ... }, ... ] }
            // Names include the version tag; we accept either exact match or
            // a base-name prefix match (so "llama3.2" matches "llama3.2:3b").
            var json    = await response.Content.ReadAsStringAsync(cancellationToken);
            var tagged  = ParseTagsResponse(json);

            status.ChatModelInstalled      = chatModel is null      || ModelInstalled(tagged, chatModel);
            status.EmbeddingModelInstalled = embeddingModel is null || ModelInstalled(tagged, embeddingModel);

            var missing = new List<string>();
            if (status.ChatModelInstalled == false)      missing.Add($"chat='{chatModel}'");
            if (status.EmbeddingModelInstalled == false) missing.Add($"embedding='{embeddingModel}'");

            if (missing.Count == 0)
            {
                status.IsAvailable = true;
                status.Message     = chatModel is null && embeddingModel is null
                    ? "Ollama reachable; no specific models configured."
                    : $"Ollama ready: chat='{chatModel ?? "<unset>"}', embedding='{embeddingModel ?? "<unset>"}'.";
            }
            else
            {
                status.IsAvailable = false;
                status.Message     =
                    $"Ollama reachable but required model(s) missing: {string.Join(", ", missing)}. " +
                    $"Pull with `ollama pull <model>`. Installed: [{string.Join(", ", tagged)}].";
            }

            return status;
        }
        catch (Exception ex)
        {
            status.IsAvailable       = false;
            status.ProviderReachable = false;
            status.Message           = $"Ollama is not reachable: {ex.Message}";
            return status;
        }
    }

    /// <summary>
    /// Parses the model-name list out of an Ollama <c>/api/tags</c> response.
    /// Robust to a missing or malformed body — returns empty rather than
    /// throwing so the caller can still report "no models" cleanly.
    /// </summary>
    private static List<string> ParseTagsResponse(string json)
    {
        // Empty/null body would otherwise throw ArgumentNullException out of
        // JsonDocument.Parse — bubble up to the outer catch and erroneously
        // mark ProviderReachable=false despite the 200. Per PR #96 review.
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("models", out var models)
                || models.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var names = new List<string>();
            foreach (var m in models.EnumerateArray())
            {
                if (m.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                {
                    var name = n.GetString();
                    if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
                }
            }
            return names;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// True iff <paramref name="required"/> matches any installed model. Match
    /// rules (in order): exact match, then base-name match (Ollama returns
    /// versioned tags like <c>llama3.2:3b</c>; users may configure
    /// <c>llama3.2</c> or vice versa, both should resolve).
    /// </summary>
    internal static bool ModelInstalled(IReadOnlyList<string> installed, string required)
    {
        foreach (var name in installed)
        {
            if (string.Equals(name, required, StringComparison.OrdinalIgnoreCase)) return true;

            // Base-name match: "llama3.2" matches "llama3.2:3b" and vice versa.
            var installedBase = name.Split(':', 2)[0];
            var requiredBase  = required.Split(':', 2)[0];
            if (string.Equals(installedBase, requiredBase, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private async Task<ChatbotStatus> GetDockerStatusAsync(CancellationToken cancellationToken)
    {
        var baseUrl = configuration["DockerModelRunner:BaseUrl"] ?? "http://localhost:12434/v1";

        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(10) };
            var response = await client.GetAsync("/models", cancellationToken);
            return new ChatbotStatus
            {
                IsAvailable       = response.IsSuccessStatusCode,
                ProviderReachable = response.IsSuccessStatusCode,
                Provider          = "docker",
                Message           = response.IsSuccessStatusCode
                    ? "Docker Model Runner is reachable."
                    : $"Docker Model Runner returned {(int)response.StatusCode}.",
                Timestamp = DateTime.UtcNow,
            };
        }
        catch (Exception ex)
        {
            return new ChatbotStatus
            {
                IsAvailable       = false,
                ProviderReachable = false,
                Provider          = "docker",
                Message           = $"Docker Model Runner is not reachable: {ex.Message}",
                Timestamp         = DateTime.UtcNow,
            };
        }
    }
}
