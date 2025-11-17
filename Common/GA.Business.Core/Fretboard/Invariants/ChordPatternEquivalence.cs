namespace GA.Business.Core.Fretboard.Invariants;

using System.Numerics;

/// <summary>
/// Describes a translation equivalence between two chord patterns.
/// </summary>
[PublicAPI]
public sealed class ChordPatternEquivalence(
    PatternId fromPattern,
    PatternId toPattern,
    int translationValue,
    BigInteger fromIndex,
    BigInteger toIndex)
{
    public PatternId FromPattern { get; } = fromPattern;
    public PatternId ToPattern { get; } = toPattern;
    public int TranslationValue { get; } = translationValue;
    public BigInteger FromIndex { get; } = fromIndex;
    public BigInteger ToIndex { get; } = toIndex;

    public bool IsPrimeForm => TranslationValue == 0 || FromPattern == ToPattern;
    public int FretOffset => TranslationValue;

    public override string ToString()
    {
        var from = FromPattern.ToPatternString();
        var to = ToPattern.ToPatternString();
        if (IsPrimeForm)
        {
            return $"Prime: {from}";
        }

        var sign = FretOffset >= 0 ? "+" : "-";
        return $"{from} {sign}{Math.Abs(FretOffset)} => {to}";
    }
}
