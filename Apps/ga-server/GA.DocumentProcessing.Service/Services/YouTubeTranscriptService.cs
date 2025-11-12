namespace GA.DocumentProcessing.Service.Services;

using GA.DocumentProcessing.Service.Models;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Service for extracting transcripts from YouTube videos
/// Uses youtube-transcript-api via Python subprocess (free, no API key needed)
/// </summary>
public class YouTubeTranscriptService
{
    private readonly ILogger<YouTubeTranscriptService> _logger;
    private readonly HttpClient _httpClient;

    public YouTubeTranscriptService(
        ILogger<YouTubeTranscriptService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    /// <summary>
    /// Extract transcript from YouTube video URL
    /// </summary>
    public async Task<YouTubeTranscript> ExtractTranscriptAsync(
        string youtubeUrl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var videoId = ExtractVideoId(youtubeUrl);
            if (string.IsNullOrEmpty(videoId))
            {
                throw new ArgumentException("Invalid YouTube URL", nameof(youtubeUrl));
            }

            _logger.LogInformation("Extracting transcript for YouTube video: {VideoId}", videoId);

            // Try multiple methods in order of preference
            var transcript = await TryExtractWithYouTubeTranscriptApi(videoId, cancellationToken)
                ?? await TryExtractWithInvidiousApi(videoId, cancellationToken)
                ?? throw new InvalidOperationException($"Failed to extract transcript for video {videoId}");

            _logger.LogInformation("Successfully extracted transcript: {Length} characters", transcript.FullText.Length);
            return transcript;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting YouTube transcript from {Url}", youtubeUrl);
            throw;
        }
    }

    /// <summary>
    /// Extract video ID from YouTube URL
    /// </summary>
    private string? ExtractVideoId(string url)
    {
        // Support multiple YouTube URL formats
        var patterns = new[]
        {
            @"(?:youtube\.com\/watch\?v=|youtu\.be\/)([a-zA-Z0-9_-]{11})",
            @"youtube\.com\/embed\/([a-zA-Z0-9_-]{11})",
            @"youtube\.com\/v\/([a-zA-Z0-9_-]{11})"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(url, pattern);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        // If it's already just a video ID
        if (Regex.IsMatch(url, @"^[a-zA-Z0-9_-]{11}$"))
        {
            return url;
        }

        return null;
    }

    /// <summary>
    /// Try extracting with youtube-transcript-api Python library
    /// This requires Python and youtube-transcript-api to be installed
    /// </summary>
    private async Task<YouTubeTranscript?> TryExtractWithYouTubeTranscriptApi(
        string videoId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Create Python script to extract transcript
            var pythonScript = $@"
import json
from youtube_transcript_api import YouTubeTranscriptApi

try:
    transcript_list = YouTubeTranscriptApi.get_transcript('{videoId}')
    result = {{
        'success': True,
        'segments': transcript_list
    }}
    print(json.dumps(result))
except Exception as e:
    result = {{
        'success': False,
        'error': str(e)
    }}
    print(json.dumps(result))
";

            // Execute Python script
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "-c \"" + pythonScript.Replace("\"", "\\\"") + "\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var result = JsonSerializer.Deserialize<JsonElement>(output);
                if (result.GetProperty("success").GetBoolean())
                {
                    var segments = result.GetProperty("segments");
                    return ParseTranscriptSegments(videoId, segments);
                }
            }

            _logger.LogWarning("youtube-transcript-api failed: {Error}", error);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract with youtube-transcript-api");
            return null;
        }
    }

    /// <summary>
    /// Try extracting with Invidious API (free, no API key)
    /// </summary>
    private async Task<YouTubeTranscript?> TryExtractWithInvidiousApi(
        string videoId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use public Invidious instance
            var invidiousInstances = new[]
            {
                "https://invidious.snopyta.org",
                "https://yewtu.be",
                "https://invidious.kavin.rocks"
            };

            foreach (var instance in invidiousInstances)
            {
                try
                {
                    var url = $"{instance}/api/v1/captions/{videoId}?label=English";
                    var response = await _httpClient.GetAsync(url, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync(cancellationToken);
                        var captions = JsonSerializer.Deserialize<JsonElement>(json);

                        if (captions.ValueKind == JsonValueKind.Array && captions.GetArrayLength() > 0)
                        {
                            // Get first English caption track
                            var captionTrack = captions[0];
                            var captionUrl = captionTrack.GetProperty("url").GetString();

                            if (!string.IsNullOrEmpty(captionUrl))
                            {
                                var captionResponse = await _httpClient.GetAsync(captionUrl, cancellationToken);
                                var captionText = await captionResponse.Content.ReadAsStringAsync(cancellationToken);

                                return ParseVttOrSrt(videoId, captionText);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed with Invidious instance {Instance}", instance);
                    continue;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract with Invidious API");
            return null;
        }
    }

    /// <summary>
    /// Parse transcript segments from JSON
    /// </summary>
    private YouTubeTranscript ParseTranscriptSegments(string videoId, JsonElement segments)
    {
        var transcriptSegments = new List<TranscriptSegment>();
        var fullText = new StringBuilder();

        foreach (var segment in segments.EnumerateArray())
        {
            var text = segment.GetProperty("text").GetString() ?? "";
            var start = segment.GetProperty("start").GetDouble();
            var duration = segment.GetProperty("duration").GetDouble();

            transcriptSegments.Add(new TranscriptSegment
            {
                Text = text,
                Start = TimeSpan.FromSeconds(start),
                Duration = TimeSpan.FromSeconds(duration)
            });

            fullText.AppendLine(text);
        }

        return new YouTubeTranscript
        {
            VideoId = videoId,
            VideoUrl = $"https://www.youtube.com/watch?v={videoId}",
            Segments = transcriptSegments,
            FullText = fullText.ToString(),
            ExtractedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Parse VTT or SRT caption format
    /// </summary>
    private YouTubeTranscript ParseVttOrSrt(string videoId, string captionText)
    {
        var segments = new List<TranscriptSegment>();
        var fullText = new StringBuilder();

        // Simple VTT/SRT parser
        var lines = captionText.Split('\n');
        TranscriptSegment? currentSegment = null;

        foreach (var line in lines)
        {
            // Parse timestamp line (e.g., "00:00:01.000 --> 00:00:05.000")
            var timestampMatch = Regex.Match(line, @"(\d{2}):(\d{2}):(\d{2})\.(\d{3})\s*-->\s*(\d{2}):(\d{2}):(\d{2})\.(\d{3})");
            if (timestampMatch.Success)
            {
                if (currentSegment != null)
                {
                    segments.Add(currentSegment);
                    fullText.AppendLine(currentSegment.Text);
                }

                var startHours = int.Parse(timestampMatch.Groups[1].Value);
                var startMinutes = int.Parse(timestampMatch.Groups[2].Value);
                var startSeconds = int.Parse(timestampMatch.Groups[3].Value);
                var startMillis = int.Parse(timestampMatch.Groups[4].Value);

                var endHours = int.Parse(timestampMatch.Groups[5].Value);
                var endMinutes = int.Parse(timestampMatch.Groups[6].Value);
                var endSeconds = int.Parse(timestampMatch.Groups[7].Value);
                var endMillis = int.Parse(timestampMatch.Groups[8].Value);

                var start = new TimeSpan(0, startHours, startMinutes, startSeconds, startMillis);
                var end = new TimeSpan(0, endHours, endMinutes, endSeconds, endMillis);

                currentSegment = new TranscriptSegment
                {
                    Start = start,
                    Duration = end - start,
                    Text = ""
                };
            }
            else if (!string.IsNullOrWhiteSpace(line) && currentSegment != null && !Regex.IsMatch(line, @"^\d+$"))
            {
                // Append text to current segment
                currentSegment.Text += line + " ";
            }
        }

        if (currentSegment != null)
        {
            segments.Add(currentSegment);
            fullText.AppendLine(currentSegment.Text);
        }

        return new YouTubeTranscript
        {
            VideoId = videoId,
            VideoUrl = $"https://www.youtube.com/watch?v={videoId}",
            Segments = segments,
            FullText = fullText.ToString(),
            ExtractedAt = DateTime.UtcNow
        };
    }
}

