namespace GaApi.Services;

/// <summary>
///     Cheap reachability check for the configured chat provider (<c>AI:ChatProvider</c>).
///     Never runs paid inference: generation goes through <see cref="Microsoft.Extensions.AI.IChatClient" />.
/// </summary>
public interface IChatProviderAvailability
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
