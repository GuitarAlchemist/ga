namespace GaApi.Middleware;

/// <summary>
///     Extension methods for middleware registration
/// </summary>
public static class MiddlewareExtensions
{
    /// <summary>
    ///     Add error handling middleware
    /// </summary>
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder builder) => builder.UseMiddleware<ErrorHandlingMiddleware>();

    /// <summary>
    ///     Add request logging middleware
    /// </summary>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder) => builder.UseMiddleware<RequestLoggingMiddleware>();

    /// <summary>
    ///     Add performance monitoring middleware
    /// </summary>
    public static IApplicationBuilder UsePerformanceMonitoring(this IApplicationBuilder builder) => builder.UseMiddleware<PerformanceMiddleware>();
}
