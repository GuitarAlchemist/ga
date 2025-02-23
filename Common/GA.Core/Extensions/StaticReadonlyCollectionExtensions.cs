namespace GA.Core.Extensions;

using Combinatorics;

public static class StaticReadonlyCollectionExtensions
{
    /// <summary>
    /// Gets all elements combinations
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    /// <param name="elements">The <see cref="IReadOnlyCollection{T}"/></param>
    /// <returns>The <see cref="Combinations{T}"/></returns>
    public static Combinations<T> ToCombinations<T>(this IReadOnlyCollection<T> elements) where T : notnull => new(elements);
}