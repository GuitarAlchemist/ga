namespace GA.Core.DesignPatterns;

/// <summary>
/// Async initializable object abstraction.
/// </summary>
public interface IAsyncInitializable
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Async initializable object abstraction (With inits object).
/// </summary>
public interface IAsyncInitializable<in TInits>
{
    Task InitializeAsync(
        TInits inits,
        CancellationToken cancellationToken = default);
}