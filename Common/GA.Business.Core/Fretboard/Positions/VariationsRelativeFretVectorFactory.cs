namespace GA.Business.Core.Fretboard.Positions;

using GA.Core.Collections;
using GA.Core.Combinatorics;
using Primitives;
using static Primitives.RelativeFretVector;

/// <summary>
/// Factory for <see cref="RelativeFretVector"/> variations.
/// </summary>
[PublicAPI]
public class VariationsRelativeFretVectorFactory : IEnumerable<RelativeFretVector>
{
    #region IEnumerable<RelativeFretVector> Members

    public IEnumerator<RelativeFretVector> GetEnumerator() => _variations.Select(Create).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion

    private readonly VariationsWithRepetitions<RelativeFret> _variations;

    public VariationsRelativeFretVectorFactory(VariationsWithRepetitions<RelativeFret> variations)
    {
        _variations = variations ?? throw new ArgumentNullException(nameof(variations));
        Equivalences = new(variations);
    }

    /// <summary>
    /// Gets the <see cref="VariationEquivalenceCollection.Translation{RelativeFret}"/>.
    /// </summary>
    public VariationEquivalenceCollection.Translation<RelativeFret> Equivalences { get; }

    private RelativeFretVector Create(Variation<RelativeFret> variation)
    {
        var isNormalized = variation.Min().Value == 0;
        if (isNormalized)
        {
            var equivalences = Equivalences.From[variation.Index].ToImmutableArray();
            var translated =
                equivalences
                    .Select(translation => _variations[translation.ToIndex])
                    .Select(Create)
                    .Cast<Translated>();
            return new Normalized(variation, new NormalizedTranslations(translated));
        }

        // Translation
        var equivalence = Equivalences.To[variation.Index];
        return new Translated(variation, () => (Normalized) Create(_variations[equivalence!.FromIndex]));
    }

    private class NormalizedTranslations : LazyCollectionBase<Translated>
    {
        public NormalizedTranslations(IEnumerable<Translated> items)
            : base(items, "; ")
        {
        }
    }
}