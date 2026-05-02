namespace GaApi.Services;

using System.Text;
using System.Text.Json;

/// <summary>
///     Harmonic Nebula chatbot with tool-use against existing GA services.
///     Grounded in the voicing corpus so replies cite real voicings
///     instead of hallucinating.
///
///     Supports two providers, selected via <c>Nebula:Provider</c>:
///     <list type="bullet">
///       <item><c>ollama</c> (default) — local, free. Requires a pulled
///         tool-capable model (e.g. qwen2.5:7b, llama3.1:8b).</item>
///       <item><c>anthropic</c> — Claude Haiku 4.5 via HTTP API. Needs
///         ANTHROPIC_API_KEY + credits.</item>
///     </list>
///
///     Called by <see cref="Controllers.NebulaChatController"/>.
/// </summary>
public class NebulaSidekickService(
    IConfiguration configuration,
    ILogger<NebulaSidekickService> logger,
    ISemanticKnowledgeSource semanticKnowledge)
{
    // Direct HttpClient — IHttpClientFactory-provided clients carry a
    // 30s Polly timeout via Microsoft.Extensions.Http.Resilience that
    // kills Ollama tool-loops on CPU inference.
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };

    private readonly string _provider =
        (configuration["Nebula:Provider"] ?? "ollama").ToLowerInvariant();

    private readonly string _anthropicModel = configuration["Anthropic:NebulaModel"]
        ?? configuration["Anthropic:Model"]
        ?? "claude-haiku-4-5-20251001";
    private readonly string? _anthropicKey = configuration["Anthropic:ApiKey"]
        ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

    private readonly string _ollamaUrl =
        (configuration["Ollama:BaseUrl"] ?? "http://localhost:11434").TrimEnd('/');
    private readonly string _ollamaModel = configuration["Ollama:NebulaModel"] ?? "llama3.1:latest";
    /// <summary>Scholar-mode model — deep reasoning, no tool-use.</summary>
    private readonly string _scholarModel = configuration["Ollama:ScholarModel"] ?? "qwopus-glm:18b";

    /// <summary>Max tool-loop iterations to guard against runaway recursion.</summary>
    private const int MaxToolIterations = 6;

    public Task<NebulaChatReply> ChatAsync(NebulaChatRequest request, CancellationToken ct)
    {
        // Scholar mode: local frankenmerge, no tool-use, context stuffed
        // into system prompt — overrides the provider config.
        if (string.Equals(request.Mode, "scholar", StringComparison.OrdinalIgnoreCase))
            return ChatViaOllamaScholarAsync(request, ct);

        return _provider switch
        {
            "anthropic" => ChatViaAnthropicAsync(request, ct),
            _ => ChatViaOllamaAsync(request, ct),
        };
    }

    // =================================================================
    // Provider: Ollama Scholar — no tools, rich context in system prompt
    // =================================================================
    private async Task<NebulaChatReply> ChatViaOllamaScholarAsync(NebulaChatRequest request, CancellationToken ct)
    {
        var systemPrompt = BuildScholarSystemPrompt(request.Context);
        var messages = new List<object> { new { role = "system", content = systemPrompt } };
        if (request.History is { Count: > 0 })
        {
            foreach (var m in request.History)
                messages.Add(new { role = m.Role, content = m.Content });
        }
        messages.Add(new { role = "user", content = request.Message });

        var body = new
        {
            model = _scholarModel,
            messages,
            stream = false,
            options = new { temperature = 0.7, num_ctx = 8192 },
        };

        var http = new HttpRequestMessage(HttpMethod.Post, $"{_ollamaUrl}/api/chat")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };

        try
        {
            var resp = await _http.SendAsync(http, ct);
            var respText = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Scholar chat: Ollama {Code}: {Body}", resp.StatusCode, respText[..Math.Min(300, respText.Length)]);
                string friendly = $"Scholar model error {(int)resp.StatusCode}.";
                if (respText.Contains("model") && respText.Contains("not found"))
                {
                    friendly = $"Scholar model '{_scholarModel}' not imported yet. See docs/scholar-setup.md — import the GGUF via `ollama create {_scholarModel} -f Modelfile`.";
                }
                return new NebulaChatReply(friendly, [], $"ollama-{(int)resp.StatusCode}");
            }

            using var doc = JsonDocument.Parse(respText);
            var reply = doc.RootElement.TryGetProperty("message", out var msg)
                && msg.TryGetProperty("content", out var content)
                ? content.GetString() ?? ""
                : "";
            return new NebulaChatReply(reply.Trim(), [], null);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Scholar chat: Ollama unreachable");
            return new NebulaChatReply(
                $"Cannot reach Ollama at {_ollamaUrl}. Start Ollama and import the scholar model.",
                [],
                "ollama-unreachable");
        }
    }

    private static string BuildScholarSystemPrompt(NebulaChatContext? context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are the Harmonic Nebula SCHOLAR — a graduate-level music theorist with deep");
        sb.AppendLine("expertise in voice-leading, harmonic analysis, counterpoint, post-tonal theory,");
        sb.AppendLine("jazz harmony, and the history of chord voicings on guitar, bass, and ukulele.");
        sb.AppendLine();
        sb.AppendLine("The user is exploring the OPTIC-K corpus (688,351 chord voicings, 15 shape+sound");
        sb.AppendLine("clusters across guitar/bass/ukulele) visualized as a 3D 'Harmonic Nebula'.");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Reason deeply. Cite theory (Schenker, Riemann, Schoenberg, Tymoczko, Persichetti) when relevant.");
        sb.AppendLine("- Ground answers in whatever context the user has provided. Do NOT fabricate voicing IDs or frets.");
        sb.AppendLine("- Responses are rendered in a chat bubble. Use concise paragraphs + bullet lists.");
        sb.AppendLine("- When asked about 'this chord', use the detailed voicing context below.");

        if (context?.SelectedVoicing is { } v)
        {
            sb.AppendLine();
            sb.AppendLine("## Currently selected voicing");
            sb.AppendLine($"- Voicing ID: {v.GlobalId}");
            sb.AppendLine($"- Chord name (heuristic): {v.ChordName}");
            sb.AppendLine($"- Family: {v.Family}");
            sb.AppendLine($"- Instrument: {v.Instrument}");
            sb.AppendLine($"- Cluster: {v.ClusterLabel} ({v.ClusterId})");
            sb.AppendLine($"- MIDI notes (low→high): {string.Join(", ", v.Midi)}");
            sb.AppendLine($"- Frets (high-string first): {string.Join(" ", v.Frets)}");
            sb.AppendLine($"- Pitch-class set: {{{string.Join(", ", v.PitchClasses)}}}");
        }

        if (context?.InstrumentCounts is { Count: > 0 } counts)
        {
            sb.AppendLine();
            sb.AppendLine("## Corpus scale");
            foreach (var kv in counts) sb.AppendLine($"- {kv.Key}: {kv.Value:N0} voicings");
        }

        return sb.ToString();
    }

    // =================================================================
    // Provider: Ollama — local, free, OpenAI-shape tool_calls
    // =================================================================
    private async Task<NebulaChatReply> ChatViaOllamaAsync(NebulaChatRequest request, CancellationToken ct)
    {
        var systemPrompt = BuildSystemPrompt(request.Context);
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt },
        };
        if (request.History is { Count: > 0 })
        {
            foreach (var m in request.History)
                messages.Add(new { role = m.Role, content = m.Content });
        }
        messages.Add(new { role = "user", content = request.Message });

        var toolCalls = new List<NebulaToolCall>();

        for (var iter = 0; iter < MaxToolIterations; iter++)
        {
            var body = new
            {
                model = _ollamaModel,
                messages,
                tools = OllamaToolDefinitions(),
                stream = false,
            };

            var http = new HttpRequestMessage(HttpMethod.Post, $"{_ollamaUrl}/api/chat")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };

            HttpResponseMessage resp;
            string respText;
            try
            {
                resp = await _http.SendAsync(http, ct);
                respText = await resp.Content.ReadAsStringAsync(ct);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Nebula chat: Ollama unreachable at {Url}", _ollamaUrl);
                return new NebulaChatReply(
                    Reply: $"Cannot reach Ollama at {_ollamaUrl}. Start Ollama (`ollama serve`) and pull a tool-capable model: `ollama pull {_ollamaModel}`.",
                    ToolCalls: toolCalls,
                    Error: "ollama-unreachable");
            }

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Nebula chat: Ollama {Code}: {Body}", resp.StatusCode, respText[..Math.Min(300, respText.Length)]);
                string friendly = $"Ollama error {(int)resp.StatusCode}.";
                if (respText.Contains("model") && respText.Contains("not found"))
                    friendly = $"Model '{_ollamaModel}' not pulled. Run: `ollama pull {_ollamaModel}`.";
                return new NebulaChatReply(
                    Reply: friendly,
                    ToolCalls: toolCalls,
                    Error: $"ollama-{(int)resp.StatusCode}");
            }

            using var doc = JsonDocument.Parse(respText);
            if (!doc.RootElement.TryGetProperty("message", out var messageEl))
            {
                return new NebulaChatReply(
                    Reply: "Ollama returned no message.",
                    ToolCalls: toolCalls,
                    Error: "ollama-no-message");
            }

            var assistantText = messageEl.TryGetProperty("content", out var contentEl)
                ? contentEl.GetString() ?? ""
                : "";

            var hasToolCalls = messageEl.TryGetProperty("tool_calls", out var toolCallsEl)
                && toolCallsEl.ValueKind == JsonValueKind.Array
                && toolCallsEl.GetArrayLength() > 0;

            if (!hasToolCalls)
            {
                return new NebulaChatReply(
                    Reply: assistantText.Trim(),
                    ToolCalls: toolCalls,
                    Error: null);
            }

            // Record the assistant turn with the tool_calls so the next
            // turn's tool message is correctly ordered.
            var assistantTurn = new Dictionary<string, object?>
            {
                ["role"] = "assistant",
                ["content"] = assistantText,
                ["tool_calls"] = JsonSerializer.Deserialize<JsonElement>(toolCallsEl.GetRawText()),
            };
            messages.Add(assistantTurn);

            foreach (var call in toolCallsEl.EnumerateArray())
            {
                if (!call.TryGetProperty("function", out var fn)) continue;
                var name = fn.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                // Ollama may emit `arguments` as an object OR as a JSON string.
                JsonElement argsElement;
                if (fn.TryGetProperty("arguments", out var argsEl))
                {
                    argsElement = argsEl.ValueKind == JsonValueKind.String
                        ? JsonDocument.Parse(argsEl.GetString() ?? "{}").RootElement.Clone()
                        : argsEl.Clone();
                }
                else
                {
                    argsElement = JsonDocument.Parse("{}").RootElement.Clone();
                }

                var resultJson = await ExecuteToolAsync(name, argsElement, request.Context, ct);
                toolCalls.Add(new NebulaToolCall(name, argsElement.GetRawText(), resultJson));

                messages.Add(new { role = "tool", content = resultJson, name });
            }
        }

        return new NebulaChatReply(
            Reply: "Tool loop exceeded the iteration budget — stopping to avoid runaway reasoning.",
            ToolCalls: toolCalls,
            Error: "tool-loop-overflow");
    }

    // =================================================================
    // Provider: Anthropic — Claude Haiku 4.5, tool_use content blocks
    // =================================================================
    private async Task<NebulaChatReply> ChatViaAnthropicAsync(NebulaChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_anthropicKey))
        {
            return new NebulaChatReply(
                Reply: "Set ANTHROPIC_API_KEY to use the Anthropic provider, or switch Nebula:Provider to 'ollama'.",
                ToolCalls: [],
                Error: "missing-api-key");
        }

        var systemPrompt = BuildSystemPrompt(request.Context);
        var messages = BuildAnthropicMessages(request);
        var toolCalls = new List<NebulaToolCall>();

        for (var iter = 0; iter < MaxToolIterations; iter++)
        {
            var body = new
            {
                model = _anthropicModel,
                max_tokens = 1024,
                system = systemPrompt,
                tools = AnthropicToolDefinitions(),
                messages,
            };

            var http = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
            http.Headers.Add("x-api-key", _anthropicKey);
            http.Headers.Add("anthropic-version", "2023-06-01");

            var resp = await _http.SendAsync(http, ct);
            var respText = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Nebula chat: Anthropic {Code}: {Body}", resp.StatusCode, respText[..Math.Min(300, respText.Length)]);
                string friendly = $"Anthropic API error {(int)resp.StatusCode}.";
                try
                {
                    using var errDoc = JsonDocument.Parse(respText);
                    if (errDoc.RootElement.TryGetProperty("error", out var err)
                        && err.TryGetProperty("message", out var msg))
                    {
                        friendly = msg.GetString() ?? friendly;
                    }
                }
                catch { /* non-JSON error body, keep generic message */ }
                return new NebulaChatReply(
                    Reply: friendly,
                    ToolCalls: toolCalls,
                    Error: $"anthropic-{(int)resp.StatusCode}");
            }

            using var doc = JsonDocument.Parse(respText);
            var stopReason = doc.RootElement.GetProperty("stop_reason").GetString();
            var contentBlocks = doc.RootElement.GetProperty("content");

            var assistantBlocks = new List<object>();
            var toolUses = new List<(string Id, string Name, JsonElement Input)>();
            var textSoFar = new StringBuilder();

            foreach (var block in contentBlocks.EnumerateArray())
            {
                var type = block.GetProperty("type").GetString();
                if (type == "text")
                {
                    var txt = block.GetProperty("text").GetString() ?? "";
                    assistantBlocks.Add(new { type = "text", text = txt });
                    textSoFar.Append(txt);
                }
                else if (type == "tool_use")
                {
                    var id = block.GetProperty("id").GetString() ?? "";
                    var name = block.GetProperty("name").GetString() ?? "";
                    var input = block.GetProperty("input");
                    toolUses.Add((id, name, input.Clone()));
                    assistantBlocks.Add(new
                    {
                        type = "tool_use",
                        id,
                        name,
                        input = JsonSerializer.Deserialize<JsonElement>(input.GetRawText()),
                    });
                }
            }

            if (toolUses.Count == 0 || stopReason != "tool_use")
            {
                return new NebulaChatReply(
                    Reply: textSoFar.ToString().Trim(),
                    ToolCalls: toolCalls,
                    Error: null);
            }

            messages.Add(new { role = "assistant", content = assistantBlocks });

            var toolResultBlocks = new List<object>();
            foreach (var use in toolUses)
            {
                var resultJson = await ExecuteToolAsync(use.Name, use.Input, request.Context, ct);
                toolCalls.Add(new NebulaToolCall(use.Name, use.Input.GetRawText(), resultJson));
                toolResultBlocks.Add(new
                {
                    type = "tool_result",
                    tool_use_id = use.Id,
                    content = resultJson,
                });
            }
            messages.Add(new { role = "user", content = toolResultBlocks });
        }

        return new NebulaChatReply(
            Reply: "Tool loop exceeded the iteration budget — stopping to avoid runaway reasoning.",
            ToolCalls: toolCalls,
            Error: "tool-loop-overflow");
    }

    // =================================================================
    // Tool definitions — separate shapes per provider
    // =================================================================
    /// <summary>Anthropic tool-use schema: flat with <c>input_schema</c>.</summary>
    private static object[] AnthropicToolDefinitions() =>
    [
        new
        {
            name = "search_voicings",
            description = "Semantic search over the 688k-voicing OPTIC-K corpus. " +
                          "Use for musical queries like 'jazzy Cmaj7', 'easy open-position D', 'bright triads'.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    query = new { type = "string", description = "Natural-language musical query." },
                    limit = new { type = "integer", description = "Max results (1-20). Default 8." },
                },
                required = new[] { "query" },
            },
        },
        new
        {
            name = "describe_selected_voicing",
            description = "Return full detail of the smartie the user has currently selected " +
                          "(notes, frets, chord name, cluster). Call this before answering " +
                          "questions about 'this chord' or 'the selected voicing'.",
            input_schema = new { type = "object", properties = new { } },
        },
        new
        {
            name = "get_corpus_stats",
            description = "High-level stats about the Harmonic Nebula corpus — cluster count, " +
                          "voicing count per instrument, chord-family distribution.",
            input_schema = new { type = "object", properties = new { } },
        },
    ];

    /// <summary>Ollama / OpenAI tool-use schema: <c>function</c> wrapper with <c>parameters</c>.</summary>
    private static object[] OllamaToolDefinitions() =>
    [
        new
        {
            type = "function",
            function = new
            {
                name = "search_voicings",
                description = "Semantic search over the 688k-voicing OPTIC-K corpus. " +
                              "Use for musical queries like 'jazzy Cmaj7', 'easy open-position D'.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Natural-language musical query." },
                        limit = new { type = "integer", description = "Max results (1-20). Default 8." },
                    },
                    required = new[] { "query" },
                },
            },
        },
        new
        {
            type = "function",
            function = new
            {
                name = "describe_selected_voicing",
                description = "Return full detail of the smartie the user has currently selected. " +
                              "Call this before answering questions about 'this chord' or 'the selected voicing'.",
                parameters = new { type = "object", properties = new { } },
            },
        },
        new
        {
            type = "function",
            function = new
            {
                name = "get_corpus_stats",
                description = "High-level stats about the Harmonic Nebula corpus.",
                parameters = new { type = "object", properties = new { } },
            },
        },
    ];

    // =================================================================
    // Shared tool execution (provider-independent)
    // =================================================================
    private async Task<string> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        NebulaChatContext? context,
        CancellationToken ct)
    {
        try
        {
            return toolName switch
            {
                "search_voicings" => await RunSearchVoicings(input, ct),
                "describe_selected_voicing" => DescribeSelectedVoicing(context),
                "get_corpus_stats" => GetCorpusStats(context),
                _ => JsonSerializer.Serialize(new { error = $"unknown tool: {toolName}" }),
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nebula tool {Tool} failed", toolName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<string> RunSearchVoicings(JsonElement input, CancellationToken ct)
    {
        var query = input.TryGetProperty("query", out var q) ? q.GetString() ?? "" : "";
        var limit = input.TryGetProperty("limit", out var l) && l.TryGetInt32(out var li) ? li : 8;
        limit = Math.Clamp(limit, 1, 20);
        var results = await semanticKnowledge.SearchAsync(query, limit, ct);
        return JsonSerializer.Serialize(new
        {
            query,
            count = results.Count,
            results = results.Select(r => new { score = r.Score, snippet = r.Content }),
        });
    }

    private static string DescribeSelectedVoicing(NebulaChatContext? context)
    {
        if (context?.SelectedVoicing is null)
            return JsonSerializer.Serialize(new { status = "no-selection" });
        return JsonSerializer.Serialize(context.SelectedVoicing);
    }

    private static string GetCorpusStats(NebulaChatContext? context)
    {
        var counts = context?.InstrumentCounts
            ?? new Dictionary<string, int> { ["guitar"] = 667125, ["bass"] = 12614, ["ukulele"] = 8612 };
        return JsonSerializer.Serialize(new
        {
            total_voicings = counts.Values.Sum(),
            cluster_count = 15,
            per_instrument = counts,
            chord_families = new[] { "Major", "Minor", "Dominant", "Diminished", "Suspended", "Altered", "Other" },
        });
    }

    // =================================================================
    // Shared helpers
    // =================================================================
    private static string BuildSystemPrompt(NebulaChatContext? context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are the Harmonic Nebula Sidekick — a musician's companion for exploring");
        sb.AppendLine("the OPTIC-K voicing corpus (guitar, bass, ukulele) visualized as a 3D nebula");
        sb.AppendLine("of 688,351 chord voicings clustered into 15 shape+sound clouds.");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Use tools whenever a question touches real corpus data. Do not invent voicing IDs, cluster names, or fingerings.");
        sb.AppendLine("- When asked about 'this chord' or 'the selected voicing', ALWAYS call describe_selected_voicing first.");
        sb.AppendLine("- Responses are rendered in a chat bubble — keep them short and musically concrete.");
        sb.AppendLine("- If no smartie is selected and context is needed, say so and suggest the user click a smartie.");
        if (context?.SelectedVoicing is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"The user currently has voicing {context.SelectedVoicing.GlobalId} selected ({context.SelectedVoicing.ChordName}, {context.SelectedVoicing.Instrument}).");
        }
        return sb.ToString();
    }

    private static List<object> BuildAnthropicMessages(NebulaChatRequest request)
    {
        var messages = new List<object>();
        if (request.History is { Count: > 0 })
        {
            foreach (var m in request.History)
                messages.Add(new { role = m.Role, content = m.Content });
        }
        messages.Add(new { role = "user", content = request.Message });
        return messages;
    }
}

// =================================================================
// Request / response DTOs
// =================================================================

public sealed record NebulaChatRequest(
    string Message,
    List<NebulaChatTurn>? History,
    NebulaChatContext? Context,
    /// <summary>"fast" (default, tool-use) or "scholar" (deep reasoning, no tools).</summary>
    string? Mode = null);

public sealed record NebulaChatTurn(string Role, string Content);

public sealed record NebulaChatContext(
    NebulaSelectedVoicing? SelectedVoicing,
    Dictionary<string, int>? InstrumentCounts);

public sealed record NebulaSelectedVoicing(
    string GlobalId,
    string ClusterId,
    string ClusterLabel,
    string Instrument,
    string ChordName,
    string Family,
    int[] Midi,
    string[] Frets,
    int[] PitchClasses);

public sealed record NebulaChatReply(
    string Reply,
    List<NebulaToolCall> ToolCalls,
    string? Error);

public sealed record NebulaToolCall(string Name, string InputJson, string ResultJson);
