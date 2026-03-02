namespace GaApi.Middleware;

using System.Diagnostics;

/// <summary>
///     Performance monitoring middleware
/// </summary>
public class PerformanceMiddleware(RequestDelegate next, ILogger<PerformanceMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        // Add response callback to set headers after response is complete
        context.Response.OnStarting(() =>
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            // Add performance headers before response starts
            if (!context.Response.Headers.ContainsKey("X-Response-Time"))
            {
                context.Response.Headers["X-Response-Time"] = $"{elapsedMs}ms";
            }

            return Task.CompletedTask;
        });

        await next(context);

        stopwatch.Stop();

        var elapsedMs = stopwatch.ElapsedMilliseconds;

        // Log slow requests (> 1 second)
        if (elapsedMs > 1000)
        {
            logger.LogWarning(
                "Slow request detected: {Method} {Path} took {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                elapsedMs);
        }

        // Log very slow requests (> 5 seconds) as errors
        if (elapsedMs > 5000)
        {
            logger.LogError(
                "Very slow request: {Method} {Path} took {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                elapsedMs);
        }
    }
}
