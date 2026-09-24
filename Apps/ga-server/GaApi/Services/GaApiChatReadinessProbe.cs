namespace GaApi.Services;

using GA.Business.Core.Orchestration.Abstractions;

/// <summary>Bounds the configured provider's availability check; does not run paid inference.</summary>
public sealed class GaApiChatReadinessProbe(IChatService chatService) : IChatReadinessProbe
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromMilliseconds(750);

    public async Task<ChatReadinessResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CheckTimeout);
        try
        {
            var available = await chatService.IsAvailableAsync(timeout.Token).WaitAsync(CheckTimeout, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return new ChatReadinessResult(available,
                available ? "Configured chat provider is available." : "Configured chat provider is unavailable.");
        }
        catch (TimeoutException)
        {
            return new ChatReadinessResult(false, "Chat provider availability check timed out.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ChatReadinessResult(false, "Chat provider availability check timed out.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ChatReadinessResult(false, "Chat provider availability check failed.");
        }
    }
}
