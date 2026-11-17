namespace GaApi.Controllers;

public class ChatbotStatus
{
    public bool IsAvailable { get; set; }
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// How the most recent LLM generation ended: <see langword="false"/> when it failed,
    /// <see langword="null"/> when none ended within
    /// <see cref="GA.Business.ML.Providers.ChatGenerationHealth.OutcomeWindow"/>. Catches a
    /// provider that answers the availability check but refuses to generate, such as
    /// Ollama Cloud without credits.
    /// </summary>
    public bool? GenerationOk { get; set; }

    public DateTime Timestamp { get; set; }
}
