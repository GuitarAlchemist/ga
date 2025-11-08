namespace GaApi.Extensions;

/// <summary>
///     Extension methods for ASP.NET Core controllers to simplify common operations
/// </summary>
public static class ControllerExtensions
{
    /// <summary>
    ///     Get the correlation ID from the current HTTP context
    /// </summary>
    /// <param name="controller">The controller instance</param>
    /// <returns>The correlation ID for the current request</returns>
    public static string GetCorrelationId(this ControllerBase controller)
    {
        return controller.HttpContext.Items["CorrelationId"]?.ToString()
               ?? controller.HttpContext.Response.Headers["X-Request-ID"].FirstOrDefault()
               ?? Guid.NewGuid().ToString();
    }

    /// <summary>
    ///     Get the correlation ID from an HTTP context
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <returns>The correlation ID for the current request</returns>
    public static string GetCorrelationId(this HttpContext context)
    {
        return context.Items["CorrelationId"]?.ToString()
               ?? context.Response.Headers["X-Request-ID"].FirstOrDefault()
               ?? Guid.NewGuid().ToString();
    }

    /// <summary>
    ///     Set the correlation ID in the HTTP context
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <param name="correlationId">The correlation ID to set</param>
    public static void SetCorrelationId(this HttpContext context, string correlationId)
    {
        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers["X-Correlation-ID"] = correlationId;
    }
}
