namespace GaChatbot.Components;

/// <summary>
/// Ensures the chatbot maintains a helpful, expert, and encouraging musical tone.
/// </summary>
public class ToneEvaluator
{
    private readonly string[] _encouragingPhrases = 
    [
        "Keep practicing!",
        "That's a great choice for this style.",
        "You're making progress with your harmonic understanding.",
        "Don't worry, these stretches get easier with time."
    ];

    public string EnhanceResponse(string rawResponse)
    {
        // Simple heuristic for now - randomly add an encouraging phrase
        var random = new Random();
        var suffix = _encouragingPhrases[random.Next(_encouragingPhrases.Length)];
        
        return $"{rawResponse} {suffix}";
    }
}
