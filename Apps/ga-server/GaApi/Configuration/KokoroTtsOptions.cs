namespace GaApi.Configuration;

/// <summary>
///     Configuration options for the local Kokoro-82M TTS server.
///     Bound from the "KokoroTts" section in appsettings.json.
/// </summary>
public sealed class KokoroTtsOptions
{
    public const string SectionName = "KokoroTts";

    /// <summary>
    ///     Base URL of the Kokoro-FastAPI server (OpenAI-compatible).
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:8880";

    /// <summary>
    ///     Default voice identifier (see jarvis-voice-kokoro-setup.md for the list).
    /// </summary>
    public string DefaultVoice { get; set; } = "af_bella";

    /// <summary>
    ///     Model identifier sent in the request body.
    /// </summary>
    public string Model { get; set; } = "kokoro";

    /// <summary>
    ///     Audio response format (mp3, wav, opus, flac, pcm).
    /// </summary>
    public string ResponseFormat { get; set; } = "mp3";

    /// <summary>
    ///     HTTP client timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    ///     Maximum allowed text length per request.
    /// </summary>
    public int MaxTextLength { get; set; } = 5000;
}
