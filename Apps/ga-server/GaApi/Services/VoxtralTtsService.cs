namespace GaApi.Services;

using System.Net.Http.Json;
using System.Text.Json;
using Configuration;
using Microsoft.Extensions.Options;

/// <summary>
///     Calls the Mistral Voxtral TTS API to synthesize speech.
///     Returns null when not configured (no API key) or on failure.
/// </summary>
public sealed class VoxtralTtsService(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<VoxtralTtsOptions> optionsMonitor,
    ILogger<VoxtralTtsService> logger) : IVoxtralTtsService
{
    public async Task<byte[]?> SynthesizeAsync(string text, CancellationToken cancellationToken = default)
    {
        var options = optionsMonitor.CurrentValue;
        if (!options.IsConfigured)
        {
            logger.LogDebug("Voxtral TTS is not configured (no API key). Returning null for fallback");
            return null;
        }

        try
        {
            var client = httpClientFactory.CreateClient("MistralTts");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/audio/speech")
            {
                Content = JsonContent.Create(new
                {
                    model = options.Model,
                    input = text,
                    voice_id = options.Voice,
                    response_format = options.ResponseFormat
                })
            };
            request.Headers.Add("Authorization", $"Bearer {options.ApiKey}");

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning(
                    "Voxtral TTS API returned {StatusCode}: {Body}",
                    (int)response.StatusCode, errorBody);
                return null;
            }

            // Voxtral returns JSON with base64-encoded audio_data
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("audio_data", out var audioData))
            {
                return Convert.FromBase64String(audioData.GetString() ?? string.Empty);
            }

            logger.LogWarning("Voxtral TTS response missing audio_data field");
            return null;
        }
        catch (OperationCanceledException)
        {
            throw; // Let cancellation propagate
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to call Voxtral TTS API");
            return null;
        }
    }
}
