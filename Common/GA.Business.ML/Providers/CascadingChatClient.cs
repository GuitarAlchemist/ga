namespace GA.Business.ML.Providers;

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

/// <summary>
/// Wraps a primary <see cref="IChatClient"/> with a secondary fallback. The
/// secondary is invoked when the primary either cancels (timeout) or throws
/// a network-level exception before producing any output.
/// </summary>
/// <remarks>
/// <para>
/// Built for #193 to address the demos.guitaralchemist.com Ollama timeout
/// (15s) that turns every low-confidence orchestration path into a dead end.
/// With a cascade in place, Ollama failures fall through to Mistral instead
/// of surfacing the "reasoning service unavailable" message to the user.
/// </para>
/// <para>
/// For streaming, the fallback only fires if the primary throws before
/// yielding any tokens — partial streams stay with the primary, because
/// switching mid-stream would emit a confusing concatenation of two
/// different models' wording. Callers that need a guaranteed fallback
/// after partial output must implement their own retry policy on top.
/// </para>
/// </remarks>
public sealed class CascadingChatClient(
    IChatClient primary,
    IChatClient secondary,
    ILogger<CascadingChatClient>? logger = null) : IChatClient
{
    private readonly IChatClient _primary   = primary   ?? throw new ArgumentNullException(nameof(primary));
    private readonly IChatClient _secondary = secondary ?? throw new ArgumentNullException(nameof(secondary));
    private readonly ILogger<CascadingChatClient>? _logger = logger;

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Materialize once so we can replay against the secondary without
        // re-enumerating a single-use IEnumerable (e.g. a yielded sequence).
        var snapshot = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();

        try
        {
            return await _primary.GetResponseAsync(snapshot, options, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller-driven cancellation — do not cascade.
            throw;
        }
        catch (Exception primaryEx) when (ShouldCascade(primaryEx))
        {
            _logger?.LogWarning(
                primaryEx,
                "Primary chat client failed ({Kind}); cascading to secondary",
                primaryEx.GetType().Name);

            return await _secondary.GetResponseAsync(snapshot, options, cancellationToken).ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var snapshot = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();

        var primarySucceededAtLeastOnce = false;
        IAsyncEnumerator<ChatResponseUpdate>? enumerator = null;
        Exception? primaryFailure = null;

        try
        {
            try
            {
                enumerator = _primary.GetStreamingResponseAsync(snapshot, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Caller-driven cancellation — must not cascade. Mirrors the
                // MoveNextAsync guard below; Codex P1 review #225 caught this
                // pre-enumerator path violating the documented contract.
                throw;
            }
            catch (Exception ex) when (ShouldCascade(ex))
            {
                primaryFailure = ex;
            }

            while (enumerator is not null)
            {
                ChatResponseUpdate? next = null;
                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false)) break;
                    next = enumerator.Current;
                    primarySucceededAtLeastOnce = true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (!primarySucceededAtLeastOnce && ShouldCascade(ex))
                {
                    primaryFailure = ex;
                    break;
                }

                if (next is not null) yield return next;
            }
        }
        finally
        {
            if (enumerator is not null) await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        if (primaryFailure is not null)
        {
            _logger?.LogWarning(
                primaryFailure,
                "Primary streaming chat client failed before yielding any tokens ({Kind}); cascading to secondary",
                primaryFailure.GetType().Name);

            await foreach (var update in _secondary.GetStreamingResponseAsync(snapshot, options, cancellationToken)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                yield return update;
            }
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        if (serviceType == typeof(CascadingChatClient)) return this;
        // Surface the primary's metadata so observers see the headline provider.
        return _primary.GetService(serviceType, serviceKey)
            ?? _secondary.GetService(serviceType, serviceKey);
    }

    public void Dispose()
    {
        _primary.Dispose();
        _secondary.Dispose();
    }

    private static bool ShouldCascade(Exception ex) => ex switch
    {
        // OperationCanceledException covers TaskCanceledException too (the
        // shape HttpClient uses for its own timeouts). We treat any non-
        // caller-driven cancellation here as a primary-side failure — the
        // caller-driven case is filtered at each catch site.
        OperationCanceledException => true,
        HttpRequestException       => true,       // network / non-2xx
        TimeoutException           => true,
        _                          => false,
    };
}
