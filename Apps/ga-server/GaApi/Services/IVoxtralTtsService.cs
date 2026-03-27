namespace GaApi.Services;

/// <summary>
///     Synthesizes speech from text using the Voxtral TTS API.
///     Returns null when the service is not configured or an error occurs.
/// </summary>
public interface IVoxtralTtsService
{
    /// <summary>
    ///     Synthesize speech from text. Returns raw audio bytes (MP3),
    ///     or null if the service is unconfigured or the request fails.
    /// </summary>
    Task<byte[]?> SynthesizeAsync(string text, CancellationToken cancellationToken = default);
}
