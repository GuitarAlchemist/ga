namespace GA.Core.Combinatorics;

using Collections;
using Extensions;

/// <summary>
/// (T x T) variations
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <typeparam name="TPair">The item pair type.</typeparam>
[PublicAPI]
public abstract class CartesianProduct<T, TPair> : IEnumerable<TPair>
    where T : IItemCollection<T>
    where TPair : IPair<T>
{
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<TPair> GetEnumerator() => Pairs.GetEnumerator();

    private readonly PairFactory<T, TPair>? _pairFactory;
        
    protected CartesianProduct(
        PairFactory<T, TPair>? pairFactory,
        Func<T, bool>? predicate = null)
    {
        _pairFactory = pairFactory;
        Variations = new(T.Items, 2, predicate);
    }

    /// <summary>
    /// Gets the <see cref="VariationsWithRepetitions{T}"/>
    /// </summary>
    public VariationsWithRepetitions<T> Variations { get; }

    /// <summary>
    /// Gets the collection of <see cref="Tuple{T,T}"/>.
    /// </summary>
    public IEnumerable<TPair> Pairs => 
        _pairFactory == null 
            ? Variations.GetPairs().Cast<TPair>() 
            : Variations.GetPairs(_pairFactory);

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(" ", Variations.Elements));
        var t = typeof(T).Name;
        sb.Append($" => {t} x {t}: {Count} variations");
        return sb.ToString();
    }


    public BigInteger Count => Variations.Count;
}

/// <summary>
/// (T x T) variations
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
[PublicAPI]
public class CartesianProduct<T> : CartesianProduct<T, Pair<T>>
    where T : IItemCollection<T>
{
    public CartesianProduct(Func<T, bool>? predicate = null)
        : base(null, predicate)
    {
    }
}