namespace GaApi.Services;

/// <summary>
///     Synthesizes speech from text using a local Kokoro-82M TTS server
///     (OpenAI-compatible API). Second TTS provider alongside Voxtral.
/// </summary>
public interface IKokoroTtsService
{
    /// <summary>
    ///     Synthesize speech from text. Returns raw audio bytes (mp3),
    ///     or null if the service is unreachable or the request fails.
    /// </summary>
    Task<byte[]?> SynthesizeAsync(string text, string? voice = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     List available Kokoro voice identifiers.
    /// </summary>
    Task<IReadOnlyList<string>> GetVoicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Whether the Kokoro server last responded to a health check.
    /// </summary>
    bool IsHealthy { get; }

    /// <summary>
    ///     Pings the Kokoro server and updates <see cref="IsHealthy"/>.
    /// </summary>
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}
