namespace GA.BSP.Service.Models;

/// <summary>
///     Standard API response wrapper for consistent response format
/// </summary>
/// <typeparam name="T">The type of data being returned</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    ///     Indicates if the request was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     The response data
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    ///     Error message if the request failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    ///     Additional error details for debugging
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    ///     Error code for programmatic error handling
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    ///     Correlation ID for request tracking across services
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    ///     Timestamp of the response
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Pagination information if applicable
    /// </summary>
    public PaginationInfo? Pagination { get; set; }

    /// <summary>
    ///     Additional metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    ///     Create a successful response
    /// </summary>
    public static ApiResponse<T> Ok(T data, PaginationInfo? pagination = null,
        Dictionary<string, object>? metadata = null, string? correlationId = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Pagination = pagination,
            Metadata = metadata,
            CorrelationId = correlationId
        };
    }

    /// <summary>
    ///     Create an error response
    /// </summary>
    public static ApiResponse<T> Fail(string error, string? errorDetails = null, string? errorCode = null,
        string? correlationId = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Error = error,
            ErrorDetails = errorDetails,
            ErrorCode = errorCode,
            CorrelationId = correlationId
        };
    }
}

/// <summary>
///     Pagination information for paginated responses
/// </summary>
public class PaginationInfo
{
    /// <summary>
    ///     Current page number (1-based)
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    ///     Number of items per page
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    ///     Total number of items
    /// </summary>
    public long TotalItems { get; set; }

    /// <summary>
    ///     Total number of pages
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);

    /// <summary>
    ///     Whether there is a next page
    /// </summary>
    public bool HasNext => Page < TotalPages;

    /// <summary>
    ///     Whether there is a previous page
    /// </summary>
    public bool HasPrevious => Page > 1;

    /// <summary>
    ///     Number of items in the current page
    /// </summary>
    public int ItemCount { get; set; }
}

/// <summary>
///     Request parameters for paginated endpoints
/// </summary>
public class PaginationRequest
{
    /// <summary>
    ///     Page number (1-based, default: 1)
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    ///     Number of items per page (default: 50, max: 1000)
    /// </summary>
    public int PageSize { get; set; } = 50;

    /// <summary>
    ///     Calculate skip count for database queries
    /// </summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    ///     Validate pagination parameters
    /// </summary>
    public void Validate()
    {
        if (Page < 1)
        {
            Page = 1;
        }

        if (PageSize < 1)
        {
            PageSize = 50;
        }

        if (PageSize > 1000)
        {
            PageSize = 1000;
        }
    }
}

/// <summary>
///     Search request parameters
/// </summary>
public class SearchRequest : PaginationRequest
{
    /// <summary>
    ///     Search query string
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    ///     Filters to apply
    /// </summary>
    public Dictionary<string, string>? Filters { get; set; }

    /// <summary>
    ///     Sort field
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    ///     Sort direction (asc/desc)
    /// </summary>
    public string SortDirection { get; set; } = "asc";

    /// <summary>
    ///     Whether to include fuzzy matching
    /// </summary>
    public bool FuzzySearch { get; set; } = false;
}

/// <summary>
///     Health check response
/// </summary>
public class HealthCheckResponse
{
    /// <summary>
    ///     Overall health status
    /// </summary>
    public string Status { get; set; } = "Healthy";

    /// <summary>
    ///     Timestamp of the health check
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Individual service health checks
    /// </summary>
    public Dictionary<string, ServiceHealth> Services { get; set; } = new();

    /// <summary>
    ///     API version
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    ///     Environment name
    /// </summary>
    public string Environment { get; set; } = "Development";
}

/// <summary>
///     Individual service health information
/// </summary>
public class ServiceHealth
{
    /// <summary>
    ///     Service health status
    /// </summary>
    public string Status { get; set; } = "Healthy";

    /// <summary>
    ///     Response time in milliseconds
    /// </summary>
    public long ResponseTimeMs { get; set; }

    /// <summary>
    ///     Additional service details
    /// </summary>
    public Dictionary<string, object>? Details { get; set; }

    /// <summary>
    ///     Error message if unhealthy
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
///     Validation error details
/// </summary>
public class ValidationError
{
    /// <summary>
    ///     Field name that failed validation
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    ///     Validation error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    ///     Attempted value
    /// </summary>
    public object? AttemptedValue { get; set; }
}

/// <summary>
///     Validation error response
/// </summary>
public class ValidationErrorResponse
{
    /// <summary>
    ///     List of validation errors
    /// </summary>
    public List<ValidationError> Errors { get; set; } = [];

    /// <summary>
    ///     Overall validation message
    /// </summary>
    public string Message { get; set; } = "Validation failed";

    /// <summary>
    ///     Timestamp of the validation error
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
///     Standard error codes for consistent error handling
/// </summary>
public static class ErrorCodes
{
    // Client Errors (4xx)
    public const string BadRequest = "BAD_REQUEST";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string InvalidArgument = "INVALID_ARGUMENT";
    public const string MissingParameter = "MISSING_PARAMETER";

    // Server Errors (5xx)
    public const string InternalError = "INTERNAL_ERROR";
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
    public const string Timeout = "TIMEOUT";
    public const string NotImplemented = "NOT_IMPLEMENTED";

    // Business Logic Errors
    public const string InvalidOperation = "INVALID_OPERATION";
    public const string ResourceNotFound = "RESOURCE_NOT_FOUND";
    public const string DuplicateResource = "DUPLICATE_RESOURCE";
    public const string OperationFailed = "OPERATION_FAILED";

    // External Service Errors
    public const string DatabaseError = "DATABASE_ERROR";
    public const string ExternalServiceError = "EXTERNAL_SERVICE_ERROR";
    public const string CacheError = "CACHE_ERROR";
}
