namespace GA.Domain.Core.Instruments.Shapes.CategoryTheory;

/// <summary>
///     Extension methods for monadic operations
/// </summary>
[PublicAPI]
public static class MonadExtensions
{
    // NOTE: These extension methods have type system issues and are commented out
    // The IMusicalMonad<T>.Unit method returns IMusicalMonad<T>, not IMusicalMonad<TResult>
    // This makes it impossible to implement Map and Sequence correctly without changing the interface

    // /// <summary>
    // /// Map/FMap: Apply a function inside the monad
    // /// </summary>
    // public static IMusicalMonad<TResult> Map<T, TResult>(
    //     this IMusicalMonad<T> monad,
    //     Func<T, TResult> f)
    // {
    //     // Cannot implement - Unit returns wrong type
    //     throw new NotImplementedException();
    // }

    // /// <summary>
    // /// Sequence: Convert list of monads to monad of list
    // /// </summary>
    // public static IMusicalMonad<IReadOnlyList<T>> Sequence<T>(
    //     this IEnumerable<IMusicalMonad<T>> monads)
    // {
    //     // Cannot implement - Unit returns wrong type
    //     throw new NotImplementedException();
    // }
}