namespace GA.Business.AI.AI.SoundBank;

using System.Text.Json;

/// <summary>
///     HTTP client for SoundBankService API with async job polling and caching
/// </summary>
public class SoundBankClient(HttpClient httpClient, ILogger<SoundBankClient> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    ///     Queue a sound generation job
    /// </summary>
    public async Task<JobResponse> GenerateSoundAsync(
        SoundGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Queueing sound generation: {Instrument} string={String} fret={Fret}",
                request.Instrument, request.String, request.Fret);

            var response =
                await httpClient.PostAsJsonAsync("/v1/sounds/generate", request, _jsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<JobResponse>(_jsonOptions, cancellationToken);

            if (result == null)
            {
                throw new InvalidOperationException("Failed to deserialize JobResponse");
            }

            logger.LogInformation("Sound generation job created: {JobId} (estimated {EstimatedSeconds}s)",
                result.JobId, result.EstimatedSeconds);

            return result;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error calling SoundBankService");
            throw new InvalidOperationException("Failed to queue sound generation", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error queueing sound generation");
            throw;
        }
    }

    /// <summary>
    ///     Get status of a generation job
    /// </summary>
    public async Task<JobStatusResponse> GetJobStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"/v1/sounds/jobs/{jobId}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<JobStatusResponse>(_jsonOptions, cancellationToken);

            if (result == null)
            {
                throw new InvalidOperationException("Failed to deserialize JobStatusResponse");
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error getting job status for {JobId}", jobId);
            throw new InvalidOperationException($"Failed to get job status for {jobId}", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting job status for {JobId}", jobId);
            throw;
        }
    }

    /// <summary>
    ///     Poll job until completion with exponential backoff
    /// </summary>
    public async Task<JobStatusResponse> WaitForJobCompletionAsync(
        string jobId,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var maxWait = timeout ?? TimeSpan.FromMinutes(5);
        var startTime = DateTime.UtcNow;
        var delay = TimeSpan.FromMilliseconds(500);
        var maxDelay = TimeSpan.FromSeconds(5);

        logger.LogInformation("Waiting for job {JobId} to complete (timeout: {Timeout})", jobId, maxWait);

        while (DateTime.UtcNow - startTime < maxWait)
        {
            var status = await GetJobStatusAsync(jobId, cancellationToken);

            if (status.Status == JobStatus.Completed)
            {
                logger.LogInformation("Job {JobId} completed successfully", jobId);
                return status;
            }

            if (status.Status == JobStatus.Failed)
            {
                logger.LogError("Job {JobId} failed: {Error}", jobId, status.ErrorMessage);
                throw new InvalidOperationException($"Job {jobId} failed: {status.ErrorMessage}");
            }

            logger.LogDebug("Job {JobId} status: {Status} ({Progress:P0})", jobId, status.Status, status.Progress);

            await Task.Delay(delay, cancellationToken);

            // Exponential backoff
            delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 1.5, maxDelay.TotalMilliseconds));
        }

        throw new TimeoutException($"Job {jobId} did not complete within {maxWait}");
    }

    /// <summary>
    ///     Generate sound and wait for completion
    /// </summary>
    public async Task<SoundSample> GenerateAndWaitAsync(
        SoundGenerationRequest request,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var jobResponse = await GenerateSoundAsync(request, cancellationToken);
        var completedJob = await WaitForJobCompletionAsync(jobResponse.JobId, timeout, cancellationToken);

        if (completedJob.Sample == null)
        {
            throw new InvalidOperationException($"Job {jobResponse.JobId} completed but no sample was returned");
        }

        return completedJob.Sample;
    }

    /// <summary>
    ///     Get sample metadata
    /// </summary>
    public async Task<SoundSample> GetSampleAsync(
        string sampleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"/v1/sounds/{sampleId}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SoundSample>(_jsonOptions, cancellationToken);

            if (result == null)
            {
                throw new InvalidOperationException("Failed to deserialize SoundSample");
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error getting sample {SampleId}", sampleId);
            throw new InvalidOperationException($"Failed to get sample {sampleId}", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting sample {SampleId}", sampleId);
            throw;
        }
    }

    /// <summary>
    ///     Download sample audio file
    /// </summary>
    public async Task<byte[]> DownloadSampleAsync(
        string sampleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Downloading sample {SampleId}", sampleId);

            var response = await httpClient.GetAsync($"/v1/sounds/{sampleId}/download", cancellationToken);
            response.EnsureSuccessStatusCode();

            var audioData = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            logger.LogInformation("Downloaded sample {SampleId}: {Size} bytes", sampleId, audioData.Length);

            return audioData;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error downloading sample {SampleId}", sampleId);
            throw new InvalidOperationException($"Failed to download sample {sampleId}", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error downloading sample {SampleId}", sampleId);
            throw;
        }
    }

    /// <summary>
    ///     Search for existing samples
    /// </summary>
    public async Task<SearchResponse> SearchSamplesAsync(
        SearchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Searching samples: {Request}", request);

            var response =
                await httpClient.PostAsJsonAsync("/v1/sounds/search", request, _jsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SearchResponse>(_jsonOptions, cancellationToken);

            if (result == null)
            {
                throw new InvalidOperationException("Failed to deserialize SearchResponse");
            }

            logger.LogInformation("Found {Count} samples", result.Total);

            return result;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error searching samples");
            throw new InvalidOperationException("Failed to search samples", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching samples");
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
