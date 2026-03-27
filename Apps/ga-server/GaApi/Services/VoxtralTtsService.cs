namespace GaApi.Services;

using System.Net.Http.Json;
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
                    voice = options.Voice,
                    response_format = options.ResponseFormat
                })
            };
            request.Headers.Add("Authorization", $"Bearer {options.ApiKey}");

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Voxtral TTS API returned {StatusCode}: {Reason}",
                    (int)response.StatusCode, response.ReasonPhrase);
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
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
