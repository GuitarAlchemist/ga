namespace GA.Business.Core.Orchestration.Abstractions;

/// <summary>
/// Shared concurrency gate for LLM calls. One process-wide owner so every chat
/// transport (REST, SignalR, AG-UI) draws from the same pool and no single
/// surface can independently saturate the model backend.
/// </summary>
/// <remarks>
/// Moved out of GaApi into the host-neutral orchestration layer as part of the
/// chat intake seam (campaign slice #1): the seam owns gating, so the gate has to
/// live where the seam lives. Transports that have not yet migrated to
/// <see cref="IChatIntake"/> still acquire this same singleton directly.
/// </remarks>
public interface ILlmConcurrencyGate
{
    /// <summary>Attempt to enter the gate. Returns false immediately if all slots are taken.</summary>
    ValueTask<bool> TryEnterAsync(CancellationToken cancellationToken = default);

    void Release();
}
