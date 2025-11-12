namespace GA.Business.AI.HuggingFace;

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;

/// <summary>
///     High-level service for music generation using Hugging Face models
/// </summary>
public class MusicGenService
{
    private readonly HuggingFaceClient _client;
    private readonly ILogger<MusicGenService> _logger;
    private readonly HuggingFaceSettings _settings;

    public MusicGenService(
        HuggingFaceClient client,
        ILogger<MusicGenService> logger,
        IOptions<HuggingFaceSettings> settings)
    {
        _client = client;
        _logger = logger;
        _settings = settings.Value;

        // Ensure cache directory exists
        if (_settings.EnableCaching && !string.IsNullOrEmpty(_settings.CacheDirectory))
        {
            Directory.CreateDirectory(_settings.CacheDirectory);
        }
    }

    /// <summary>
    ///     Generate music from a text description
    /// </summary>
    public async Task<MusicGenerationResponse> GenerateMusicAsync(
        MusicGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating music: {Description} ({Duration}s)",
                request.Description, request.DurationSeconds);

            // Check cache first
            if (_settings.EnableCaching)
            {
                var cached = await GetCachedAudioAsync(request);
                if (cached != null)
                {
                    _logger.LogInformation("Returning cached music for: {Description}", request.Description);
                    return cached;
                }
            }

            // Build the prompt with duration hint
            var prompt = $"{request.Description}, {request.DurationSeconds} seconds";

            var textToAudioRequest = new TextToAudioRequest(
                prompt,
                new TextToAudioParameters(
                    Temperature: request.Temperature,
                    Seed: request.Seed,
                    DoSample: true
                )
            );

            var response = await _client.GenerateAudioAsync(
                _settings.DefaultMusicModel,
                textToAudioRequest,
                cancellationToken
            );

            var result = new MusicGenerationResponse(
                response.AudioData,
                GetFormatFromContentType(response.ContentType),
                44100, // Default sample rate for MusicGen
                request.DurationSeconds,
                response.SizeBytes,
                DateTime.UtcNow.ToString("O")
            );

            // Cache the result
            if (_settings.EnableCaching)
            {
                await CacheAudioAsync(request, result);
            }

            _logger.LogInformation("Music generated successfully: {Size} bytes", result.SizeBytes);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating music for: {Description}", request.Description);
            throw;
        }
    }

    /// <summary>
    ///     Generate a backing track for practice
    /// </summary>
    public async Task<MusicGenerationResponse> GenerateBackingTrackAsync(
        string key,
        string style,
        double durationSeconds = 30.0,
        int? tempo = null,
        CancellationToken cancellationToken = default)
    {
        var tempoText = tempo.HasValue ? $" at {tempo} BPM" : "";
        var description = $"{style} backing track in {key}{tempoText}";

        var request = new MusicGenerationRequest(description, durationSeconds);
        return await GenerateMusicAsync(request, cancellationToken);
    }

    /// <summary>
    ///     Generate chord progression audio
    /// </summary>
    public async Task<MusicGenerationResponse> GenerateChordProgressionAsync(
        string progression,
        string key,
        string style = "acoustic guitar",
        double durationSeconds = 15.0,
        CancellationToken cancellationToken = default)
    {
        var description = $"{style} playing {progression} progression in {key}";

        var request = new MusicGenerationRequest(description, durationSeconds);
        return await GenerateMusicAsync(request, cancellationToken);
    }

    /// <summary>
    ///     Generate guitar-specific audio
    /// </summary>
    public async Task<GuitarSynthesisResponse> GenerateGuitarAudioAsync(
        GuitarSynthesisRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating guitar audio: {Style} at {Tempo} BPM",
                request.Style, request.Tempo);

            var description = $"{request.Style} guitar playing tablature at {request.Tempo} BPM";
            if (!string.IsNullOrEmpty(request.Tuning))
            {
                description += $" in {request.Tuning} tuning";
            }

            var textToAudioRequest = new TextToAudioRequest(
                description,
                new TextToAudioParameters(DoSample: true)
            );

            var response = await _client.GenerateAudioAsync(
                _settings.DefaultGuitarModel,
                textToAudioRequest,
                cancellationToken
            );

            var result = new GuitarSynthesisResponse(
                response.AudioData,
                GetFormatFromContentType(response.ContentType),
                44100,
                10.0, // Estimated duration
                response.SizeBytes,
                DateTime.UtcNow.ToString("O")
            );

            _logger.LogInformation("Guitar audio generated: {Size} bytes", result.SizeBytes);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating guitar audio");
            throw;
        }
    }

    /// <summary>
    ///     Get cached audio if available
    /// </summary>
    private async Task<MusicGenerationResponse?> GetCachedAudioAsync(MusicGenerationRequest request)
    {
        try
        {
            var cacheKey = GetCacheKey(request);
            var cachePath = Path.Combine(_settings.CacheDirectory, $"{cacheKey}.wav");
            var metaPath = Path.Combine(_settings.CacheDirectory, $"{cacheKey}.json");

            if (File.Exists(cachePath) && File.Exists(metaPath))
            {
                var audioData = await File.ReadAllBytesAsync(cachePath);
                var metaJson = await File.ReadAllTextAsync(metaPath);
                var meta = JsonSerializer.Deserialize<MusicGenerationResponse>(metaJson);

                if (meta != null)
                {
                    return meta with { AudioData = audioData };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading cached audio");
        }

        return null;
    }

    /// <summary>
    ///     Cache generated audio
    /// </summary>
    private async Task CacheAudioAsync(MusicGenerationRequest request, MusicGenerationResponse response)
    {
        try
        {
            var cacheKey = GetCacheKey(request);
            var cachePath = Path.Combine(_settings.CacheDirectory, $"{cacheKey}.wav");
            var metaPath = Path.Combine(_settings.CacheDirectory, $"{cacheKey}.json");

            await File.WriteAllBytesAsync(cachePath, response.AudioData);

            var metaJson = JsonSerializer.Serialize(response with { AudioData = Array.Empty<byte>() });
            await File.WriteAllTextAsync(metaPath, metaJson);

            _logger.LogDebug("Cached audio: {CacheKey}", cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error caching audio");
        }
    }

    /// <summary>
    ///     Generate cache key from request
    /// </summary>
    private static string GetCacheKey(MusicGenerationRequest request)
    {
        var input = $"{request.Description}|{request.DurationSeconds}|{request.Temperature}|{request.Seed}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    ///     Extract audio format from content type
    /// </summary>
    private static string GetFormatFromContentType(string contentType)
    {
        return contentType switch
        {
            "audio/wav" => "wav",
            "audio/mpeg" => "mp3",
            "audio/flac" => "flac",
            "audio/ogg" => "ogg",
            _ => "wav"
        };
    }
}
