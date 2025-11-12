namespace GA.DocumentProcessing.Service.Services;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GA.DocumentProcessing.Service.Models;

/// <summary>
/// Service for interacting with Google NotebookLM Podcast API
/// </summary>
public class NotebookLMService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotebookLMService> _logger;
    private readonly string _projectId;
    private readonly string _baseUrl = "https://discoveryengine.googleapis.com/v1";

    public NotebookLMService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<NotebookLMService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("GoogleCloud");
        _logger = logger;
        _projectId = configuration["GoogleCloud:ProjectId"] 
            ?? throw new InvalidOperationException("GoogleCloud:ProjectId not configured");
    }

    /// <summary>
    /// Generate a podcast from source documents using NotebookLM Podcast API
    /// </summary>
    public async Task<PodcastGenerationResult> GeneratePodcastAsync(
        PodcastRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating podcast: {Title}", request.Title);

            // Build the API request
            var apiRequest = new
            {
                podcastConfig = new
                {
                    focus = request.Focus,
                    length = request.Length.ToString().ToUpper(),
                    languageCode = request.LanguageCode ?? "en"
                },
                contexts = request.Contexts.Select(c => new Dictionary<string, object>
                {
                    { c.Type == "text" ? "text" : "blob", c.Content }
                }).ToArray(),
                title = request.Title,
                description = request.Description
            };

            var json = JsonSerializer.Serialize(apiRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Call the Podcast API
            var url = $"{_baseUrl}/projects/{_projectId}/locations/global/podcasts";
            var response = await _httpClient.PostAsync(url, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Podcast generation failed: {Error}", error);
                throw new HttpRequestException($"Podcast generation failed: {error}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<PodcastOperationResponse>(responseJson);

            if (result?.Name == null)
            {
                throw new InvalidOperationException("Invalid response from Podcast API");
            }

            _logger.LogInformation("Podcast generation started: {OperationName}", result.Name);

            return new PodcastGenerationResult
            {
                OperationName = result.Name,
                Status = "PENDING",
                Title = request.Title,
                Description = request.Description
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating podcast");
            throw;
        }
    }

    /// <summary>
    /// Poll the status of a podcast generation operation
    /// </summary>
    public async Task<PodcastOperationStatus> GetOperationStatusAsync(
        string operationName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_baseUrl}/{operationName}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to get operation status: {Error}", error);
                throw new HttpRequestException($"Failed to get operation status: {error}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var operation = JsonSerializer.Deserialize<PodcastOperationResponse>(json);

            return new PodcastOperationStatus
            {
                OperationName = operationName,
                Done = operation?.Done ?? false,
                Error = operation?.Error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting operation status");
            throw;
        }
    }

    /// <summary>
    /// Download a completed podcast
    /// </summary>
    public async Task<byte[]> DownloadPodcastAsync(
        string operationName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Downloading podcast: {OperationName}", operationName);

            var url = $"{_baseUrl}/{operationName}:download?alt=media";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Podcast download failed: {Error}", error);
                throw new HttpRequestException($"Podcast download failed: {error}");
            }

            var podcastData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            _logger.LogInformation("Downloaded podcast: {Size} bytes", podcastData.Length);

            return podcastData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading podcast");
            throw;
        }
    }

    /// <summary>
    /// Generate and wait for podcast completion (convenience method)
    /// </summary>
    public async Task<byte[]> GenerateAndDownloadPodcastAsync(
        PodcastRequest request,
        TimeSpan? maxWaitTime = null,
        CancellationToken cancellationToken = default)
    {
        var maxWait = maxWaitTime ?? TimeSpan.FromMinutes(10);
        var pollInterval = TimeSpan.FromSeconds(10);
        var startTime = DateTime.UtcNow;

        // Start generation
        var result = await GeneratePodcastAsync(request, cancellationToken);

        // Poll until complete or timeout
        while (DateTime.UtcNow - startTime < maxWait)
        {
            await Task.Delay(pollInterval, cancellationToken);

            var status = await GetOperationStatusAsync(result.OperationName, cancellationToken);

            if (status.Done)
            {
                if (status.Error != null)
                {
                    throw new InvalidOperationException($"Podcast generation failed: {status.Error}");
                }

                // Download the podcast
                return await DownloadPodcastAsync(result.OperationName, cancellationToken);
            }

            _logger.LogInformation("Podcast generation in progress... ({Elapsed}s elapsed)",
                (DateTime.UtcNow - startTime).TotalSeconds);
        }

        throw new TimeoutException($"Podcast generation timed out after {maxWait.TotalMinutes} minutes");
    }
}

/// <summary>
/// Request model for podcast generation
/// </summary>
public class PodcastRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Focus { get; set; }
    public PodcastLength Length { get; set; } = PodcastLength.Standard;
    public string? LanguageCode { get; set; }
    public List<PodcastContext> Contexts { get; set; } = new();
}

/// <summary>
/// Context input for podcast generation
/// </summary>
public class PodcastContext
{
    public string Type { get; set; } = "text"; // "text" or "blob"
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Podcast length options
/// </summary>
public enum PodcastLength
{
    Short,    // 4-5 minutes
    Standard  // ~10 minutes
}

/// <summary>
/// Result of podcast generation request
/// </summary>
public class PodcastGenerationResult
{
    public string OperationName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Status of a podcast generation operation
/// </summary>
public class PodcastOperationStatus
{
    public string OperationName { get; set; } = string.Empty;
    public bool Done { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Response from Podcast API
/// </summary>
internal class PodcastOperationResponse
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

