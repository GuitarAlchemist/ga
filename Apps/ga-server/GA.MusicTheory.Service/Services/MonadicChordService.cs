namespace GA.MusicTheory.Service.Services;

using Constants;
using GA.Core.Functional;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Models;

/// <summary>
///     Monadic chord service using Option and Result monads for type-safe operations
/// </summary>
public interface IMonadicChordService
{
    Task<Try<long>> GetTotalCountAsync();
    Task<Result<List<Chord>, ChordError>> GetByQualityAsync(string quality, int limit = 100);
    Task<Result<List<Chord>, ChordError>> GetByExtensionAsync(string extension, int limit = 100);
    Task<Result<List<Chord>, ChordError>> GetByStackingTypeAsync(string stackingType, int limit = 100);
    Task<Result<List<Chord>, ChordError>> SearchChordsAsync(string query, int limit = 100);
    Task<Option<Chord>> GetByIdAsync(string id);
    Task<Result<List<Chord>, ChordError>> GetSimilarChordsAsync(string chordId, int limit = 10);
    Task<Try<ChordStatistics>> GetStatisticsAsync();
    Task<Try<List<string>>> GetAvailableQualitiesAsync();
    Task<Try<List<string>>> GetAvailableExtensionsAsync();
    Task<Try<List<string>>> GetAvailableStackingTypesAsync();
}

public class MonadicChordService(
    MongoDbService mongoDb,
    IMemoryCache cache,
    ILogger<MonadicChordService> logger)
    : MonadicServiceBase<MonadicChordService>(logger, cache), IMonadicChordService
{
    public async Task<Try<long>> GetTotalCountAsync()
    {
        const string cacheKey = "chord_total_count";

        return await GetOrSetCacheWithTryAsync(
            cacheKey,
            async () => await mongoDb.GetTotalChordCountAsync(),
            TimeSpan.FromMinutes(5)
        );
    }

    public async Task<Result<List<Chord>, ChordError>> GetByQualityAsync(string quality, int limit = 100)
    {
        // Validate input
        var validation = ValidateQualityParameter(quality, limit);
        if (validation.IsInvalid)
        {
            return Result<List<Chord>, ChordError>.Failure(
                new ChordError(ChordErrorType.ValidationError, validation.Errors.First().Message)
            );
        }

        var cacheKey = $"chords_quality_{quality}_{limit}";

        var tryChords = await GetOrSetCacheWithTryAsync(
            cacheKey,
            async () => await mongoDb.GetChordsByQualityAsync(quality, limit),
            TimeSpan.FromMinutes(10)
        );

        return tryChords.Match<Result<List<Chord>, ChordError>>(
            onSuccess: chords => Result<List<Chord>, ChordError>.Success(chords),
            onFailure: ex => Result<List<Chord>, ChordError>.Failure(
                new ChordError(ChordErrorType.DatabaseError, ex.Message)
            )
        );
    }

    public async Task<Result<List<Chord>, ChordError>> GetByExtensionAsync(string extension, int limit = 100)
    {
        // Validate input
        var validation = ValidateExtensionParameter(extension, limit);
        if (validation.IsInvalid)
        {
            return Result<List<Chord>, ChordError>.Failure(
                new ChordError(ChordErrorType.ValidationError, validation.Errors.First().Message)
            );
        }

        var cacheKey = $"chords_extension_{extension}_{limit}";

        var tryChords = await GetOrSetCacheWithTryAsync(
            cacheKey,
            async () => await mongoDb.GetChordsByExtensionAsync(extension, limit),
            TimeSpan.FromMinutes(10)
        );

        return tryChords.Match<Result<List<Chord>, ChordError>>(
            onSuccess: chords => Result<List<Chord>, ChordError>.Success(chords),
            onFailure: ex => Result<List<Chord>, ChordError>.Failure(
                new ChordError(ChordErrorType.DatabaseError, ex.Message)
            )
        );
    }

    public async Task<Result<List<Chord>, ChordError>> GetByStackingTypeAsync(string stackingType, int limit = 100)
    {
        // Validate input
        var validation = ValidateStackingTypeParameter(stackingType, limit);
        if (validation.IsInvalid)
        {
            return Result<List<Chord>, ChordError>.Failure(
                new ChordError(ChordErrorType.ValidationError, validation.Errors.First().Message)
            );
        }

        var cacheKey = $"chords_stacking_{stackingType}_{limit}";

        var tryChords = await GetOrSetCacheWithTryAsync(
            cacheKey,
            async () => await mongoDb.GetChordsByStackingTypeAsync(stackingType, limit),
            TimeSpan.FromMinutes(10)
        );

        return tryChords.Match<Result<List<Chord>, ChordError>>(
            onSuccess: chords => Result<List<Chord>, ChordError>.Success(chords),
            onFailure: ex => Result<List<Chord>, ChordError>.Failure(
                new ChordError(ChordErrorType.DatabaseError, ex.Message)
            )
        );
    }

    public async Task<Result<List<Chord>, ChordError>> SearchChordsAsync(string query, int limit = 100)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(query))
        {
            return Result<List<Chord>, ChordError>.Failure(
                new ChordError(ChordErrorType.ValidationError, "Search query cannot be empty")
            );
        }

        if (limit <= 0 || limit > 1000)
        {
            return Result<List<Chord>, ChordError>.Failure(
                new ChordError(ChordErrorType.ValidationError, "Limit must be between 1 and 1000")
            );
        }

        var cacheKey = CacheKeys.ChordSearch(query, limit);

        var tryChords = await GetOrSetCacheWithTryAsync(
            cacheKey,
            async () => await mongoDb.SearchChordsAsync(query, limit),
            CacheKeys.Durations.ChordSearch
        );

        return tryChords.Match<Result<List<Chord>, ChordError>>(
            onSuccess: chords => Result<List<Chord>, ChordError>.Success(chords),
            onFailure: ex => Result<List<Chord>, ChordError>.Failure(
                new ChordError(ChordErrorType.DatabaseError, ex.Message)
            )
        );
    }

    public async Task<Option<Chord>> GetByIdAsync(string id)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(id))
        {
            return Option<Chord>.None;
        }

        var cacheKey = $"chord_{id}";

        // Try to get from cache first
        var cachedChord = GetFromCache<Chord>(cacheKey);
        if (cachedChord.IsSome)
        {
            return cachedChord;
        }

        // Get from database
        var tryChord = await ExecuteAsync(
            async () => await mongoDb.GetChordByIdAsync(id),
            $"GetChordById({id})"
        );

        return tryChord.Match<Option<Chord>>(
            onSuccess: chord =>
            {
                if (chord != null)
                {
                    Cache?.Set(cacheKey, chord, TimeSpan.FromMinutes(30));
                    return Option<Chord>.Some(chord);
                }

                return Option<Chord>.None;
            },
            onFailure: ex =>
            {
                Logger.LogError(ex, "Failed to get chord by ID: {Id}", id);
                return Option<Chord>.None;
            }
        );
    }

    public async Task<Result<List<Chord>, ChordError>> GetSimilarChordsAsync(string chordId, int limit = 10)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(chordId))
        {
            return Result<List<Chord>, ChordError>.Failure(
                new ChordError(ChordErrorType.ValidationError, "Chord ID cannot be empty")
            );
        }

        if (limit <= 0 || limit > 100)
        {
            return Result<List<Chord>, ChordError>.Failure(
                new ChordError(ChordErrorType.ValidationError, "Limit must be between 1 and 100")
            );
        }

        var cacheKey = $"similar_chords_{chordId}_{limit}";

        var tryChords = await GetOrSetCacheWithTryAsync(
            cacheKey,
            async () => await mongoDb.GetSimilarChordsAsync(chordId, limit),
            TimeSpan.FromMinutes(15)
        );

        return tryChords.Match<Result<List<Chord>, ChordError>>(
            onSuccess: chords => Result<List<Chord>, ChordError>.Success(chords),
            onFailure: ex => Result<List<Chord>, ChordError>.Failure(
                new ChordError(ChordErrorType.DatabaseError, ex.Message)
            )
        );
    }

    public async Task<Try<ChordStatistics>> GetStatisticsAsync()
    {
        const string cacheKey = "chord_statistics";

        return await GetOrSetCacheWithTryAsync<ChordStatistics>(
            cacheKey,
            async () => await mongoDb.GetChordStatisticsAsync(),
            TimeSpan.FromMinutes(5)
        );
    }

    public async Task<Try<List<string>>> GetAvailableQualitiesAsync()
    {
        return await ExecuteAsync(async () =>
        {
            var qualities = await mongoDb.GetDistinctQualitiesAsync();
            return qualities.OrderBy(q => q).ToList();
        }, "GetAvailableQualities");
    }

    public async Task<Try<List<string>>> GetAvailableExtensionsAsync()
    {
        return await ExecuteAsync(async () =>
        {
            var extensions = await mongoDb.GetDistinctExtensionsAsync();
            return extensions.OrderBy(e => e).ToList();
        }, "GetAvailableExtensions");
    }

    public async Task<Try<List<string>>> GetAvailableStackingTypesAsync()
    {
        return await ExecuteAsync(async () =>
        {
            var stackingTypes = await mongoDb.GetDistinctStackingTypesAsync();
            return stackingTypes.OrderBy(s => s).ToList();
        }, "GetAvailableStackingTypes");
    }

    // Validation helpers
    private Validation<(string, int), ValidationError> ValidateQualityParameter(
        string quality, int limit)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(quality))
        {
            errors.Add(new ValidationError("Quality", "Quality cannot be empty"));
        }

        if (limit <= 0 || limit > 1000)
        {
            errors.Add(new ValidationError("Limit", "Limit must be between 1 and 1000"));
        }

        return errors.Any()
            ? Validation.Fail<(string, int), ValidationError>(errors.ToArray())
            : Validation.Success<(string, int), ValidationError>((quality, limit));
    }

    private Validation<(string, int), ValidationError> ValidateExtensionParameter(
        string extension, int limit)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(extension))
        {
            errors.Add(new ValidationError("Extension", "Extension cannot be empty"));
        }

        if (limit <= 0 || limit > 1000)
        {
            errors.Add(new ValidationError("Limit", "Limit must be between 1 and 1000"));
        }

        return errors.Any()
            ? Validation.Fail<(string, int), ValidationError>(errors.ToArray())
            : Validation.Success<(string, int), ValidationError>((extension, limit));
    }

    private Validation<(string, int), ValidationError> ValidateStackingTypeParameter(
        string stackingType, int limit)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(stackingType))
        {
            errors.Add(new ValidationError("StackingType",
                "Stacking type cannot be empty"));
        }

        if (limit <= 0 || limit > 1000)
        {
            errors.Add(new ValidationError("Limit", "Limit must be between 1 and 1000"));
        }

        return errors.Any()
            ? Validation.Fail<(string, int), ValidationError>(errors.ToArray())
            : Validation.Success<(string, int), ValidationError>((stackingType, limit));
    }
}

/// <summary>
///     Chord error types
/// </summary>
public enum ChordErrorType
{
    ValidationError,
    DatabaseError,
    NotFound
}

/// <summary>
///     Chord error details
/// </summary>
public record ChordError(ChordErrorType Type, string Message);

/// <summary>
///     Extension methods for registering monadic chord service
/// </summary>
public static class MonadicChordServiceExtensions
{
    public static IServiceCollection AddMonadicChordService(this IServiceCollection services)
    {
        services.AddScoped<IMonadicChordService, MonadicChordService>();
        return services;
    }
}
