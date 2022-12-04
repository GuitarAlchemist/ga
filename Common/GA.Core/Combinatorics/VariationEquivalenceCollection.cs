namespace GA.Core.Combinatorics;

using Extensions;

/// <summary>
/// Abstract collection of variation equivalences.
/// </summary>
public abstract class VariationEquivalenceCollection
{
    /// <summary>
    /// Concrete collection of variation translation equivalences.
    /// </summary>
    public class Translation<T> : VariationEquivalenceCollection
        where T : struct, IValueObject<T>
    {
        private readonly Lazy<ImmutableList<VariationEquivalence.Translation>> _lazyEquivalences;
        private readonly Lazy<ImmutableDictionary<BigInteger, VariationEquivalence.Translation>> _lazyToEquivalences;
        private readonly Lazy<ILookup<BigInteger, VariationEquivalence.Translation>> _lazyFromEquivalences;

        public Translation(VariationsWithRepetitions<T> variationsWithRepetitions)
            : this(variationsWithRepetitions.GetIndex,
                   variationsWithRepetitions)
        {
        }

        public Translation(
            Func<IEnumerable<T>, BigInteger> indexProvider,
            IEnumerable<Variation<T>> variations)
        {
            if (indexProvider == null) throw new ArgumentNullException(nameof(indexProvider));
            if (variations == null) throw new ArgumentNullException(nameof(variations));

            _lazyEquivalences = new(() => GetTranslationEquivalences(indexProvider, variations));
            _lazyToEquivalences = new(() => Equivalences.ToImmutableDictionary(Equivalence => Equivalence.ToIndex));
            _lazyFromEquivalences = new(() => Equivalences.ToLookup(Equivalence => Equivalence.FromIndex));
        }

        public IReadOnlyCollection<VariationEquivalence.Translation> Equivalences => _lazyEquivalences.Value;
        public ImmutableDictionary<BigInteger, VariationEquivalence.Translation> To => _lazyToEquivalences.Value;
        public ILookup<BigInteger, VariationEquivalence.Translation> From => _lazyFromEquivalences.Value;

        public override string ToString() => $"{Equivalences.Count}";

        private static ImmutableList<VariationEquivalence.Translation> GetTranslationEquivalences(
            Func<IEnumerable<T>, BigInteger> indexProvider,
            IEnumerable<Variation<T>> variations)
        {
            if (indexProvider == null) throw new ArgumentNullException(nameof(indexProvider));
            if (variations == null) throw new ArgumentNullException(nameof(variations));

            var mapItemsBuilder = ImmutableList.CreateBuilder<VariationEquivalence.Translation>();
            foreach (var variation in variations)
            {
                if (TryGetEquivalence(variation, indexProvider, out var mapItem)) mapItemsBuilder.Add(mapItem);
            }
            return mapItemsBuilder.ToImmutable();

            static bool TryGetEquivalence(
                Variation<T> variation,
                Func<IEnumerable<T>, BigInteger> indexProvider,
                out VariationEquivalence.Translation equivalence)
            {
                var isNormalized = variation.Any(T => T.Value == 0);
                if (isNormalized) // Already normalized, no Equivalence
                {
                    equivalence = VariationEquivalence.Translation.None;
                    return false;
                }

                // The two variations have a translation Equivalence
                equivalence = new(
                    indexProvider(variation.ToNormalizedArray()),
                    variation.Index,
                    variation.Min().Value);
                return true;
            }
        }
    }
}

