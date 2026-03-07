namespace AllProjects.ServiceDefaults;

using System;
using System.Collections.Generic;

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
        Dictionary<string, object>? metadata = null, string? correlationId = null) =>
        new()
        {
            Success = true,
            Data = data,
            Pagination = pagination,
            Metadata = metadata,
            CorrelationId = correlationId
        };

    /// <summary>
    ///     Create an error response
    /// </summary>
    public static ApiResponse<T> Fail(string error, string? errorDetails = null, string? errorCode = null,
        string? correlationId = null) =>
        new()
        {
            Success = false,
            Error = error,
            ErrorDetails = errorDetails,
            ErrorCode = errorCode,
            CorrelationId = correlationId
        };
}
