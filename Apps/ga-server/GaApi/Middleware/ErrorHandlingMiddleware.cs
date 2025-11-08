namespace GaApi.Middleware;

using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Models;

/// <summary>
///     Global error handling middleware for consistent error responses
/// </summary>
public class ErrorHandlingMiddleware(
    RequestDelegate next,
    ILogger<ErrorHandlingMiddleware> logger,
    IWebHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unhandled exception occurred while processing the request");
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        // Get or create correlation ID
        var correlationId = context.Response.Headers["X-Request-ID"].FirstOrDefault()
                            ?? context.Items["CorrelationId"]?.ToString()
                            ?? Guid.NewGuid().ToString();

        var response = new ApiResponse<object>();
        var statusCode = HttpStatusCode.InternalServerError;

        switch (exception)
        {
            case ArgumentNullException nullEx:
                statusCode = HttpStatusCode.BadRequest;
                response = ApiResponse<object>.Fail(
                    "Required parameter is missing",
                    nullEx.ParamName ?? "Unknown parameter",
                    ErrorCodes.MissingParameter,
                    correlationId);
                break;

            case ArgumentException argEx:
                statusCode = HttpStatusCode.BadRequest;
                response = ApiResponse<object>.Fail(
                    "Invalid argument",
                    argEx.Message,
                    ErrorCodes.InvalidArgument,
                    correlationId);
                break;

            case UnauthorizedAccessException:
                statusCode = HttpStatusCode.Unauthorized;
                response = ApiResponse<object>.Fail(
                    "Unauthorized access",
                    null,
                    ErrorCodes.Unauthorized,
                    correlationId);
                break;

            case NotImplementedException:
                statusCode = HttpStatusCode.NotImplemented;
                response = ApiResponse<object>.Fail(
                    "Feature not implemented",
                    null,
                    ErrorCodes.NotImplemented,
                    correlationId);
                break;

            case TimeoutException:
                statusCode = HttpStatusCode.RequestTimeout;
                response = ApiResponse<object>.Fail(
                    "Request timeout",
                    null,
                    ErrorCodes.Timeout,
                    correlationId);
                break;

            case InvalidOperationException invalidOpEx:
                statusCode = HttpStatusCode.BadRequest;
                response = ApiResponse<object>.Fail(
                    "Invalid operation",
                    invalidOpEx.Message,
                    ErrorCodes.InvalidOperation,
                    correlationId);
                break;

            default:
                statusCode = HttpStatusCode.InternalServerError;
                response = ApiResponse<object>.Fail(
                    "An internal server error occurred",
                    environment.IsDevelopment() ? exception.ToString() : null,
                    ErrorCodes.InternalError,
                    correlationId);
                break;
        }

        context.Response.StatusCode = (int)statusCode;

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}

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

/// <summary>
///     Extension methods for middleware registration
/// </summary>
public static class MiddlewareExtensions
{
    /// <summary>
    ///     Add error handling middleware
    /// </summary>
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ErrorHandlingMiddleware>();
    }

    /// <summary>
    ///     Add request logging middleware
    /// </summary>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }

    /// <summary>
    ///     Add performance monitoring middleware
    /// </summary>
    public static IApplicationBuilder UsePerformanceMonitoring(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<PerformanceMiddleware>();
    }
}
