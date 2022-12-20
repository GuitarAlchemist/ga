namespace GA.Core;

/// <summary>
/// A pair with a norm.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <typeparam name="TNorm">The norm type.</typeparam>
[PublicAPI]
public sealed record NormedPair<T, TNorm>(Pair<T> Pair) : Pair<T>(Pair)
    where T : IStaticNorm<T, TNorm>
    where TNorm : struct, IValueObject<TNorm>
{
    /// <summary>
    /// Gets the <see cref="TNorm"/> norm.
    /// </summary>
    public TNorm Norm { get; } = T.GetNorm(Pair.Item1, Pair.Item2);

    /// <summary>
    /// Creates a 2-tuple.
    /// </summary>
    /// <returns>The <see cref="Tuple{T,T}"/></returns>
    public Tuple<T,T> ToTuple() => Tuple.Create(Item1, Item2);

    public override string ToString() => $"({Item1}, {Item2}) => {Norm}";
}