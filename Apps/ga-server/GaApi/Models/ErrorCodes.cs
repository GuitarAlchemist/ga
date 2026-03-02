namespace GaApi.Models;

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
