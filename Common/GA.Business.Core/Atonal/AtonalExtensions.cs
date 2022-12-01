namespace GA.Business.Core.Atonal;

using GA.Core.Extensions;
using Primitives;

[PublicAPI]
public static class AtonalExtensions
{
    /// <summary>
    /// Gets the interval class vector.
    /// </summary>
    /// <typeparam name="T">The items type (Must implement <see cref="IIntervalClassType{T}"/>).</typeparam>
    /// <param name="items"></param>
    /// <returns></returns>
    public static IntervalClassVector ToIntervalClassVector<T>(this IEnumerable<T> items) 
        where T : IIntervalClassType<T>
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        return new(items.ToNormedCartesianProduct<T, IntervalClass>().ByNormCounts(pair => pair.Norm.Value > 0));
    }
}
