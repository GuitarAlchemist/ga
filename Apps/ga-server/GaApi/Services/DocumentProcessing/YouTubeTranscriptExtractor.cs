namespace GaApi.Services.DocumentProcessing;

using System.Text;
using System.Text.Json;

/// <summary>
/// Extracts transcripts from YouTube videos using youtube-transcript-api or similar free services
/// </summary>
public class YouTubeTranscriptExtractor
{
    private readonly ILogger<YouTubeTranscriptExtractor> _logger;
    private readonly HttpClient _httpClient;

    public YouTubeTranscriptExtractor(
        ILogger<YouTubeTranscriptExtractor> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    /// <summary>
    /// Extract transcript from a YouTube video
    /// </summary>
    public async Task<string> ExtractTranscriptAsync(
        string videoUrl,
        CancellationToken cancellationToken = default)
    {
        var videoId = ExtractVideoId(videoUrl);
        if (string.IsNullOrEmpty(videoId))
        {
            throw new ArgumentException("Invalid YouTube URL", nameof(videoUrl));
        }

        _logger.LogInformation("Extracting transcript for video: {VideoId}", videoId);

        try
        {
            // Try multiple methods to get transcript
            
            // Method 1: Try using Invidious API (same as YouTube search)
            var transcript = await TryInvidiousTranscriptAsync(videoId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(transcript))
            {
                return transcript;
            }

            // Method 2: Try using YouTube's timedtext API directly
            transcript = await TryYouTubeTimedTextAsync(videoId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(transcript))
            {
                return transcript;
            }

            _logger.LogWarning("No transcript available for video: {VideoId}", videoId);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting transcript for video: {VideoId}", videoId);
            throw;
        }
    }

    /// <summary>
    /// Extract video ID from YouTube URL
    /// </summary>
    private string ExtractVideoId(string url)
    {
        try
        {
            // Handle different YouTube URL formats
            // https://www.youtube.com/watch?v=VIDEO_ID
            // https://youtu.be/VIDEO_ID
            // https://www.youtube.com/embed/VIDEO_ID

            if (url.Contains("youtube.com/watch"))
            {
                var uri = new Uri(url);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                return query["v"] ?? string.Empty;
            }
            else if (url.Contains("youtu.be/"))
            {
                var uri = new Uri(url);
                return uri.Segments.Last().TrimEnd('/');
            }
            else if (url.Contains("youtube.com/embed/"))
            {
                var uri = new Uri(url);
                return uri.Segments.Last().TrimEnd('/');
            }

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Try to get transcript using Invidious API
    /// </summary>
    private async Task<string> TryInvidiousTranscriptAsync(
        string videoId,
        CancellationToken cancellationToken)
    {
        var invidiousInstances = new[]
        {
            "https://invidious.snopyta.org",
            "https://yewtu.be",
            "https://invidious.kavin.rocks",
            "https://vid.puffyan.us"
        };

        foreach (var instance in invidiousInstances)
        {
            try
            {
                var url = $"{instance}/api/v1/captions/{videoId}";
                var response = await _httpClient.GetAsync(url, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var captions = JsonSerializer.Deserialize<JsonElement>(json);

                // Look for English captions
                if (captions.ValueKind == JsonValueKind.Array)
                {
                    foreach (var caption in captions.EnumerateArray())
                    {
                        if (caption.TryGetProperty("language_code", out var langCode) &&
                            (langCode.GetString() == "en" || langCode.GetString() == "en-US"))
                        {
                            if (caption.TryGetProperty("url", out var captionUrl))
                            {
                                var transcriptUrl = instance + captionUrl.GetString();
                                var transcriptResponse = await _httpClient.GetAsync(transcriptUrl, cancellationToken);
                                
                                if (transcriptResponse.IsSuccessStatusCode)
                                {
                                    var transcriptXml = await transcriptResponse.Content.ReadAsStringAsync(cancellationToken);
                                    return ParseTranscriptXml(transcriptXml);
                                }
                            }
                        }
                    }
                }

                _logger.LogInformation("Successfully retrieved transcript from {Instance}", instance);
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get transcript from Invidious instance: {Instance}", instance);
                continue;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Try to get transcript using YouTube's timedtext API directly
    /// </summary>
    private async Task<string> TryYouTubeTimedTextAsync(
        string videoId,
        CancellationToken cancellationToken)
    {
        try
        {
            // This is a simplified approach - in production you'd need to:
            // 1. Get the video page HTML
            // 2. Extract the caption tracks from the player config
            // 3. Download the caption track

            var videoPageUrl = $"https://www.youtube.com/watch?v={videoId}";
            var response = await _httpClient.GetAsync(videoPageUrl, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Look for caption tracks in the HTML
            // This is a simplified extraction - production code would be more robust
            var captionTrackMatch = System.Text.RegularExpressions.Regex.Match(
                html,
                @"""captionTracks"":\[(.*?)\]");

            if (captionTrackMatch.Success)
            {
                var captionTracksJson = "[" + captionTrackMatch.Groups[1].Value + "]";
                var tracks = JsonSerializer.Deserialize<JsonElement>(captionTracksJson);

                foreach (var track in tracks.EnumerateArray())
                {
                    if (track.TryGetProperty("languageCode", out var langCode) &&
                        (langCode.GetString() == "en" || langCode.GetString() == "en-US"))
                    {
                        if (track.TryGetProperty("baseUrl", out var baseUrl))
                        {
                            var transcriptUrl = baseUrl.GetString();
                            var transcriptResponse = await _httpClient.GetAsync(transcriptUrl, cancellationToken);
                            
                            if (transcriptResponse.IsSuccessStatusCode)
                            {
                                var transcriptXml = await transcriptResponse.Content.ReadAsStringAsync(cancellationToken);
                                return ParseTranscriptXml(transcriptXml);
                            }
                        }
                    }
                }
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get transcript from YouTube timedtext API");
            return string.Empty;
        }
    }

    /// <summary>
    /// Parse transcript XML and extract text
    /// </summary>
    private string ParseTranscriptXml(string xml)
    {
        try
        {
            var sb = new StringBuilder();
            
            // Simple XML parsing - extract text from <text> tags
            var textMatches = System.Text.RegularExpressions.Regex.Matches(
                xml,
                @"<text[^>]*>(.*?)</text>",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            foreach (System.Text.RegularExpressions.Match match in textMatches)
            {
                var text = match.Groups[1].Value;
                
                // Decode HTML entities
                text = System.Web.HttpUtility.HtmlDecode(text);
                
                // Remove newlines and extra spaces
                text = text.Replace("\n", " ").Trim();
                
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.Append(text);
                    sb.Append(" ");
                }
            }

            return sb.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing transcript XML");
            return string.Empty;
        }
    }
}

