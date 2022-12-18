﻿namespace GA.Core.Combinatorics;

/// <summary>
/// Abstract collection of variation equivalences.
/// </summary>
public abstract class VariationEquivalenceCollection
{
    /// <summary>
    /// Concrete collection of variation translation equivalences.
    /// </summary>
    public class Translation<T> : VariationEquivalenceCollection
        where T : struct, IRangeValueObject<T>
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
            _lazyToEquivalences = new(() => Equivalences.ToImmutableDictionary(equivalence => equivalence.ToIndex));
            _lazyFromEquivalences = new(() => Equivalences.ToLookup(equivalence => equivalence.FromIndex));
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
                var isPrimeForm = variation.Any(T => T.Value == 0);
                if (isPrimeForm) // Already prime form, no Equivalence
                {
                    equivalence = VariationEquivalence.Translation.None;
                    return false;
                }

                // The two variations have a translation equivalence
                equivalence = new(
                    indexProvider(ToPrimeForm(variation)),
                    variation.Index,
                    variation.Min().Value);
                return true;

                static IEnumerable<T> ToPrimeForm<T>(IEnumerable<T> items) 
                    where T : struct, IRangeValueObject<T>
                {
                    if (items == null) throw new ArgumentNullException(nameof(items));
                    if (items is not IReadOnlyCollection<T> collection) collection = items.ToImmutableArray();

                    var minItem = collection.Min();
                    return collection.Select(item => T.FromValue(item.Value - minItem.Value));
                }
            }
        }
    }
}

