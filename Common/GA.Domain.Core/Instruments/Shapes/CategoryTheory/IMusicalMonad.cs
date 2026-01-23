namespace GA.Domain.Core.Instruments.Shapes.CategoryTheory;

/// <summary>
///     A monad for composing musical transformations
/// </summary>
/// <typeparam name="T">The type being transformed</typeparam>
/// <remarks>
///     A monad is a functor M with two natural transformations:
///     - ? (unit/return): Id ? M - wraps a value
///     - µ (join/flatten): M ° M ? M - flattens nested structure
///     Monad laws:
///     1. Left identity: µ ° ?_M = id
///     2. Right identity: µ ° M(?) = id
///     3. Associativity: µ ° M(µ) = µ ° µ_M
///     Musical applications:
///     - Compose transformations with error handling
///     - Chain voice leading operations
///     - Sequence harmonic progressions
///     - Handle optional/multiple voicings
///     Example: Maybe monad for optional voicings
///     - Some(voicing) if voicing exists
///     - None if no valid voicing
/// </remarks>
[PublicAPI]
public interface IMusicalMonad<T>
{
    /// <summary>
    ///     Extract the value (if present)
    /// </summary>
    T? Value { get; }

    /// <summary>
    ///     Is the value present?
    /// </summary>
    bool HasValue { get; }

    /// <summary>
    ///     Unit/Return: Wrap a value in the monad
    /// </summary>
    IMusicalMonad<T> Unit(T value);

    /// <summary>
    ///     Bind/FlatMap: Chain operations
    /// </summary>
    /// <remarks>
    ///     Bind allows sequencing operations:
    ///     m.Bind(f).Bind(g) = m.Bind(x => f(x).Bind(g))
    /// </remarks>
    IMusicalMonad<TResult> Bind<TResult>(Func<T, IMusicalMonad<TResult>> f);
}