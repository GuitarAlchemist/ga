namespace GaApi.Services;

using System.Threading.Channels;
using Models;

/// <summary>
///     Background service that continuously processes pending room generation jobs using Channels for instant processing
/// </summary>
public class RoomGenerationBackgroundService(
    IServiceProvider serviceProvider,
    ILogger<RoomGenerationBackgroundService> logger)
    : BackgroundService
{
    private readonly int _batchSize = 10;

    // Channel for instant job processing (no polling delay!)
    private readonly Channel<RoomGenerationJob> _jobChannel = Channel.CreateUnbounded<RoomGenerationJob>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    /// <summary>
    ///     Queue a job for instant processing
    /// </summary>
    public async Task QueueJobAsync(RoomGenerationJob job)
    {
        await _jobChannel.Writer.WriteAsync(job);
        logger.LogDebug("Job {JobId} queued for instant processing", job.Id);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Room Generation Background Service started (Channel-based)");

        // Wait a bit before starting to allow the application to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        // Process jobs as they arrive (instant processing, no polling!)
        await foreach (var job in _jobChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing job {JobId}", job.Id);
            }
        }

        logger.LogInformation("Room Generation Background Service stopped");
    }

    /// <summary>
    ///     Process a single job (called from channel consumer)
    /// </summary>
    private async Task ProcessJobAsync(RoomGenerationJob job, CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        // Create a scope to get scoped services
        using var scope = serviceProvider.CreateScope();
        var musicRoomService = scope.ServiceProvider.GetRequiredService<MusicRoomService>();

        try
        {
            logger.LogInformation("Processing job {JobId} for floor {Floor}", job.Id, job.Floor);

            var result = await musicRoomService.ProcessJobAsync(job.Id!);

            logger.LogInformation(
                "Successfully processed job {JobId} - Generated {RoomCount} rooms for floor {Floor}",
                job.Id,
                result.Rooms.Count,
                result.Floor);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process job {JobId}", job.Id);
            // Continue processing other jobs even if one fails
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Room Generation Background Service is stopping");
        await base.StopAsync(stoppingToken);
    }
}
