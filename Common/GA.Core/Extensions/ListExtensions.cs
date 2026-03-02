namespace GA.Core.Extensions;

using GA.Core.Combinatorics;

[PublicAPI]
public static class ListExtensions
{
    extension<T>(IReadOnlyList<T> list)
    {
        public OrderedPair<T> ToPair() => new(list[0], list[1]);

        public TPair ToPair<TPair>(Func<T, T, TPair> pairFactory)
            where TPair : OrderedPair<T> =>
            pairFactory(list[0], list[1]);

        public Tuple<T, T, T> ToTuple3() => new(list[0], list[1], list[2]);

        public Tuple<T, T, T, T> ToTuple4() => new(list[0], list[1], list[2], list[3]);

        public Tuple<T, T, T, T, T> ToTuple5() => new(list[0], list[1], list[2], list[3], list[4]);

        public Tuple<T, T, T, T, T, T> ToTuple6() => new(list[0], list[1], list[2], list[3], list[4], list[5]);

        public Tuple<T, T, T, T, T, T, T> ToTuple7() => new(list[0], list[1], list[2], list[3], list[4], list[5], list[6]);

        public (T, T, T, T, T, T, T, T) ToTuple8() => (list[0], list[1], list[2], list[3], list[4], list[5], list[6], list[7]);

        public (T, T, T, T, T, T, T, T, T) ToTuple9() => (list[0], list[1], list[2], list[3], list[4], list[5], list[6], list[7], list[8]);

        public (T, T, T, T, T, T, T, T, T, T) ToTuple10() => (list[0], list[1], list[2], list[3], list[4], list[5], list[6], list[7], list[8], list[9]);

        public (T, T, T, T, T, T, T, T, T, T, T) ToTuple11() => (list[0], list[1], list[2], list[3], list[4], list[5], list[6], list[7], list[8], list[9], list[10]);

        public (T, T, T, T, T, T, T, T, T, T, T, T) ToTuple12() =>
            (list[0], list[1], list[2], list[3], list[4], list[5], list[6], list[7], list[8], list[9], list[10],
                list[11]);

        public (T, T, T, T, T, T, T, T, T, T, T, T, T) ToTuple13() =>
            (list[0], list[1], list[2], list[3], list[4], list[5], list[6], list[7], list[8], list[9], list[10],
                list[11], list[11]);
    }
}

