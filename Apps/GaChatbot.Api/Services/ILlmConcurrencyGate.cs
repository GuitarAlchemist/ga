namespace GaChatbot.Api.Services;

public interface ILlmConcurrencyGate
{
    ValueTask<bool> TryEnterAsync(CancellationToken cancellationToken = default);

    void Release();
}

public sealed class LlmConcurrencyGate : ILlmConcurrencyGate, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(3, 3);

    public async ValueTask<bool> TryEnterAsync(CancellationToken cancellationToken = default) =>
        await _semaphore.WaitAsync(TimeSpan.Zero, cancellationToken);

    public void Release() => _semaphore.Release();

    public void Dispose() => _semaphore.Dispose();
}
