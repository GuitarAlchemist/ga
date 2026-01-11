namespace ScenesService.Services;

using Models;
using MongoDB.Driver;

public sealed class SceneBuildWorker(IJobStore jobs, ICancelStore cancel, ISceneStore store, GlbSceneBuilder builder,
    IMongoDatabase db, ILogger<SceneBuildWorker> log, IConfiguration cfg)
    : BackgroundService
{
    private readonly int _parallelism = Math.Clamp(cfg.GetValue("Build:Parallelism", Environment.ProcessorCount / 2), 1, 32);
    private readonly IMongoCollection<SceneBuildRequestDto> _payloads = db.GetCollection<SceneBuildRequestDto>("scene_payloads");
    private readonly TimeSpan _pollDelay = TimeSpan.FromMilliseconds(cfg.GetValue("Build:PollMs", 500));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var sem = new SemaphoreSlim(_parallelism);
        var running = new List<Task>();

        while (!stoppingToken.IsCancellationRequested)
        {
            var pulled = false;
            await foreach (var job in jobs.DequeueBatchAsync(_parallelism, stoppingToken))
            {
                pulled = true;
                await sem.WaitAsync(stoppingToken);
                running.Add(Process(job, sem, stoppingToken));
            }

            // collect terminé
            running.RemoveAll(t => t.IsCompleted);

            if (!pulled) // rien en file, petite pause
            {
                await Task.Delay(_pollDelay, stoppingToken);
            }
        }

        await Task.WhenAll(running);
    }

    private async Task Process(BuildJobDto job, SemaphoreSlim sem, CancellationToken ct)
    {
        try
        {
            if (await cancel.IsCanceledAsync(job.JobId, ct))
            {
                await jobs.UpdateAsync(
                    job with { Status = BuildStatus.Canceled, CompletedUtc = DateTimeOffset.UtcNow }, ct);
                return;
            }

            var payload = await _payloads.Find(x => x.SceneId == job.SceneId).SortByDescending(_ => _.SceneId)
                .FirstOrDefaultAsync(ct);
            if (payload is null)
            {
                throw new InvalidOperationException("Payload introuvable");
            }

            var (path, bytes, etag) = await builder.BuildGlbAsync(payload, store);
            await jobs.UpdateAsync(job with
            {
                Status = BuildStatus.Succeeded,
                CompletedUtc = DateTimeOffset.UtcNow,
                Attempt = job.Attempt + 1
            }, ct);

            log.LogInformation("Build completed for scene {SceneId}, job {JobId}, {Bytes} bytes", job.SceneId,
                job.JobId, bytes);
        }
        catch (Exception ex)
        {
            var attempt = job.Attempt + 1;
            var nextStatus = attempt < job.MaxAttempts ? BuildStatus.Queued : BuildStatus.Failed;
            await jobs.UpdateAsync(job with
            {
                Status = nextStatus,
                Error = ex.Message,
                Attempt = attempt,
                CompletedUtc = nextStatus == BuildStatus.Failed ? DateTimeOffset.UtcNow : null
            }, ct);

            log.LogError(ex, "Build failed for scene {SceneId}, job {JobId}, attempt {Attempt}/{MaxAttempts}",
                job.SceneId, job.JobId, attempt, job.MaxAttempts);
        }
        finally
        {
            sem.Release();
        }
    }
}
