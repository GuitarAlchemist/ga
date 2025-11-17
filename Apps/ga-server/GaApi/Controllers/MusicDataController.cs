namespace GaApi.Controllers;

using System.Runtime.CompilerServices;
using System.Text.Json;
using Constants;
using GA.Business.Core.Atonal;
using Microsoft.Extensions.Caching.Distributed;

/// <summary>
///     API controller for accessing music theory data (Set Classes, Forte Numbers, etc.)
///     This provides a centralized source of truth for music data across all services
/// </summary>
[ApiController]
[Route("api/music-data")]
public class MusicDataController(
    IDistributedCache cache,
    ILogger<MusicDataController> logger)
    : ControllerBase
{
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromHours(1);

    /// <summary>
    ///     Get all Set Classes (93 items)
    /// </summary>
    [HttpGet("set-classes")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> GetSetClasses()
    {
        var cacheKey = CacheKeys.MusicSetClasses;

        try
        {
            // Check Redis cache first
            var cached = await cache.GetStringAsync(cacheKey);
            if (cached != null)
            {
                logger.LogInformation("Cache hit for set-classes");
                return Ok(JsonSerializer.Deserialize<List<string>>(cached));
            }

            // Fetch from source
            logger.LogInformation("Cache miss for set-classes, fetching from source");
            var items = SetClass.Items.Select(sc => sc.ToString()).ToList();

            // Cache for 1 hour
            await cache.SetStringAsync(cacheKey,
                JsonSerializer.Serialize(items),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _cacheDuration
                });

            return Ok(items);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching set classes");
            return StatusCode(500, new { error = "Failed to fetch set classes", message = ex.Message });
        }
    }

    /// <summary>
    ///     Get all Forte Numbers (224 items)
    /// </summary>
    [HttpGet("forte-numbers")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> GetForteNumbers()
    {
        var cacheKey = CacheKeys.MusicForteNumbers;

        try
        {
            // Check Redis cache first
            var cached = await cache.GetStringAsync(cacheKey);
            if (cached != null)
            {
                logger.LogInformation("Cache hit for forte-numbers");
                return Ok(JsonSerializer.Deserialize<List<string>>(cached));
            }

            // Fetch from source
            logger.LogInformation("Cache miss for forte-numbers, fetching from source");
            var items = ForteNumber.Items.Select(fn => fn.ToString()).ToList();

            // Cache for 1 hour
            await cache.SetStringAsync(cacheKey,
                JsonSerializer.Serialize(items),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _cacheDuration
                });

            return Ok(items);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching forte numbers");
            return StatusCode(500, new { error = "Failed to fetch forte numbers", message = ex.Message });
        }
    }

    /// <summary>
    ///     Stream all Set Classes (93 items) for progressive rendering
    /// </summary>
    [HttpGet("set-classes/stream")]
    [ProducesResponseType(typeof(IAsyncEnumerable<string>), StatusCodes.Status200OK)]
    public async IAsyncEnumerable<string> GetSetClassesStream(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Streaming set classes");

        var items = SetClass.Items;
        var count = 0;

        foreach (var item in items)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Set classes streaming cancelled after {Count} items", count);
                yield break;
            }

            count++;
            yield return item.ToString();

            // Backpressure control
            await Task.Delay(1, cancellationToken);
        }

        logger.LogInformation("Completed streaming {Count} set classes", count);
    }

    /// <summary>
    ///     Stream all Forte Numbers (224 items) for progressive rendering
    /// </summary>
    [HttpGet("forte-numbers/stream")]
    [ProducesResponseType(typeof(IAsyncEnumerable<string>), StatusCodes.Status200OK)]
    public async IAsyncEnumerable<string> GetForteNumbersStream(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Streaming forte numbers");

        var items = ForteNumber.Items;
        var count = 0;

        foreach (var item in items)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Forte numbers streaming cancelled after {Count} items", count);
                yield break;
            }

            count++;
            yield return item.ToString();

            // Log progress every 50 items
            if (count % 50 == 0)
            {
                logger.LogDebug("Streamed {Count} forte numbers so far", count);
            }

            // Backpressure control
            await Task.Delay(1, cancellationToken);
        }

        logger.LogInformation("Completed streaming {Count} forte numbers", count);
    }

    /// <summary>
    ///     Get music items for a specific floor (0-5)
    /// </summary>
    [HttpGet("floor/{floorNumber}/items")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<string>>> GetFloorItems(int floorNumber)
    {
        if (floorNumber is < 0 or > 5)
        {
            return BadRequest(new { error = "Floor number must be between 0 and 5" });
        }

        var cacheKey = CacheKeys.MusicFloorItems(floorNumber);

        try
        {
            // Check Redis cache first
            var cached = await cache.GetStringAsync(cacheKey);
            if (cached != null)
            {
                logger.LogInformation("Cache hit for floor {Floor} items", floorNumber);
                return Ok(JsonSerializer.Deserialize<List<string>>(cached));
            }

            // Fetch from source based on floor
            logger.LogInformation("Cache miss for floor {Floor} items, fetching from source", floorNumber);
            var items = floorNumber switch
            {
                0 => SetClass.Items.Select(sc => sc.ToString()).ToList(),
                1 => [.. ForteNumber.Items.Select(fn => fn.ToString())],
                2 => [.. SetClass.Items.Take(200).Select(sc => $"Prime: {sc}")],
                3 => [.. SetClass.Items.Take(350).Select(sc => $"Chord: {sc}")],
                4 => [.. SetClass.Items.Take(100).Select(sc => $"Inversion: {sc}")],
                5 => [.. SetClass.Items.Take(200).Select(sc => $"Voicing: {sc}")],
                _ => []
            };

            // Cache for 1 hour
            await cache.SetStringAsync(cacheKey,
                JsonSerializer.Serialize(items),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _cacheDuration
                });

            return Ok(items);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching floor {Floor} items", floorNumber);
            return StatusCode(500, new { error = $"Failed to fetch floor {floorNumber} items", message = ex.Message });
        }
    }

    /// <summary>
    ///     Stream music items for a specific floor (0-5) for progressive rendering
    /// </summary>
    [HttpGet("floor/{floorNumber}/items/stream")]
    [ProducesResponseType(typeof(IAsyncEnumerable<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async IAsyncEnumerable<string> GetFloorItemsStream(
        int floorNumber,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (floorNumber is < 0 or > 5)
        {
            logger.LogWarning("Invalid floor number: {Floor}", floorNumber);
            yield break;
        }

        logger.LogInformation("Streaming floor {Floor} items", floorNumber);

        // Get items based on floor
        var items = floorNumber switch
        {
            0 => SetClass.Items.Select(sc => sc.ToString()),
            1 => ForteNumber.Items.Select(fn => fn.ToString()),
            2 => SetClass.Items.Take(200).Select(sc => $"Prime: {sc}"),
            3 => SetClass.Items.Take(350).Select(sc => $"Chord: {sc}"),
            4 => SetClass.Items.Take(100).Select(sc => $"Inversion: {sc}"),
            5 => SetClass.Items.Take(200).Select(sc => $"Voicing: {sc}"),
            _ => []
        };

        var count = 0;

        foreach (var item in items)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Floor {Floor} items streaming cancelled after {Count} items", floorNumber,
                    count);
                yield break;
            }

            count++;
            yield return item;

            // Log progress every 50 items
            if (count % 50 == 0)
            {
                logger.LogDebug("Streamed {Count} floor {Floor} items so far", count, floorNumber);
            }

            // Backpressure control
            await Task.Delay(1, cancellationToken);
        }

        logger.LogInformation("Completed streaming {Count} items for floor {Floor}", count, floorNumber);
    }

    /// <summary>
    ///     Get cache statistics
    /// </summary>
    [HttpGet("cache/stats")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult GetCacheStats()
    {
        return Ok(new
        {
            cacheDuration = _cacheDuration.ToString(),
            cacheKeys = new[]
            {
                CacheKeys.MusicSetClasses,
                CacheKeys.MusicForteNumbers,
                CacheKeys.MusicFloorItems(0),
                CacheKeys.MusicFloorItems(1),
                CacheKeys.MusicFloorItems(2),
                CacheKeys.MusicFloorItems(3),
                CacheKeys.MusicFloorItems(4),
                CacheKeys.MusicFloorItems(5)
            },
            message = "Use these keys to check cache status"
        });
    }

    /// <summary>
    ///     Clear all music data cache
    /// </summary>
    [HttpPost("cache/clear")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ClearCache()
    {
        try
        {
            var keys = new[]
            {
                CacheKeys.MusicSetClasses,
                CacheKeys.MusicForteNumbers,
                CacheKeys.MusicFloorItems(0),
                CacheKeys.MusicFloorItems(1),
                CacheKeys.MusicFloorItems(2),
                CacheKeys.MusicFloorItems(3),
                CacheKeys.MusicFloorItems(4),
                CacheKeys.MusicFloorItems(5)
            };

            foreach (var key in keys)
            {
                await cache.RemoveAsync(key);
            }

            logger.LogInformation("Cleared all music data cache");
            return Ok(new { message = "Cache cleared successfully", keysCleared = keys.Length });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing cache");
            return StatusCode(500, new { error = "Failed to clear cache", message = ex.Message });
        }
    }
}
