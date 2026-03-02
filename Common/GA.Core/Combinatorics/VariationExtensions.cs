namespace GA.Core.Combinatorics;

using Extensions;

[PublicAPI]
public static class VariationExtensions
{
    extension<T>(IEnumerable<Variation<T>> variations)
    {
        public IEnumerable<OrderedPair<T>> GetPairs() => variations.Select(variation => variation.ToPair());

        public IEnumerable<TPair> GetPairs<TPair>(
            Func<T, T, TPair> pairFactory) where TPair : OrderedPair<T> =>
            variations.Select(variation => variation.ToPair(pairFactory));

        public IEnumerable<Tuple<T, T, T>> Get3Tuples() => variations.Select(variation => variation.ToTuple3());

        public IEnumerable<Tuple<T, T, T, T>> Get4Tuples() => variations.Select(variation => variation.ToTuple4());

        public IEnumerable<Tuple<T, T, T, T, T>> Get5Tuples() => variations.Select(variation => variation.ToTuple5());

        public IEnumerable<Tuple<T, T, T, T, T, T>> Get6Tuples() => variations.Select(variation => variation.ToTuple6());

        public IEnumerable<Tuple<T, T, T, T, T, T, T>> Get7Tuples() => variations.Select(variation => variation.ToTuple7());

        public IEnumerable<(T, T, T, T, T, T, T, T)> Get8Tuples() => variations.Select(variation => variation.ToTuple8());

        public IEnumerable<(T, T, T, T, T, T, T, T, T)> Get9Tuples() => variations.Select(variation => variation.ToTuple9());

        public IEnumerable<(T, T, T, T, T, T, T, T, T, T)> Get10Tuples() => variations.Select(variation => variation.ToTuple10());

        public IEnumerable<(T, T, T, T, T, T, T, T, T, T, T)> Get11Tuples() => variations.Select(variation => variation.ToTuple11());

        public IEnumerable<(T, T, T, T, T, T, T, T, T, T, T, T)> Get12Tuples() => variations.Select(variation => variation.ToTuple12());

        public IEnumerable<(T, T, T, T, T, T, T, T, T, T, T, T, T)> Get13Tuples() => variations.Select(variation => variation.ToTuple13());

        public ImmutableDictionary<BigInteger, Variation<T>> ToIndexDictionary() => variations.ToImmutableDictionary(variation => variation.Index);
    }
}
