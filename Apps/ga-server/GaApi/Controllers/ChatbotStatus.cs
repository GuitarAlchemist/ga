namespace GaApi.Controllers;

public class ChatbotStatus
{
    public bool IsAvailable { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
