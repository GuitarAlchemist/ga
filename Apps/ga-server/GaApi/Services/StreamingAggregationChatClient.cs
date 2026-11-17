namespace GaApi.Services;

using Microsoft.Extensions.AI;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;

/// <summary>
///     Answers non-streaming requests by aggregating the inner client's stream.
/// </summary>
/// <remarks>
///     The local providers (Ollama, Docker Model Runner) used to be called only in streaming mode, so the
///     transport timeout bounded the wait for the first bytes, not the whole generation. Aggregating the
///     stream keeps that: a slow but progressing generation is not cut off by the timeout.
/// </remarks>
public sealed class StreamingAggregationChatClient(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<AiChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        GetStreamingResponseAsync(messages, options, cancellationToken).ToChatResponseAsync(cancellationToken);
}
