namespace GA.Business.Core.Microservices.Microservices;

using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

/// <summary>
///     Monadic HTTP client wrapper for type-safe HTTP operations
///     Uses Try and Result monads for error handling
/// </summary>
public class MonadicHttpClient(HttpClient httpClient, ILogger<MonadicHttpClient> logger)
{
    /// <summary>
    ///     GET request returning Try monad
    /// </summary>
    public async Task<Try<T>> GetAsync<T>(string url, CancellationToken cancellationToken = default)
    {
        return await Try.OfAsync(async () =>
        {
            logger.LogDebug("GET request to {Url}", url);

            var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken);

            if (result == null)
            {
                throw new InvalidOperationException($"Failed to deserialize response from {url}");
            }

            logger.LogDebug("GET request to {Url} succeeded", url);
            return result;
        });
    }

    /// <summary>
    ///     GET request returning Result monad with typed error
    /// </summary>
    public async Task<Result<T, HttpError>> GetWithResultAsync<T>(
        string url,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("GET request to {Url}", url);

            var response = await httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return new Result<T, HttpError>.Failure(new HttpError(
                    (int)response.StatusCode,
                    response.ReasonPhrase ?? "Unknown error",
                    errorContent,
                    url
                ));
            }

            var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken);

            if (result == null)
            {
                return new Result<T, HttpError>.Failure(new HttpError(
                    500,
                    "Deserialization failed",
                    "Response body was null or could not be deserialized",
                    url
                ));
            }

            logger.LogDebug("GET request to {Url} succeeded", url);
            return new Result<T, HttpError>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GET request to {Url} failed", url);
            return new Result<T, HttpError>.Failure(new HttpError(
                0,
                "Exception",
                ex.Message,
                url
            ));
        }
    }

    /// <summary>
    ///     POST request returning Try monad
    /// </summary>
    public async Task<Try<TResponse>> PostAsync<TRequest, TResponse>(
        string url,
        TRequest content,
        CancellationToken cancellationToken = default)
    {
        return await Try.OfAsync(async () =>
        {
            logger.LogDebug("POST request to {Url}", url);

            var response = await httpClient.PostAsJsonAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);

            if (result == null)
            {
                throw new InvalidOperationException($"Failed to deserialize response from {url}");
            }

            logger.LogDebug("POST request to {Url} succeeded", url);
            return result;
        });
    }

    /// <summary>
    ///     POST request returning Result monad with typed error
    /// </summary>
    public async Task<Result<TResponse, HttpError>> PostWithResultAsync<TRequest, TResponse>(
        string url,
        TRequest content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("POST request to {Url}", url);

            var response = await httpClient.PostAsJsonAsync(url, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return new Result<TResponse, HttpError>.Failure(new HttpError(
                    (int)response.StatusCode,
                    response.ReasonPhrase ?? "Unknown error",
                    errorContent,
                    url
                ));
            }

            var result = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);

            if (result == null)
            {
                return new Result<TResponse, HttpError>.Failure(new HttpError(
                    500,
                    "Deserialization failed",
                    "Response body was null or could not be deserialized",
                    url
                ));
            }

            logger.LogDebug("POST request to {Url} succeeded", url);
            return new Result<TResponse, HttpError>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "POST request to {Url} failed", url);
            return new Result<TResponse, HttpError>.Failure(new HttpError(
                0,
                "Exception",
                ex.Message,
                url
            ));
        }
    }

    /// <summary>
    ///     GET request with retry logic
    /// </summary>
    public async Task<Try<T>> GetWithRetryAsync<T>(
        string url,
        int maxAttempts = 3,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        var retryDelay = delay ?? TimeSpan.FromSeconds(1);
        var attempts = 0;

        while (attempts < maxAttempts)
        {
            var result = await GetAsync<T>(url, cancellationToken);
            if (result is Try<T>.Success)
            {
                return result;
            }

            attempts++;
            if (attempts < maxAttempts)
            {
                await Task.Delay(retryDelay, cancellationToken);
            }
        }

        return await GetAsync<T>(url, cancellationToken);
    }

    /// <summary>
    ///     Batch GET requests returning list of results
    /// </summary>
    public async Task<List<Try<T>>> GetBatchAsync<T>(
        IEnumerable<string> urls,
        CancellationToken cancellationToken = default)
    {
        var tasks = urls.Select(url => GetAsync<T>(url, cancellationToken));
        return (await Task.WhenAll(tasks)).ToList();
    }

    /// <summary>
    ///     GET request with lazy evaluation
    /// </summary>
    public Lazy<Task<Try<T>>> GetLazy<T>(string url, CancellationToken cancellationToken = default)
    {
        return new Lazy<Task<Try<T>>>(() => GetAsync<T>(url, cancellationToken));
    }
}

/// <summary>
///     HTTP error details
/// </summary>
public record HttpError(
    int StatusCode,
    string ReasonPhrase,
    string Content,
    string Url)
{
    public bool IsClientError => StatusCode >= 400 && StatusCode < 500;
    public bool IsServerError => StatusCode >= 500;
    public bool IsNetworkError => StatusCode == 0;

    public override string ToString()
    {
        return $"HTTP {StatusCode} {ReasonPhrase} at {Url}: {Content}";
    }
}

/// <summary>
///     Extension methods for MonadicHttpClient
/// </summary>
public static class MonadicHttpClientExtensions
{
    /// <summary>
    ///     Create MonadicHttpClient from IHttpClientFactory
    /// </summary>
    public static MonadicHttpClient CreateMonadicClient(
        this IHttpClientFactory factory,
        string name,
        ILogger<MonadicHttpClient> logger)
    {
        var httpClient = factory.CreateClient(name);
        return new MonadicHttpClient(httpClient, logger);
    }

    /// <summary>
    ///     Create MonadicHttpClient from HttpClient
    /// </summary>
    public static MonadicHttpClient ToMonadic(
        this HttpClient httpClient,
        ILogger<MonadicHttpClient> logger)
    {
        return new MonadicHttpClient(httpClient, logger);
    }
}
