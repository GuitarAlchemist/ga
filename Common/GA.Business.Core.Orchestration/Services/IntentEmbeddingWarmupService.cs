namespace GA.Business.Core.Orchestration.Services;

using GA.Business.ML.Agents.Intents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Pre-warms the <see cref="SemanticIntentRouter"/> embedding cache at startup
/// so the first real user request doesn't pay the cold-cache cost (~80 embedding
/// calls for the current intent set × CPU-mode latency = &gt;60s on a slow box,
/// which is well past any reasonable HTTP timeout).
/// </summary>
/// <remarks>
/// <para>Fires a single dummy <see cref="SemanticIntentRouter.RouteAsync"/>
/// call after the host starts. The router computes intent embeddings on first
/// query; this just makes that "first query" happen in the background.</para>
/// <para>Failures are logged but never thrown — embedding service might not be
/// reachable at startup (Ollama warming up, network blip), and we'd rather the
/// first user request retry warmup than crash the host.</para>
/// </remarks>
public sealed class IntentEmbeddingWarmupService(
    IServiceProvider services,
    SemanticIntentRouter router,
    ILogger<IntentEmbeddingWarmupService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Fire-and-forget — host startup must not block on this.
        _ = Task.Run(() => WarmAsync(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task WarmAsync(CancellationToken cancellationToken)
    {
        if (!router.IsAvailable)
        {
            logger.LogDebug("IntentEmbeddingWarmupService: router has no embedder; skipping warmup");
            return;
        }

        try
        {
            using var scope = services.CreateScope();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // The query content doesn't matter — we just want the router to
            // populate its example-embedding cache during this call.
            await router.RouteAsync("warmup", scope.ServiceProvider, cancellationToken);

            sw.Stop();
            logger.LogInformation(
                "IntentEmbeddingWarmupService: cache warmed in {ElapsedMs}ms",
                sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host is shutting down — fine.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "IntentEmbeddingWarmupService: warmup failed; first user request will retry");
        }
    }
}
