namespace GA.Business.AI.AI.HuggingFace;

/// <summary>
///     Request to generate audio from text using Hugging Face models
/// </summary>
public record TextToAudioRequest(
    string Inputs,
    TextToAudioParameters? Parameters = null
);

/// <summary>
///     Parameters for text-to-audio generation
/// </summary>
public record TextToAudioParameters(
    int? MaxNewTokens = null,
    bool? DoSample = null,
    double? Temperature = null,
    double? TopP = null,
    int? Seed = null
);

/// <summary>
///     Response from text-to-audio generation (binary audio data)
/// </summary>
public record TextToAudioResponse(
    byte[] AudioData,
    string ContentType,
    int SizeBytes
);

/// <summary>
///     Request to generate music from text description
/// </summary>
public record MusicGenerationRequest(
    string Description,
    double DurationSeconds = 10.0,
    double Temperature = 1.0,
    int? Seed = null
);

/// <summary>
///     Response from music generation
/// </summary>
public record MusicGenerationResponse(
    byte[] AudioData,
    string Format,
    int SampleRate,
    double DurationSeconds,
    int SizeBytes,
    string GeneratedAt
);

/// <summary>
///     Request to generate guitar audio from tablature
/// </summary>
public record GuitarSynthesisRequest(
    string Tablature,
    string Style = "electric",
    double Tempo = 120.0,
    string? Tuning = null
);

/// <summary>
///     Response from guitar synthesis
/// </summary>
public record GuitarSynthesisResponse(
    byte[] AudioData,
    string Format,
    int SampleRate,
    double DurationSeconds,
    int SizeBytes,
    string GeneratedAt
);

/// <summary>
///     Hugging Face API error response
/// </summary>
public record HuggingFaceError(
    string Error,
    List<string>? Warnings = null,
    int? EstimatedTime = null
);

/// <summary>
///     Model information from Hugging Face
/// </summary>
public record ModelInfo(
    string ModelId,
    string Task,
    List<string> Tags,
    string? Pipeline = null,
    bool? Private = null
);

/// <summary>
///     Available Hugging Face models for audio generation
/// </summary>
public static class HuggingFaceModels
{
    /// <summary>
    ///     Facebook MusicGen Small - Fast music generation
    /// </summary>
    public const string MusicGenSmall = "facebook/musicgen-small";

    /// <summary>
    ///     Facebook MusicGen Large - High quality music generation
    /// </summary>
    public const string MusicGenLarge = "facebook/musicgen-large";

    /// <summary>
    ///     Stability AI Stable Audio - Open source audio generation
    /// </summary>
    public const string StableAudioOpen = "stabilityai/stable-audio-open-1.0";

    /// <summary>
    ///     Riffusion - Music generation from text
    /// </summary>
    public const string Riffusion = "riffusion/riffusion-model-v1";

    /// <summary>
    ///     ChatTTS - Text to speech
    /// </summary>
    public const string ChatTTS = "2Noise/ChatTTS";
}

/// <summary>
///     Configuration settings for Hugging Face integration
/// </summary>
public record HuggingFaceSettings
{
    /// <summary>
    ///     Hugging Face API token (required for private models and higher rate limits)
    /// </summary>
    public string? ApiToken { get; init; }

    /// <summary>
    ///     Base URL for Hugging Face Inference API
    /// </summary>
    public string ApiUrl { get; init; } = "https://api-inference.huggingface.co";

    /// <summary>
    ///     Default model for music generation
    /// </summary>
    public string DefaultMusicModel { get; init; } = HuggingFaceModels.MusicGenSmall;

    /// <summary>
    ///     Default model for guitar synthesis
    /// </summary>
    public string DefaultGuitarModel { get; init; } = HuggingFaceModels.StableAudioOpen;

    /// <summary>
    ///     Timeout for API requests in seconds
    /// </summary>
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>
    ///     Maximum retries for failed requests
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    ///     Enable caching of generated audio
    /// </summary>
    public bool EnableCaching { get; init; } = true;

    /// <summary>
    ///     Cache directory for generated audio files
    /// </summary>
    public string CacheDirectory { get; init; } = "cache/audio";

    /// <summary>
    ///     Use mock client for testing without API token (generates synthetic audio)
    /// </summary>
    public bool UseMockClient { get; init; } = false;
}
