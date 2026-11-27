namespace GA.Business.Core.Orchestration.Clients;

/// <summary>
/// Single-prompt text generation for <see cref="Services.OllamaGroundedNarrator"/>,
/// <see cref="Services.QueryUnderstandingService"/> and the performance-intent services, without
/// naming the model provider.
/// </summary>
/// <remarks>
/// <see cref="ChatClientTextGenerator"/> is the default adapter. <see cref="OllamaGenerateClient"/>
/// is used only with <c>AI:Generate:Provider=ollama</c>, or when no chat client is registered.
/// </remarks>
public interface ITextGenerator
{
    /// <summary>Generates a free-text reply to <paramref name="prompt"/>.</summary>
    /// <param name="prompt">Prompt text.</param>
    /// <param name="temperature">Sampling temperature; adapters whose model rejects it ignore it.</param>
    /// <param name="numPredict">Optional max token limit.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<string> GenerateAsync(
        string prompt,
        float temperature = 0.7f,
        int? numPredict = null,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a JSON reply constrained by <paramref name="jsonSchema"/> and deserialises it into
    /// <typeparamref name="T"/>. Returns <see langword="null"/> when the reply is empty.
    /// </summary>
    /// <param name="prompt">Prompt text.</param>
    /// <param name="jsonSchema">A JSON Schema object, e.g. a <see cref="System.Text.Json.Nodes.JsonNode"/>.</param>
    /// <param name="temperature">Sampling temperature; adapters whose model rejects it ignore it.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<T?> GenerateStructuredAsync<T>(
        string prompt,
        object jsonSchema,
        float temperature = 0.1f,
        CancellationToken ct = default) where T : class;
}
