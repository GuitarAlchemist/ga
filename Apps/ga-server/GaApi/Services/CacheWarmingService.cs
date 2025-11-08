namespace GaApi.Services;

using GA.Business.Core.Atonal;

/// <summary>
///     Background service for warming up caches on startup
/// </summary>
public class CacheWarmingService(
    ICachingService cachingService,
    ILogger<CacheWarmingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Starting cache warming...");

            // Wait a bit for the application to fully start
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            await WarmCaches(stoppingToken);

            logger.LogInformation("Cache warming completed successfully");
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Cache warming cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during cache warming");
        }
    }

    private async Task WarmCaches(CancellationToken cancellationToken)
    {
        // Warm up frequently accessed data
        await WarmPitchClassSets(cancellationToken);
        await WarmCommonScales(cancellationToken);
        await WarmCommonChords(cancellationToken);
    }

    private async Task WarmPitchClassSets(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogDebug("Warming pitch class sets cache...");

            // Preload common pitch class sets
            await cachingService.GetOrCreateRegularAsync(
                "pitchclasssets:all",
                async () =>
                {
                    await Task.CompletedTask;
                    // This would normally fetch from database or compute
                    // For now, just return a placeholder
                    return new List<PitchClassSet>();
                });

            logger.LogDebug("Pitch class sets cache warmed");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to warm pitch class sets cache");
        }
    }

    private async Task WarmCommonScales(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogDebug("Warming common scales cache...");

            // Preload common scales (Major, Minor, etc.)
            var commonScales = new[]
            {
                "Major",
                "NaturalMinor",
                "HarmonicMinor",
                "MelodicMinor",
                "Dorian",
                "Phrygian",
                "Lydian",
                "Mixolydian",
                "Locrian"
            };

            foreach (var scale in commonScales)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                await cachingService.GetOrCreateRegularAsync(
                    $"scale:{scale}",
                    async () =>
                    {
                        await Task.CompletedTask;
                        // This would normally fetch from database or compute
                        return new { Name = scale, Intervals = new int[] { } };
                    });
            }

            logger.LogDebug("Common scales cache warmed");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to warm common scales cache");
        }
    }

    private async Task WarmCommonChords(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogDebug("Warming common chords cache...");

            // Preload common chord types
            var commonChords = new[]
            {
                "Major",
                "Minor",
                "Diminished",
                "Augmented",
                "Major7",
                "Minor7",
                "Dominant7",
                "HalfDiminished7",
                "Diminished7"
            };

            foreach (var chord in commonChords)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                await cachingService.GetOrCreateRegularAsync(
                    $"chord:{chord}",
                    async () =>
                    {
                        await Task.CompletedTask;
                        // This would normally fetch from database or compute
                        return new { Name = chord, Intervals = new int[] { } };
                    });
            }

            logger.LogDebug("Common chords cache warmed");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to warm common chords cache");
        }
    }
}
