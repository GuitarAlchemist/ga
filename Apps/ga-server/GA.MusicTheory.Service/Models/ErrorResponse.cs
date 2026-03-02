namespace GA.MusicTheory.Service.Models;

/// <summary>
///     Standard API error payload for GA.MusicTheory.Service endpoints.
/// </summary>
public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
}
