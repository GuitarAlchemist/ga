namespace GaChatbot.Api.Services;

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

/// <summary>
/// Wraps the host's <see cref="IChatClient"/> so every LLM generation, from the
/// orchestrator's agents, the direct chat or the narrator, updates
/// <see cref="ChatGenerationHealth"/>. Deterministic answers never reach this
/// client, so they never count as a generation. A call whose token was
/// cancelled records nothing: the caller decided, or the caller's own timeout
/// is recorded by the caller.
/// </summary>
public sealed class GenerationHealthChatClient(IChatClient inner, ChatGenerationHealth health)
    : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ChatResponse response;
        try
        {
            response = await base.GetResponseAsync(messages, options, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            health.RecordFailure(exception);
            throw;
        }

        health.RecordSuccess();
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var updates = base.GetStreamingResponseAsync(messages, options, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            try
            {
                if (!await updates.MoveNextAsync())
                {
                    break;
                }
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                health.RecordFailure(exception);
                throw;
            }

            yield return updates.Current;
        }

        health.RecordSuccess();
    }
}
