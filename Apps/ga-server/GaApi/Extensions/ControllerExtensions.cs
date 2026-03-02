namespace GaApi.Extensions;

/// <summary>
///     Extension methods for ASP.NET Core controllers to simplify common operations
/// </summary>
public static class ControllerExtensions
{
    extension(ControllerBase controller)
    {
        /// <summary>
        ///     Get the correlation ID from the current HTTP context
        /// </summary>
        /// <returns>The correlation ID for the current request</returns>
        public string GetCorrelationId() =>
            controller.HttpContext.Items["CorrelationId"]?.ToString()
            ?? controller.HttpContext.Response.Headers["X-Request-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();
    }

    extension(HttpContext context)
    {
        /// <summary>
        ///     Get the correlation ID from an HTTP context
        /// </summary>
        /// <returns>The correlation ID for the current request</returns>
        public string GetCorrelationId() =>
            context.Items["CorrelationId"]?.ToString()
            ?? context.Response.Headers["X-Request-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        /// <summary>
        ///     Set the correlation ID in the HTTP context
        /// </summary>
        /// <param name="correlationId">The correlation ID to set</param>
        public void SetCorrelationId(string correlationId)
        {
            context.Items["CorrelationId"] = correlationId;
            context.Response.Headers["X-Correlation-ID"] = correlationId;
        }
    }
}
