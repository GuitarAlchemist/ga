namespace GA.Business.AI.AI.HuggingFace;

using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

/// <summary>
///     Mock implementation of HuggingFaceClient for testing without API token
///     Generates synthetic audio data instead of calling the real API
/// </summary>
public class MockHuggingFaceClient(
    HttpClient httpClient,
    ILogger<MockHuggingFaceClient> logger,
    IOptions<HuggingFaceSettings> settings)
    : HuggingFaceClient(httpClient, logger, settings)
{
    private readonly Random _random = new();

    /// <summary>
    ///     Generate mock audio data instead of calling the real API
    /// </summary>
    public override async Task<TextToAudioResponse> GenerateAudioAsync(
        string modelId,
        TextToAudioRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("MOCK: Generating audio with model {ModelId}: {Inputs}",
            modelId, request.Inputs.Substring(0, Math.Min(50, request.Inputs.Length)));

        // Simulate API delay
        await Task.Delay(TimeSpan.FromSeconds(1 + _random.NextDouble() * 2), cancellationToken);

        // Generate synthetic WAV audio
        var audioData = GenerateSyntheticWavAudio(request);

        logger.LogInformation("MOCK: Audio generated successfully: {Size} bytes", audioData.Length);

        return new TextToAudioResponse(audioData, "audio/wav", audioData.Length);
    }

    /// <summary>
    ///     Generate a simple WAV file with a sine wave tone
    ///     This creates a valid WAV file that can be played
    /// </summary>
    private byte[] GenerateSyntheticWavAudio(TextToAudioRequest request)
    {
        // Parse duration from request inputs (e.g., "description, 5 seconds")
        var duration = ParseDuration(request.Inputs);
        var sampleRate = 44100;
        var channels = 2; // Stereo
        var bitsPerSample = 16;
        var numSamples = (int)(sampleRate * duration);

        // Calculate sizes
        var dataSize = numSamples * channels * (bitsPerSample / 8);
        var fileSize = 44 + dataSize; // 44 bytes for WAV header

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Write WAV header
        WriteWavHeader(writer, fileSize, sampleRate, channels, bitsPerSample, dataSize);

        // Generate audio samples (simple sine wave with some variation)
        var frequency = GetFrequencyFromDescription(request.Inputs);
        var amplitude = 0.3; // 30% volume to avoid clipping

        for (var i = 0; i < numSamples; i++)
        {
            // Generate a sine wave with some harmonic content
            var t = (double)i / sampleRate;
            var sample = amplitude * (
                Math.Sin(2 * Math.PI * frequency * t) +
                0.3 * Math.Sin(2 * Math.PI * frequency * 2 * t) + // 2nd harmonic
                0.2 * Math.Sin(2 * Math.PI * frequency * 3 * t) // 3rd harmonic
            );

            // Add some envelope (fade in/out)
            var envelope = 1.0;
            if (t < 0.1)
            {
                envelope = t / 0.1; // Fade in
            }

            if (t > duration - 0.1)
            {
                envelope = (duration - t) / 0.1; // Fade out
            }

            sample *= envelope;

            // Convert to 16-bit PCM
            var sampleValue = (short)(sample * short.MaxValue);

            // Write stereo samples
            writer.Write(sampleValue); // Left channel
            writer.Write(sampleValue); // Right channel
        }

        return ms.ToArray();
    }

    /// <summary>
    ///     Write WAV file header
    /// </summary>
    private static void WriteWavHeader(
        BinaryWriter writer,
        int fileSize,
        int sampleRate,
        int channels,
        int bitsPerSample,
        int dataSize)
    {
        // RIFF header
        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write(fileSize - 8);
        writer.Write(new[] { 'W', 'A', 'V', 'E' });

        // fmt chunk
        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(16); // Chunk size
        writer.Write((short)1); // Audio format (1 = PCM)
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8); // Byte rate
        writer.Write((short)(channels * bitsPerSample / 8)); // Block align
        writer.Write((short)bitsPerSample);

        // data chunk
        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(dataSize);
    }

    /// <summary>
    ///     Parse duration from request inputs
    /// </summary>
    private static double ParseDuration(string inputs)
    {
        // Look for patterns like "5 seconds" or "10 seconds"
        var match = Regex.Match(inputs, @"(\d+)\s*seconds?");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var seconds))
        {
            return seconds;
        }

        return 5.0; // Default duration
    }

    /// <summary>
    ///     Get a base frequency based on the description
    ///     This creates different tones for different descriptions
    /// </summary>
    private double GetFrequencyFromDescription(string description)
    {
        var lowerDesc = description.ToLowerInvariant();

        // Map musical terms to frequencies (notes)
        if (lowerDesc.Contains("blues") || lowerDesc.Contains("a minor"))
        {
            return 220.0; // A3
        }

        if (lowerDesc.Contains("rock") || lowerDesc.Contains("energetic"))
        {
            return 329.63; // E4
        }

        if (lowerDesc.Contains("calm") || lowerDesc.Contains("acoustic"))
        {
            return 261.63; // C4
        }

        if (lowerDesc.Contains("jazz"))
        {
            return 293.66; // D4
        }

        if (lowerDesc.Contains("metal") || lowerDesc.Contains("heavy"))
        {
            return 164.81; // E3
        }

        // Default to middle C
        return 261.63;
    }

    /// <summary>
    ///     Mock health check - always returns true
    /// </summary>
    public override async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("MOCK: Health check - always healthy");
        await Task.Delay(100, cancellationToken);
        return true;
    }

    /// <summary>
    ///     Mock model availability check - always returns true
    /// </summary>
    public override async Task<bool> IsModelAvailableAsync(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("MOCK: Model {ModelId} is available", modelId);
        await Task.Delay(50, cancellationToken);
        return true;
    }
}
