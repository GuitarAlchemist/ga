namespace GA.Business.AI.AI.HandPose;

using System.Net.Http.Headers;
using System.Text.Json;

/// <summary>
///     HTTP client for HandPoseService API
/// </summary>
public class HandPoseClient(HttpClient httpClient, ILogger<HandPoseClient> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    ///     Detect hand pose from image bytes
    /// </summary>
    public async Task<HandPoseResponse> InferAsync(byte[] imageData, string fileName = "image.jpg",
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Sending image to HandPoseService for inference");

            using var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(imageData);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(imageContent, "file", fileName);

            var response = await httpClient.PostAsync("/v1/handpose/infer", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<HandPoseResponse>(_jsonOptions, cancellationToken);

            if (result == null)
            {
                throw new InvalidOperationException("Failed to deserialize HandPoseResponse");
            }

            logger.LogInformation("Hand pose inference completed: {HandCount} hands detected in {ProcessingTime}ms",
                result.Hands.Count, result.ProcessingTimeMs);

            return result;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error calling HandPoseService");
            throw new InvalidOperationException("Failed to call HandPoseService", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during hand pose inference");
            throw;
        }
    }

    /// <summary>
    ///     Detect hand pose from image stream
    /// </summary>
    public async Task<HandPoseResponse> InferAsync(Stream imageStream, string fileName = "image.jpg",
        CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream, cancellationToken);
        return await InferAsync(memoryStream.ToArray(), fileName, cancellationToken);
    }

    /// <summary>
    ///     Map hand pose to guitar string/fret positions
    /// </summary>
    public async Task<GuitarMappingResponse> MapToGuitarAsync(
        HandPoseResponse handPose,
        NeckConfig? neckConfig = null,
        string handToMap = "left",
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Mapping hand pose to guitar positions (hand: {Hand})", handToMap);

            var request = new GuitarMappingRequest(
                handPose,
                neckConfig ?? new NeckConfig(),
                handToMap
            );

            var response =
                await httpClient.PostAsJsonAsync("/v1/handpose/guitar-map", request, _jsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result =
                await response.Content.ReadFromJsonAsync<GuitarMappingResponse>(_jsonOptions, cancellationToken);

            if (result == null)
            {
                throw new InvalidOperationException("Failed to deserialize GuitarMappingResponse");
            }

            logger.LogInformation("Guitar mapping completed: {PositionCount} positions found using {Method}",
                result.Positions.Count, result.MappingMethod);

            return result;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error calling HandPoseService guitar mapping");
            throw new InvalidOperationException("Failed to map hand pose to guitar", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during guitar mapping");
            throw;
        }
    }

    /// <summary>
    ///     Check service health
    /// </summary>
    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync("/healthz", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
