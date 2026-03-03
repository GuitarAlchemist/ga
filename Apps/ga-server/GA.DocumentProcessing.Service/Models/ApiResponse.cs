namespace GA.DocumentProcessing.Service.Models;

/// <summary>
/// Standard API response wrapper
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public List<string>? Errors { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null, Dictionary<string, object>? metadata = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message,
            Metadata = metadata
        };
    }

    public static ApiResponse<T> Fail(string message, params string[] errors)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Errors = errors.ToList()
        };
    }
}

