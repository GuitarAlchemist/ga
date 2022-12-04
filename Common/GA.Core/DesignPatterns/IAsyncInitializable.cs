namespace GA.Core.DesignPatterns;

/// <summary>
/// Async initializable object abstraction (No inits).
/// </summary>
public interface IAsyncInitializable
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Async initializable object abstraction (With inits).
/// </summary>
public interface IAsyncInitializable<in TInits>
{
    Task InitializeAsync(
        TInits inits,
        CancellationToken cancellationToken = default);
}