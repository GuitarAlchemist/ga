namespace GaApi.Services;

/// <summary>
/// Shared concurrency gate for LLM calls. Applied to both the SignalR hub and the REST controller
/// so that they draw from the same pool and cannot independently saturate Ollama.
/// </summary>
public interface ILlmConcurrencyGate
{
    /// <summary>Attempt to enter the gate. Returns false immediately if all slots are taken.</summary>
    ValueTask<bool> TryEnterAsync(CancellationToken cancellationToken = default);

    void Release();
}

/// <summary>SemaphoreSlim-backed implementation (3 concurrent LLM calls).</summary>
public sealed class LlmConcurrencyGate : ILlmConcurrencyGate, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(3, 3);

    public async ValueTask<bool> TryEnterAsync(CancellationToken cancellationToken = default) =>
        await _semaphore.WaitAsync(TimeSpan.Zero, cancellationToken);

    public void Release() => _semaphore.Release();

    public void Dispose() => _semaphore.Dispose();
}
