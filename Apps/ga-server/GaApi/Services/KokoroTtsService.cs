namespace GaApi.Services;

using System.Net.Http.Json;
using System.Text.Json;
using Configuration;
using Microsoft.Extensions.Options;

/// <summary>
///     Calls a local Kokoro-82M TTS server (Kokoro-FastAPI, OpenAI-compatible).
///     Returns null on failure so the controller can signal fallback. Apache 2.0,
///     ~85x smaller than Voxtral, runs fully local.
/// </summary>
public sealed class KokoroTtsService : IKokoroTtsService
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<KokoroTtsOptions> _optionsMonitor;
    private readonly ILogger<KokoroTtsService> _logger;
    private bool _isHealthy;

    public KokoroTtsService(
        HttpClient httpClient,
        IOptionsMonitor<KokoroTtsOptions> optionsMonitor,
        ILogger<KokoroTtsService> logger)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;

        var options = optionsMonitor.CurrentValue;
        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = new Uri(options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }

    public bool IsHealthy => _isHealthy;

    public async Task<byte[]?> SynthesizeAsync(
        string text,
        string? voice = null,
        CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;
        var selectedVoice = string.IsNullOrWhiteSpace(voice) ? options.DefaultVoice : voice;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/audio/speech")
            {
                Content = JsonContent.Create(new
                {
                    model = options.Model,
                    input = text,
                    voice = selectedVoice,
                    response_format = options.ResponseFormat
                })
            };

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Kokoro TTS returned {StatusCode}: {Body}",
                    (int)response.StatusCode, errorBody);
                _isHealthy = false;
                return null;
            }

            var audio = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            _isHealthy = true;
            return audio;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call Kokoro TTS server at {BaseUrl}", options.BaseUrl);
            _isHealthy = false;
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> GetVoicesAsync(CancellationToken cancellationToken = default)
    {
        // Kokoro-FastAPI exposes /v1/audio/voices — fall back to a hard-coded set if not available.
        try
        {
            using var response = await _httpClient.GetAsync("/v1/audio/voices", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("voices", out var voicesEl) &&
                    voicesEl.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<string>();
                    foreach (var v in voicesEl.EnumerateArray())
                    {
                        var s = v.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) list.Add(s!);
                    }
                    if (list.Count > 0)
                    {
                        _isHealthy = true;
                        return list;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Kokoro voices endpoint unavailable — returning static list");
        }

        return new[]
        {
            "af_bella", "af_sarah", "af_nicole",
            "am_adam", "am_michael",
            "bf_emma", "bm_george"
        };
    }

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/health", cancellationToken);
            _isHealthy = response.IsSuccessStatusCode;
            return _isHealthy;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Kokoro health check failed");
            _isHealthy = false;
            return false;
        }
    }
}
