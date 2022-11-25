namespace GA.Core.Combinatorics;

using Collections;

/// <summary>
/// (T x T) variations
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
[PublicAPI]
public class CartesianProduct<T> : VariationsWithRepetitions<T>
    where T : IItemCollection<T>
{
    public CartesianProduct(Func<T, bool>? predicate = null)
        : base(T.Items, 2, predicate)
    {
    }

    /// <summary>
    /// Gets the collection of <see cref="Tuple{T,T}"/>.
    /// </summary>
    public IEnumerable<(T, T)> Tuples => this.GetTuples();

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(" ", Elements));
        var t = typeof(T).Name;
        sb.Append($" => {t} x {t}: {Count} variations");
        return sb.ToString();
    }
}

/// <summary>
/// Ordered (T x T) variations (Integer norm)
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
public class OrderedCartesianProduct<T> : OrderedCartesianProduct<T, int> 
    where T : IItemCollection<T>, INormed<T, int>
{
}