namespace GA.Core.Extensions;

[PublicAPI]
public static class ListExtensions
{
    public static Pair<T> ToPair<T>(this IReadOnlyList<T> list) => new(list[0], list[1]);
    public static TPair ToPair<T, TPair>(this IReadOnlyList<T> list, Func<T,T,TPair> pairFactory) where TPair : Pair<T> => pairFactory(list[0], list[1]);
    public static Tuple<T, T, T> ToTuple3<T>(this IReadOnlyList<T> list) => new(list[0], list[1], list[2]);
    public static Tuple<T, T, T, T> ToTuple4<T>(this IReadOnlyList<T> list) => new(list[0], list[1], list[2], list[3]);
    public static Tuple<T, T, T, T, T> ToTuple5<T>(this IReadOnlyList<T> list) => new(list[0], list[1], list[2], list[3], list[4]);
    public static Tuple<T, T, T, T, T, T> ToTuple6<T>(this IReadOnlyList<T> list) => new(list[0], list[1], list[2], list[3], list[4], list[5]);
    public static Tuple<T, T, T, T, T, T, T> ToTuple7<T>(this IReadOnlyList<T> list) => new(list[0], list[1], list[2], list[3], list[4], list[5], list[6]);
    public static (T, T, T, T, T, T, T, T) ToTuple8<T>(this IReadOnlyList<T> list) => (list[0], list[1], list[2], list[3], list[4], list[5], list[6], list[7]);
    public static (T, T, T, T, T, T, T, T, T) ToTuple9<T>(this IReadOnlyList<T> list) => (list[0], list[1], list[2], list[3], list[4], list[5], list[6], list[7], list[8]);
    public static (T ,T, T, T, T, T, T, T, T, T) ToTuple10<T>(this IReadOnlyList<T> list) => (list[0], list[1], list[2], list[3], list[4], list[5], list[6], list[7], list[8], list[9]);
    public static (T ,T, T, T, T, T, T, T, T, T, T) ToTuple11<T>(this IReadOnlyList<T> list) => (list[0], list[1], list[2], list[3], list[4], list[5], list[6], list[7], list[8], list[9], list[10]);
    public static (T ,T, T, T, T, T, T, T, T, T, T, T) ToTuple12<T>(this IReadOnlyList<T> list) => (list[0], list[1], list[2], list[3], list[4], list[5], list[6], list[7], list[8], list[9], list[10], list[11]);
    public static (T ,T, T, T, T, T, T, T, T, T, T, T, T) ToTuple13<T>(this IReadOnlyList<T> list) => (list[0], list[1], list[2], list[3], list[4], list[5], list[6], list[7], list[8], list[9], list[10], list[11], list[11]);
}