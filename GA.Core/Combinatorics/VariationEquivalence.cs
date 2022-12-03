namespace GA.Core.Combinatorics;

/// <summary>
/// Abstract equivalence between two variations.
/// </summary>
/// <param name="FromIndex">The source variation.</param>
/// <param name="ToIndex">The target variation.</param>
/// <param name="Value">The displacement value between from to to.</param>
public abstract record VariationEquivalence(
    BigInteger FromIndex,
    BigInteger ToIndex,
    int Value)
{
    /// <summary>
    /// Concrete translation equivalence between two variations.
    /// </summary>
    /// <inheritdoc cref="VariationEquivalence"/>
    public record Translation(
        BigInteger FromIndex,
        BigInteger ToIndex,
        int Value) : VariationEquivalence(FromIndex, ToIndex, Value)
    {
        public static readonly Translation None = new(BigInteger.Zero, BigInteger.Zero, 0);

        public override string ToString() => $"{FromIndex} => {ToIndex} - Translation: {Value}";
    }

    /// <summary>
    /// Concrete rotation equivalence between two variations.
    /// </summary>
    /// <inheritdoc cref="VariationEquivalence"/>
    public record Rotation(
        BigInteger FromIndex,
        BigInteger ToIndex,
        int Value) : VariationEquivalence(FromIndex, ToIndex, Value)
    {
        public static readonly Translation None = new(BigInteger.Zero, BigInteger.Zero, 0);

        public override string ToString() => $"{FromIndex} => {ToIndex} - Rotation: {Value}";
    }
}
