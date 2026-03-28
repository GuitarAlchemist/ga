namespace GaApi.Services;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
///     Visual critic powered by Claude vision — analyzes Prime Radiant screenshots
///     and returns IXQL commands to fix visual issues + algedonic pain/pleasure signals.
///     Uses raw HTTP to avoid SDK type complexity for vision/image content blocks.
///     Future: refactor as an ix pipeline connector.
/// </summary>
public class VisualCriticService
{
    private readonly HttpClient _http;
    private readonly ILogger<VisualCriticService> _logger;
    private readonly string _model;
    private readonly string? _apiKey;

    private const string CriticSystemPrompt = """
        You are a visual critic for a 3D space visualization called the Prime Radiant.
        You analyze screenshots of a solar system with planets orbiting a sun.

        Your job:
        1. Identify visual issues (artifacts, gaps, wrong colors, unrealistic lighting, broken rendering)
        2. Rate the visual quality from 1-10
        3. Output IXQL commands to fix issues where possible
        4. Emit a pain or pleasure algedonic signal based on quality

        Focus especially on Earth — it should look realistic with:
        - Proper day/night terminator with warm sunrise/sunset glow
        - Visible cloud cover (white, semi-transparent, no gaps)
        - Seasonal snow at poles
        - Blue atmosphere rim glow on day side
        - City lights on night side
        - Correct proportions relative to other planets

        IXQL syntax:
          SELECT nodes WHERE <predicate> SET <property> = <value>
          Properties: glow, pulse, size, color, visible, opacity, speed

        Respond in this exact JSON format (no markdown, no code fences):
        {
          "quality": 7,
          "issues": ["description of issue 1"],
          "ixql_commands": ["SELECT nodes WHERE name = 'earth' SET glow = true"],
          "signal_type": "pleasure",
          "signal_severity": "info",
          "signal_description": "Earth rendering quality assessment",
          "suggestions": ["shader fix suggestion"]
        }

        signal_type: "pain" if quality < 5, "pleasure" if quality >= 5
        signal_severity: "critical" if quality < 3, "warning" if quality < 5, "info" otherwise
        """;

    private readonly string _ollamaUrl;
    private readonly string _ollamaModel;

    public VisualCriticService(IConfiguration configuration, ILogger<VisualCriticService> logger, IHttpClientFactory httpFactory)
    {
        _logger = logger;
        _http = httpFactory.CreateClient();
        _model = configuration["Anthropic:VisionModel"] ?? "claude-haiku-4-5-20251001";
        _apiKey = configuration["Anthropic:ApiKey"]
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        _ollamaUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        _ollamaModel = configuration["Ollama:VisionModel"] ?? "llava:7b";
    }

    public async Task<VisualCriticResult> AnalyzeScreenshotAsync(
        string base64Image,
        string mediaType = "image/png",
        CancellationToken ct = default)
    {
        // Try Claude first, fall back to Ollama (free, local)
        if (!string.IsNullOrEmpty(_apiKey))
        {
            var claudeResult = await TryClaudeAsync(base64Image, mediaType, ct);
            if (claudeResult != null) return claudeResult;
            _logger.LogWarning("[VisualCritic] Claude failed, falling back to Ollama");
        }

        return await TryOllamaAsync(base64Image, ct);
    }

    private async Task<VisualCriticResult?> TryClaudeAsync(
        string base64Image, string mediaType, CancellationToken ct)
    {

        // Build raw Anthropic API request with vision content
        var requestBody = new
        {
            model = _model,
            max_tokens = 1024,
            system = CriticSystemPrompt,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "image",
                            source = new
                            {
                                type = "base64",
                                media_type = mediaType,
                                data = base64Image,
                            }
                        },
                        new
                        {
                            type = "text",
                            text = (object)"Analyze this Prime Radiant screenshot. Focus on Earth's visual quality. Return JSON only."
                        }
                    }
                }
            }
        };

        _logger.LogInformation("[VisualCritic] Analyzing screenshot ({Model})", _model);

        try
        {
            var json = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            var response = await _http.SendAsync(request, ct);
            var responseText = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[VisualCritic] Claude API error {Code}: {Body}", response.StatusCode, responseText[..Math.Min(200, responseText.Length)]);
                return null; // fall through to Ollama
            }

            // Parse Anthropic response → extract text content
            using var doc = JsonDocument.Parse(responseText);
            var contentArray = doc.RootElement.GetProperty("content");
            var text = "";
            foreach (var block in contentArray.EnumerateArray())
            {
                if (block.GetProperty("type").GetString() == "text")
                {
                    text = block.GetProperty("text").GetString() ?? "";
                    break;
                }
            }

            // Strip code fences
            text = text.Trim();
            if (text.StartsWith("```")) text = text[(text.IndexOf('\n') + 1)..];
            if (text.EndsWith("```")) text = text[..text.LastIndexOf("```")];
            text = text.Trim();

            var result = JsonSerializer.Deserialize<VisualCriticResult>(text,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            _logger.LogInformation("[VisualCritic] Quality: {Quality}/10, Issues: {Count}",
                result?.Quality ?? 0, result?.Issues?.Length ?? 0);

            return result ?? new VisualCriticResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[VisualCritic] Claude analysis failed");
            return null; // fall through to Ollama
        }
    }

    private async Task<VisualCriticResult> TryOllamaAsync(string base64Image, CancellationToken ct)
    {
        try
        {
            var requestBody = new
            {
                model = _ollamaModel,
                prompt = "Analyze this screenshot of a 3D solar system visualization. Focus on Earth's visual quality. " +
                         "Rate quality 1-10. Return ONLY valid JSON: " +
                         "{\"quality\":N,\"issues\":[\"...\"],\"ixql_commands\":[],\"signal_type\":\"pain|pleasure\",\"signal_severity\":\"info|warning|critical\",\"signal_description\":\"...\",\"suggestions\":[\"...\"]}",
                images = new[] { base64Image },
                stream = false,
                format = "json",
            };

            var json = JsonSerializer.Serialize(requestBody);
            var response = await _http.PostAsync(
                $"{_ollamaUrl}/api/generate",
                new StringContent(json, Encoding.UTF8, "application/json"), ct);

            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("[VisualCritic] Ollama error {Code}: {Body}", response.StatusCode, errBody[..Math.Min(200, errBody.Length)]);
                return new VisualCriticResult
                {
                    Quality = 0,
                    Issues = [$"Ollama error: {response.StatusCode}"],
                    SignalType = "pain", SignalSeverity = "warning",
                    SignalDescription = "Visual critic: both Claude and Ollama failed",
                };
            }

            var responseText = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseText);
            var text = doc.RootElement.GetProperty("response").GetString() ?? "{}";

            // Strip code fences
            text = text.Trim();
            if (text.StartsWith("```")) text = text[(text.IndexOf('\n') + 1)..];
            if (text.EndsWith("```")) text = text[..text.LastIndexOf("```")];
            text = text.Trim();

            var result = JsonSerializer.Deserialize<VisualCriticResult>(text,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            _logger.LogInformation("[VisualCritic] Ollama quality: {Quality}/10, Issues: {Count}",
                result?.Quality ?? 0, result?.Issues?.Length ?? 0);

            return result ?? new VisualCriticResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VisualCritic] Ollama analysis failed");
            return new VisualCriticResult
            {
                Quality = 0,
                Issues = [$"Both Claude and Ollama failed: {ex.Message}"],
                SignalType = "pain", SignalSeverity = "warning",
                SignalDescription = "Visual critic unavailable",
            };
        }
    }
}

public class VisualCriticResult
{
    [JsonPropertyName("quality")]
    public int Quality { get; set; }

    [JsonPropertyName("issues")]
    public string[]? Issues { get; set; }

    [JsonPropertyName("ixql_commands")]
    public string[]? IxqlCommands { get; set; }

    [JsonPropertyName("signal_type")]
    public string? SignalType { get; set; }

    [JsonPropertyName("signal_severity")]
    public string? SignalSeverity { get; set; }

    [JsonPropertyName("signal_description")]
    public string? SignalDescription { get; set; }

    [JsonPropertyName("suggestions")]
    public string[]? Suggestions { get; set; }
}
