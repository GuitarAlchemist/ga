namespace GA.Business.AI.AI.HuggingFace;

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;

/// <summary>
///     HTTP client for Hugging Face Inference API
/// </summary>
public class HuggingFaceClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<HuggingFaceClient> _logger;
    private readonly HuggingFaceSettings _settings;

    public HuggingFaceClient(
        HttpClient httpClient,
        ILogger<HuggingFaceClient> logger,
        IOptions<HuggingFaceSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        // Set authorization header if API token is provided
        if (!string.IsNullOrEmpty(_settings.ApiToken))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiToken}");
        }
    }

    /// <summary>
    ///     Generate audio from text using a Hugging Face model
    /// </summary>
    public virtual async Task<TextToAudioResponse> GenerateAudioAsync(
        string modelId,
        TextToAudioRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating audio with model {ModelId}: {Inputs}",
                modelId, request.Inputs.Substring(0, Math.Min(50, request.Inputs.Length)));

            var endpoint = $"/models/{modelId}";
            var jsonContent = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

            // Handle model loading (503 with estimated_time)
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var error = JsonSerializer.Deserialize<HuggingFaceError>(errorContent, _jsonOptions);

                if (error?.EstimatedTime > 0)
                {
                    _logger.LogInformation("Model {ModelId} is loading, estimated time: {EstimatedTime}s",
                        modelId, error.EstimatedTime);

                    // Wait for model to load and retry
                    await Task.Delay(TimeSpan.FromSeconds(error.EstimatedTime.Value + 2), cancellationToken);
                    response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
                }
            }

            response.EnsureSuccessStatusCode();

            var audioData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "audio/wav";

            _logger.LogInformation("Audio generated successfully: {Size} bytes, type: {ContentType}",
                audioData.Length, contentType);

            return new TextToAudioResponse(audioData, contentType, audioData.Length);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling Hugging Face API for model {ModelId}", modelId);
            throw new InvalidOperationException($"Failed to generate audio with model {modelId}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating audio with model {ModelId}", modelId);
            throw;
        }
    }

    /// <summary>
    ///     Get model information
    /// </summary>
    public async Task<ModelInfo?> GetModelInfoAsync(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting info for model {ModelId}", modelId);

            var response = await _httpClient.GetAsync($"/api/models/{modelId}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var modelInfo = await response.Content.ReadFromJsonAsync<ModelInfo>(_jsonOptions, cancellationToken);

            _logger.LogInformation("Model info retrieved: {ModelId}, task: {Task}",
                modelInfo?.ModelId, modelInfo?.Task);

            return modelInfo;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get model info for {ModelId}", modelId);
            return null;
        }
    }

    /// <summary>
    ///     Check if a model is available and loaded
    /// </summary>
    public virtual async Task<bool> IsModelAvailableAsync(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var testRequest = new TextToAudioRequest("test");
            var jsonContent = JsonSerializer.Serialize(testRequest, _jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"/models/{modelId}", content, cancellationToken);

            return response.IsSuccessStatusCode ||
                   response.StatusCode == HttpStatusCode.ServiceUnavailable;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Health check for Hugging Face API
    /// </summary>
    public virtual async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
