namespace GA.Business.Core.AI.LmStudio;

/// <summary>
/// Settings for LM Studio integration
/// </summary>
public class LmStudioSettings
{
    /// <summary>
    /// The URL of the LM Studio API
    /// </summary>
    public string ApiUrl { get; set; } = "http://localhost:1234/v1";
    
    /// <summary>
    /// The model to use for LM Studio
    /// </summary>
    public string Model { get; set; } = "NyxGleam/mistral-7b-instruct-v0.1.Q4_K_M";
    
    /// <summary>
    /// The system prompt to use for LM Studio
    /// </summary>
    public string SystemPrompt { get; set; } = "You are a guitar expert assistant. Your knowledge includes detailed information about guitar fretboard positions, chords, scales, and music theory. Always provide accurate and helpful information about guitar playing techniques, music theory, and fretboard navigation.";
    
    /// <summary>
    /// The maximum number of tokens to generate
    /// </summary>
    public int MaxTokens { get; set; } = 1024;
    
    /// <summary>
    /// The temperature to use for generation
    /// </summary>
    public float Temperature { get; set; } = 0.7f;
    
    /// <summary>
    /// The top_p value to use for generation
    /// </summary>
    public float TopP { get; set; } = 0.9f;
}
