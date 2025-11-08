namespace GA.Business.Core.Fretboard.Positions;

using GA.Core.Combinatorics;
using Primitives;
using static Primitives.RelativeFretVector;

/// <summary>
///     Factory for <see cref="RelativeFretVector" /> variations.
/// </summary>
[PublicAPI]
public class VariationsRelativeFretVectorFactory(VariationsWithRepetitions<RelativeFret> variations)
    : IEnumerable<RelativeFretVector>
{
    private readonly VariationsWithRepetitions<RelativeFret> _variations =
        variations ?? throw new ArgumentNullException(nameof(variations));

    /// <summary>
    ///     Gets the <see cref="VariationEquivalenceCollection.Translation{RelativeFret}" />.
    /// </summary>
    public VariationEquivalenceCollection.Translation<RelativeFret> Equivalences { get; } = new(variations);

    /// <summary>
    ///     Create a vector from a variation.
    /// </summary>
    /// <param name="variation">The <see cref="Variation{RelativeFret}" /></param>
    /// <returns>The <see cref="RelativeFretVector" />.</returns>
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
        return new Translation(variation, () => (PrimeForm)Create(_variations[equivalence.FromIndex]));
    }

    private class OrderedTranslationCollection(IEnumerable<Translation> items)
        : LazyCollectionBase<Translation>(items.OrderBy(translation => translation.Increment), "; ");

    #region IEnumerable<RelativeFretVector> Members

    public IEnumerator<RelativeFretVector> GetEnumerator()
    {
        return _variations.Select(Create).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    #endregion
}
