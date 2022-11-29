namespace GA.Core.Combinatorics;

using Extensions;

[PublicAPI]
public static class VariationExtensions
{
    public static IEnumerable<Pair<T>> GetPairs<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.ToPair());
    public static IEnumerable<TPair> GetPairs<T, TPair>(this IEnumerable<Variation<T>> variations, Func<T,T,TPair> pairFactory) where TPair: Pair<T> => variations.Select(variation => variation.ToPair(pairFactory));
    public static IEnumerable<Tuple<T, T, T>> Get3Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.ToTuple3());
    public static IEnumerable<Tuple<T, T, T, T>> Get4Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.ToTuple4());
    public static IEnumerable<Tuple<T, T, T, T, T>> Get5Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.ToTuple5());
    public static IEnumerable<Tuple<T, T, T, T, T, T>> Get6Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.ToTuple6());
    public static IEnumerable<Tuple<T, T, T, T, T, T, T>> Get7Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.ToTuple7());
    public static IEnumerable<(T, T, T, T, T, T, T, T)> Get8Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.ToTuple8());
    public static IEnumerable<(T, T, T, T, T, T, T, T, T)> Get9Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.ToTuple9());
    public static IEnumerable<(T, T, T, T, T, T, T, T, T, T)> Get10Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.ToTuple10());
    public static IEnumerable<(T, T, T, T, T, T, T, T, T, T, T)> Get11Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.ToTuple11());
    public static IEnumerable<(T, T, T, T, T, T, T, T, T, T, T, T)> Get12Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.ToTuple12());
    public static IEnumerable<(T, T, T, T, T, T, T, T, T, T, T, T, T)> Get13Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.ToTuple13());
}
