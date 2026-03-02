namespace GA.Core.Extensions;

using Combinatorics;

public static class StaticReadonlyCollectionExtensions
{
    extension<T>(IReadOnlyCollection<T> elements) where T : notnull
    {
        /// <summary>
        ///     Gets all elements combinations
        /// </summary>
        /// <returns>The <see cref="Combinations{T}" /></returns>
        public Combinations<T> ToCombinations() => new(elements);
    }
}
