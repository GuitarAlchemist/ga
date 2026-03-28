namespace GaApi.Configuration;

/// <summary>
///     Configuration options for the Voxtral TTS (Mistral AI) backend proxy.
///     Bound from the "VoxtralTts" section in appsettings.json.
/// </summary>
public sealed class VoxtralTtsOptions
{
    public const string SectionName = "VoxtralTts";

    /// <summary>
    ///     Mistral API key. When empty, the TTS endpoint returns 503 (fallback to browser speech).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    ///     Base URL for the Mistral API.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.mistral.ai";

    /// <summary>
    ///     Voxtral model identifier.
    /// </summary>
    public string Model { get; set; } = "voxtral-mini-tts-2603";

    /// <summary>
    ///     Preset voice ID for Demerzel (Oliver - Neutral, British male, articulate).
    /// </summary>
    public string Voice { get; set; } = "e3596645-b1af-469e-b857-f18ddedc7652";

    /// <summary>
    ///     Audio response format (mp3, wav, flac, opus, pcm).
    /// </summary>
    public string ResponseFormat { get; set; } = "mp3";

    /// <summary>
    ///     Maximum allowed text length per request.
    /// </summary>
    public int MaxTextLength { get; set; } = 5000;

    /// <summary>
    ///     Whether the service is configured (has a valid API key).
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
