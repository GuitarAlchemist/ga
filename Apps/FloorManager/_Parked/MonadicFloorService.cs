namespace FloorManager.Services;

using System.Text.Json;
using GA.Business.Microservices;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Monadic version of FloorService using Try/Result monads
/// Demonstrates functional error handling and type safety
/// </summary>
public class MonadicFloorService : MonadicServiceBase<MonadicFloorService>
{
    private readonly MonadicHttpClient _httpClient;

    public MonadicFloorService(
        IHttpClientFactory httpClientFactory,
        ILogger<MonadicFloorService> logger,
        IMemoryCache cache)
        : base(logger, cache)
    {
        _httpClient = _httpClient = httpClientFactory.CreateMonadicClient("GaApi",
            logger as ILogger<MonadicHttpClient> ?? throw new ArgumentException("Invalid logger type"));
    }

    /// <summary>
    /// Get floor using Try monad - returns Try&lt;FloorData&gt;
    /// </summary>
    public async Task<Try<FloorData>> GetFloorAsync(
        int floorNumber,
        int floorSize = 80,
        int? seed = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"floor_{floorNumber}_{floorSize}_{seed}";

        // Try to get from cache first
        var cachedFloor = GetFromCache<FloorData>(cacheKey);
        if (cachedFloor is Option<FloorData>.Some some)
        {
            Logger.LogDebug("Cache hit for floor {FloorNumber}", floorNumber);
            return Try.Success(some.Value);
        }

        // Build URL
        var url = BuildFloorUrl(floorNumber, floorSize, seed);

        // Make HTTP request with Try monad
        var result = await _httpClient.GetAsync<ApiResponse>(url, cancellationToken);

        // Map the result to extract FloorData
        return result.Map(apiResponse =>
        {
            if (apiResponse.Data == null)
                throw new InvalidOperationException($"Floor {floorNumber} data is null");

            // Cache the result
            Cache?.Set(cacheKey, apiResponse.Data, TimeSpan.FromMinutes(10));

            return apiResponse.Data;
        });
    }

    /// <summary>
    /// Get floor using Result monad - returns Result&lt;FloorData, FloorError&gt;
    /// </summary>
    public async Task<Result<FloorData, FloorError>> GetFloorWithResultAsync(
        int floorNumber,
        int floorSize = 80,
        int? seed = null,
        CancellationToken cancellationToken = default)
    {
        // Validate input
        var validation = ValidateFloorRequest(floorNumber, floorSize);
        if (validation is Validation<(int, int), ValidationError>.Failure failure)
        {
            return new Result<FloorData, FloorError>.Failure(new FloorError(
                FloorErrorType.ValidationError,
                $"Validation failed: {string.Join(", ", failure.Errors.Select(e => e.Message))}"
            ));
        }

        var cacheKey = $"floor_{floorNumber}_{floorSize}_{seed}";

        // Try to get from cache first
        var cachedFloor = GetFromCache<FloorData>(cacheKey);
        if (cachedFloor is Option<FloorData>.Some some)
        {
            Logger.LogDebug("Cache hit for floor {FloorNumber}", floorNumber);
            return new Result<FloorData, FloorError>.Success(some.Value);
        }

        // Build URL
        var url = BuildFloorUrl(floorNumber, floorSize, seed);

        // Make HTTP request with Result monad
        var httpResult = await _httpClient.GetWithResultAsync<ApiResponse>(url, cancellationToken);

        // Map HTTP errors to FloorErrors
        return httpResult.Match(
            onSuccess: apiResponse =>
            {
                if (apiResponse.Data == null)
                {
                    return new Result<FloorData, FloorError>.Failure(new FloorError(
                        FloorErrorType.DataNotFound,
                        $"Floor {floorNumber} data is null"
                    ));
                }

                // Cache the result
                Cache?.Set(cacheKey, apiResponse.Data, TimeSpan.FromMinutes(10));

                return new Result<FloorData, FloorError>.Success(apiResponse.Data);
            },
            onFailure: httpError =>
            {
                var errorType = httpError.StatusCode switch
                {
                    404 => FloorErrorType.NotFound,
                    >= 500 => FloorErrorType.ServerError,
                    >= 400 => FloorErrorType.ClientError,
                    _ => FloorErrorType.NetworkError
                };

                return new Result<FloorData, FloorError>.Failure(new FloorError(
                    errorType,
                    $"HTTP {httpError.StatusCode}: {httpError.ReasonPhrase}"
                ));
            }
        );
    }

    /// <summary>
    /// Get all floors using monadic composition
    /// Returns Result with list of floors and any errors encountered
    /// </summary>
    public async Task<Result<List<FloorData>, List<FloorError>>> GetAllFloorsAsync(
        int floorSize = 80,
        int? seed = null,
        CancellationToken cancellationToken = default)
    {
        var floorNumbers = Enumerable.Range(0, 6);
        var results = new List<FloorData>();
        var errors = new List<FloorError>();

        foreach (var floorNumber in floorNumbers)
        {
            var result = await GetFloorWithResultAsync(floorNumber, floorSize, seed, cancellationToken);

            result.Match(
                onSuccess: floor => results.Add(floor),
                onFailure: error => errors.Add(error)
            );
        }

        // Return success if we got at least some floors, otherwise failure
        return results.Any()
            ? new Result<List<FloorData>, List<FloorError>>.Success(results)
            : new Result<List<FloorData>, List<FloorError>>.Failure(errors);
    }

    /// <summary>
    /// Get floor with retry logic using IO monad
    /// </summary>
    public IO<Try<FloorData>> GetFloorWithRetry(
        int floorNumber,
        int floorSize = 80,
        int? seed = null,
        int maxAttempts = 3)
    {
        return IO.Of(async () =>
        {
            return await GetFloorAsync(floorNumber, floorSize, seed);
        }).Retry(maxAttempts, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Get floor lazily - only fetches when Value is accessed
    /// </summary>
    public LazyM<Task<Try<FloorData>>> GetFloorLazy(
        int floorNumber,
        int floorSize = 80,
        int? seed = null)
    {
        return LazyM.Of(async () => await GetFloorAsync(floorNumber, floorSize, seed));
    }

    /// <summary>
    /// Validate floor request parameters
    /// </summary>
    private Validation<(int floorNumber, int floorSize), ValidationError> ValidateFloorRequest(
        int floorNumber,
        int floorSize)
    {
        var floorNumberValidation = ValidationHelpers.InRange(floorNumber, 0, 100, "FloorNumber");
        var floorSizeValidation = ValidationHelpers.InRange(floorSize, 10, 200, "FloorSize");

        // Combine validations - accumulates all errors
        var errors = new List<ValidationError>();

        if (floorNumberValidation is Validation<int, ValidationError>.Failure fnf)
            errors.AddRange(fnf.Errors);

        if (floorSizeValidation is Validation<int, ValidationError>.Failure fsf)
            errors.AddRange(fsf.Errors);

        return errors.Any()
            ? new Validation<(int, int), ValidationError>.Failure(errors)
            : new Validation<(int, int), ValidationError>.Success((floorNumber, floorSize));
    }

    /// <summary>
    /// Build floor URL
    /// </summary>
    private static string BuildFloorUrl(int floorNumber, int floorSize, int? seed)
    {
        var url = $"/api/music-rooms/floor/{floorNumber}?floorSize={floorSize}";
        if (seed.HasValue)
        {
            url += $"&seed={seed.Value}";
        }
        return url;
    }
}

/// <summary>
/// Floor error types
/// </summary>
public enum FloorErrorType
{
    ValidationError,
    NotFound,
    ClientError,
    ServerError,
    NetworkError,
    DataNotFound
}

/// <summary>
/// Floor error details
/// </summary>
public record FloorError(FloorErrorType Type, string Message)
{
    public override string ToString() => $"[{Type}] {Message}";
}

/// <summary>
/// Extension methods for FloorService results
/// </summary>
public static class FloorServiceExtensions
{
    /// <summary>
    /// Convert Try&lt;FloorData&gt; to nullable FloorData (for backward compatibility)
    /// </summary>
    public static FloorData? ToNullable(this Try<FloorData> tryFloor)
    {
        return tryFloor.Match(
            onSuccess: floor => floor,
            onFailure: _ => null
        );
    }

    /// <summary>
    /// Convert Result&lt;FloorData, FloorError&gt; to nullable FloorData
    /// </summary>
    public static FloorData? ToNullable(this Result<FloorData, FloorError> result)
    {
        return result.Match(
            onSuccess: floor => floor,
            onFailure: _ => null
        );
    }

    /// <summary>
    /// Log floor errors
    /// </summary>
    public static Result<FloorData, FloorError> LogErrors(
        this Result<FloorData, FloorError> result,
        ILogger logger)
    {
        result.Match(
            onSuccess: _ => { },
            onFailure: error => logger.LogError("Floor error: {Error}", error)
        );
        return result;
    }
}

