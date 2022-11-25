namespace GA.Core.Combinatorics;

using Collections;

/// <summary>
/// Ordered (T x T) variations
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <typeparam name="TNorm">The norm type.</typeparam>
public class OrderedCartesianProduct<T,TNorm> : CartesianProduct<T>
    where T : IItemCollection<T>, INormed<T, TNorm> 
    where TNorm : struct
{
    private readonly Lazy<ImmutableDictionary<(T, T), TNorm>> _lazyNormByTuple;

    public OrderedCartesianProduct()
    {
        _lazyNormByTuple = new(GetNormByTuple);
    }

    public ImmutableDictionary<(T, T), TNorm> NormByTuple => _lazyNormByTuple.Value;

    public override string ToString()
    {
        return $"{base.ToString()}; Norms: {GetNorms()}";

        StringBuilder GetNorms()
        {
            var sb = new StringBuilder();
            var normTuplesGroupings =
                NormByTuple.ToLookup(pair => pair.Value, pair => pair.Key)
                    .OrderBy(tuples => tuples.Key);
            foreach (var grouping in normTuplesGroupings)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append($"{grouping.Key} x {grouping.Count()}");
            }

            return sb;
        }
    }

    private ImmutableDictionary<(T, T), TNorm> GetNormByTuple()
    {
        var dict = new Dictionary<(T, T), TNorm>();
        foreach (var tuple in Tuples)
        {
            dict.Add(
                tuple, 
                T.GetNorm(tuple.Item1, tuple.Item2));
        }
        return dict.ToImmutableDictionary();
    }
}