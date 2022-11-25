namespace GA.Core.Combinatorics;

public static class VariationExtensions
{
    public static IEnumerable<(T, T)> GetTuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.Tuple());
    public static IEnumerable<(T, T, T)> Get3Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.Tuple3());
    public static IEnumerable<(T, T, T, T)> Get4Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.Tuple4());
    public static IEnumerable<(T, T, T, T, T)> Get5Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.Tuple5());
    public static IEnumerable<(T, T, T, T, T, T)> Get6Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.Tuple6());
    public static IEnumerable<(T, T, T, T, T, T, T)> Get7Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.Tuple7());
    public static IEnumerable<(T, T, T, T, T, T, T, T)> Get8Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.Tuple8());
    public static IEnumerable<(T, T, T, T, T, T, T, T, T)> Get9Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.Tuple9());
    public static IEnumerable<(T, T, T, T, T, T, T, T, T, T)> Get10Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.Tuple10());
    public static IEnumerable<(T, T, T, T, T, T, T, T, T, T, T)> Get11Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.Tuple11());
    public static IEnumerable<(T, T, T, T, T, T, T, T, T, T, T, T)> Get12Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.Tuple12());
    public static IEnumerable<(T, T, T, T, T, T, T, T, T, T, T, T, T)> Get13Tuples<T>(this IEnumerable<Variation<T>> variations) => variations.Select(variation => variation.Tuple13());
}