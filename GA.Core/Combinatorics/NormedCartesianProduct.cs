namespace GA.Core.Combinatorics;

using Collections;

/// <summary>
/// O(T x T) variations where (T,T) has a norm
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
/// <typeparam name="TNorm">The norm type.</typeparam>
[PublicAPI]
public class NormedCartesianProduct<T, TNorm> : CartesianProduct<T, NormedPair<T, TNorm>>
    where T : IItemCollection<T>, INormed<T, TNorm>
    where TNorm : struct
{
    public NormedCartesianProduct(Func<T, bool>? predicate = null) 
        : base((item1, item2) => new(item1, item2), predicate)
    {
    }

    public override string ToString()
    {
        return $"{base.ToString()}; Norms: {GetNormsDescription()}";

        StringBuilder GetNormsDescription()
        {
            var sb = new StringBuilder();
            var groupings = Pairs.ToLookup(pair => pair.Norm).OrderBy(grouping => grouping.Key);
            foreach (var grouping in groupings)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append($"{grouping.Key} x {grouping.Count()}");
            }
            return sb;
        }
    }
}