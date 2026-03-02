namespace GaApi.Middleware;

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
