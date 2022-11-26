namespace GA.Core;

public readonly struct Pair<T> : IPair<T>
{
    public static Pair<T> FromTuple(Tuple<T, T> tuple) => new(tuple.Item1, tuple.Item2);
    public static Pair<T> FromTuple((T, T) tuple) => new(tuple.Item1, tuple.Item2);

    public Pair(T item1, T item2)
    {
        Item1 = item1;
        Item2 = item2;
    }

    public T Item1 { get; }
    public T Item2 { get; }
}