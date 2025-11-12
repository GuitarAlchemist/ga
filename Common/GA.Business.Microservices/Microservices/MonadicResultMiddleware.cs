namespace GA.Business.Core.Microservices.Microservices;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
///     Middleware for automatic monad error handling
///     Intercepts controller responses and converts monad types to appropriate HTTP responses
/// </summary>
public class MonadicResultMiddleware(RequestDelegate next, ILogger<MonadicResultMiddleware> logger)
{
    private readonly ILogger<MonadicResultMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        // Store original response body stream
        var originalBodyStream = context.Response.Body;

        try
        {
            // Create a new memory stream to capture the response
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            // Call the next middleware
            await next(context);

            // Reset the stream position
            responseBody.Seek(0, SeekOrigin.Begin);

            // Read the response
            var responseText = await new StreamReader(responseBody).ReadToEndAsync();

            // Reset the stream position again
            responseBody.Seek(0, SeekOrigin.Begin);

            // Copy the response to the original stream
            await responseBody.CopyToAsync(originalBodyStream);
        }
        finally
        {
            // Restore the original body stream
            context.Response.Body = originalBodyStream;
        }
    }
}

/// <summary>
///     Action filter for automatic monad result handling
///     Converts Try, Option, and Result monads to appropriate HTTP responses
/// </summary>
public class MonadicResultFilter(ILogger<MonadicResultFilter> logger) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        // No action needed before execution
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Result is ObjectResult objectResult && objectResult.Value != null)
        {
            var value = objectResult.Value;
            var valueType = value.GetType();

            // Check if the result is a Try monad
            if (valueType.IsGenericType && valueType.GetGenericTypeDefinition().Name.StartsWith("Try"))
            {
                context.Result = HandleTryResult(value, objectResult);
            }
            // Check if the result is an Option monad
            else if (valueType.IsGenericType && valueType.GetGenericTypeDefinition().Name.StartsWith("Option"))
            {
                context.Result = HandleOptionResult(value, objectResult);
            }
            // Check if the result is a Result monad
            else if (valueType.IsGenericType && valueType.GetGenericTypeDefinition().Name.StartsWith("Result"))
            {
                context.Result = HandleResultResult(value, objectResult);
            }
        }
    }

    private IActionResult HandleTryResult(object tryValue, ObjectResult originalResult)
    {
        var tryType = tryValue.GetType();
        var matchMethod = tryType.GetMethod("Match");

        if (matchMethod == null)
        {
            logger.LogWarning("Try type does not have a Match method");
            return originalResult;
        }

        // Create delegates for success and failure cases
        object? result = null;
        var onSuccess = new Func<object, IActionResult>(value =>
        {
            result = new OkObjectResult(value);
            return (IActionResult)result;
        });

        var onFailure = new Func<Exception, IActionResult>(ex =>
        {
            logger.LogError(ex, "Try monad failed");
            result = new ObjectResult(new { error = ex.Message })
            {
                StatusCode = 500
            };
            return (IActionResult)result;
        });

        // Invoke Match method
        try
        {
            matchMethod.Invoke(tryValue, new object[] { onSuccess, onFailure });
            return result as IActionResult ?? originalResult;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling Try result");
            return originalResult;
        }
    }

    private IActionResult HandleOptionResult(object optionValue, ObjectResult originalResult)
    {
        var optionType = optionValue.GetType();
        var matchMethod = optionType.GetMethod("Match");

        if (matchMethod == null)
        {
            logger.LogWarning("Option type does not have a Match method");
            return originalResult;
        }

        // Create delegates for Some and None cases
        object? result = null;
        var onSome = new Func<object, IActionResult>(value =>
        {
            result = new OkObjectResult(value);
            return (IActionResult)result;
        });

        var onNone = new Func<IActionResult>(() =>
        {
            result = new NotFoundResult();
            return (IActionResult)result;
        });

        // Invoke Match method
        try
        {
            matchMethod.Invoke(optionValue, new object[] { onSome, onNone });
            return result as IActionResult ?? originalResult;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling Option result");
            return originalResult;
        }
    }

    private IActionResult HandleResultResult(object resultValue, ObjectResult originalResult)
    {
        var resultType = resultValue.GetType();
        var matchMethod = resultType.GetMethod("Match");

        if (matchMethod == null)
        {
            logger.LogWarning("Result type does not have a Match method");
            return originalResult;
        }

        // Create delegates for Success and Failure cases
        object? result = null;
        var onSuccess = new Func<object, IActionResult>(value =>
        {
            result = new OkObjectResult(value);
            return (IActionResult)result;
        });

        var onFailure = new Func<object, IActionResult>(error =>
        {
            logger.LogWarning("Result monad failed with error: {Error}", error);
            result = new BadRequestObjectResult(new { error = error?.ToString() ?? "Unknown error" });
            return (IActionResult)result;
        });

        // Invoke Match method
        try
        {
            matchMethod.Invoke(resultValue, new object[] { onSuccess, onFailure });
            return result as IActionResult ?? originalResult;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling Result result");
            return originalResult;
        }
    }
}

/// <summary>
///     Extension methods for registering monadic middleware
/// </summary>
public static class MonadicMiddlewareExtensions
{
    /// <summary>
    ///     Add monadic result middleware to the application
    /// </summary>
    public static IApplicationBuilder UseMonadicResults(this IApplicationBuilder app)
    {
        return app.UseMiddleware<MonadicResultMiddleware>();
    }

    /// <summary>
    ///     Add monadic result filter to MVC options
    /// </summary>
    public static IMvcBuilder AddMonadicResultFilter(this IMvcBuilder builder)
    {
        builder.Services.AddScoped<MonadicResultFilter>();
        return builder;
    }
}

/// <summary>
///     Standard error response model for monad failures
/// </summary>
public class MonadErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? TraceId { get; set; }
}

/// <summary>
///     Attribute to enable automatic monad result handling for a controller or action
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class MonadicResultAttribute : Attribute
{
    /// <summary>
    ///     Whether to include exception details in error responses
    /// </summary>
    public bool IncludeExceptionDetails { get; set; } = false;

    /// <summary>
    ///     Custom error message for failures
    /// </summary>
    public string? CustomErrorMessage { get; set; }
}
