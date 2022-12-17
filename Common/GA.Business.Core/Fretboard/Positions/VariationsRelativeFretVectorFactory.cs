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

    /// <summary>
    /// Create a vector from a variation.
    /// </summary>
    /// <param name="variation">The <see cref="Variation{RelativeFret}"/></param>
    /// <returns>The <see cref="RelativeFretVector"/>.</returns>
    private RelativeFretVector Create(Variation<RelativeFret> variation)
    {
        var isPrimeForm = variation.Min().Value == 0;
        if (isPrimeForm)
        {
            // The variation represents vector is its prime form
            var equivalences = Equivalences.From[variation.Index].ToImmutableArray();
            var translations =
                equivalences
                    .Select(translation => _variations[translation.ToIndex])
                    .Select(Create)
                    .Cast<Translation>();
            return new PrimeForm(variation, new OrderedTranslationCollection(translations));
        }

        // The variation represents vector is its translated form
        var equivalence = Equivalences.To[variation.Index];
        return new Translation(variation, () => (PrimeForm) Create(_variations[equivalence.FromIndex]));
    }

    private class OrderedTranslationCollection : LazyCollectionBase<Translation>
    {
        public OrderedTranslationCollection(IEnumerable<Translation> items)
            : base(items.OrderBy(translation => translation.Index), "; ")
        {
        }
    }
}