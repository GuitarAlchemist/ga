namespace GA.Business.ML.Agents;

using System;
using System.Threading;

/// <summary>One LLM round trip made by an agent.</summary>
/// <param name="AgentId">Agent that made the call.</param>
/// <param name="Pass">Role of the call in the agent's flow: <c>single</c>, <c>stream</c>, or
/// <c>draft</c> / <c>critique</c> / <c>rewrite</c> for the self-critique loop.</param>
/// <param name="Model">Model id the provider reported, when it reported one.</param>
/// <param name="ElapsedMs">Wall time of the call, tool rounds included.</param>
/// <param name="FirstTokenMs">Time to the first streamed text, for streaming calls.</param>
/// <param name="InputTokens">Input tokens the provider reported.</param>
/// <param name="OutputTokens">Output tokens the provider reported.</param>
/// <param name="ToolCalls">Function calls the model made during the call.</param>
/// <param name="ResponseLength">Characters of answer text.</param>
/// <param name="Succeeded">False when the call threw.</param>
public sealed record LlmCall(
    string AgentId,
    string Pass,
    string? Model,
    long ElapsedMs,
    long? FirstTokenMs,
    long? InputTokens,
    long? OutputTokens,
    int ToolCalls,
    int ResponseLength,
    bool Succeeded);

/// <summary>
/// Hands the LLM calls agents make to whoever observes the current request.
/// </summary>
/// <remarks>
/// Agents live below the orchestration layer and cannot see its per-request trace capture, so
/// the orchestration layer registers an observer for the request's async flow and agents report
/// to it. Without an observer, reporting does nothing.
/// </remarks>
public static class LlmCallTrace
{
    private static readonly AsyncLocal<Action<LlmCall>?> Current = new();

    /// <summary>Observes the calls reported in this async flow until the result is disposed.</summary>
    public static IDisposable Observe(Action<LlmCall> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var previous = Current.Value;
        Current.Value = observer;
        return new Restore(previous);
    }

    /// <summary>Reports a call to the current observer, if any.</summary>
    public static void Report(LlmCall call) => Current.Value?.Invoke(call);

    private sealed class Restore(Action<LlmCall>? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
