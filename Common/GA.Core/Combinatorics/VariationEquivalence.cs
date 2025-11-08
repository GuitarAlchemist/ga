namespace GA.Core.Combinatorics;

/// <summary>
///     Abstract equivalence between two variations.
/// </summary>
/// <remarks>
///     See https://en.wikipedia.org/wiki/Symmetry_in_mathematics
///     See https://en.wikipedia.org/wiki/Invariant_(mathematics)
/// </remarks>
/// <param name="FromIndex">The source variation.</param>
/// <param name="ToIndex">The target variation.</param>
/// <param name="Value">The displacement value between from and to.</param>
public abstract record VariationEquivalence(
    BigInteger FromIndex,
    BigInteger ToIndex,
    int Value)
{
    /// <summary>
    ///     Concrete translation equivalence between two variations.
    /// </summary>
    /// <inheritdoc cref="VariationEquivalence" />
    public record Translation(
        BigInteger FromIndex,
        BigInteger ToIndex,
        int Value) : VariationEquivalence(FromIndex, ToIndex, Value)
    {
        public static readonly Translation None = new(BigInteger.Zero, BigInteger.Zero, 0);

        public override string ToString()
        {
            return $"{FromIndex} => T+{Value}: {ToIndex}";
        }
    }
}
