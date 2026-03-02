namespace GaApi.Middleware;

/// <summary>
///     Request logging middleware for API monitoring
/// </summary>
public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = DateTime.UtcNow;
        var requestId = Guid.NewGuid().ToString();

        // Store correlation ID in HttpContext.Items for easy access
        context.Items["CorrelationId"] = requestId;

        // Add request ID to response headers
        context.Response.Headers["X-Request-ID"] = requestId;
        context.Response.Headers["X-Correlation-ID"] = requestId;

        // Log request start
        logger.LogInformation(
            "Request started: {RequestId} {Method} {Path} from {RemoteIp}",
            requestId,
            context.Request.Method,
            context.Request.Path,
            GetClientIpAddress(context));

        try
        {
            await next(context);
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;

            // Log request completion
            logger.LogInformation(
                "Request completed: {RequestId} {Method} {Path} responded {StatusCode} in {Duration}ms",
                requestId,
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                duration.TotalMilliseconds);
        }
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP first (for load balancers/proxies)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }
}