namespace GaApi.Configuration;

/// <summary>
///     Configuration options for the chat orchestration pipeline.
///     Mirrors typical Spring Boot style strongly-typed options.
/// </summary>
public sealed class ChatbotOptions
{
    public const string SectionName = "Chatbot";

    /// <summary>
    ///     Primary conversational model identifier registered in Ollama.
    /// </summary>
    public string Model { get; set; } = "llama3.2:3b";

    /// <summary>
    ///     Maximum number of back-and-forth messages (user + assistant) to retain.
    /// </summary>
    public int HistoryLimit { get; set; } = 12;

    /// <summary>
    ///     Whether semantic search enrichment is enabled.
    /// </summary>
    public bool EnableSemanticSearch { get; set; } = true;

    /// <summary>
    ///     Maximum number of semantic search documents to fetch before building context.
    /// </summary>
    public int SemanticSearchLimit { get; set; } = 5;

    /// <summary>
    ///     How many semantic snippets to embed in the system prompt.
    /// </summary>
    public int SemanticContextDocuments { get; set; } = 3;

    /// <summary>
    ///     Timeout applied when waiting for streaming completions (in seconds).
    /// </summary>
    public int StreamTimeoutSeconds { get; set; } = 60;
}
