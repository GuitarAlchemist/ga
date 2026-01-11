namespace GA.Business.Core.Atonal.Abstractions;

using System;
using GA.Core.Abstractions;
using Primitives;

/// <summary>
///     Abstraction for a <typeparamref name="TSelf" /> to <typeparamref name="TSelf" /> <see cref="IntervalClass" /> norm,
///     defined at the class level
/// </summary>
/// <typeparam name="TSelf">The class type</typeparam>
/// Derives from
/// <see cref="IStaticPairNorm{TSelf,TNorm}" />
public interface IStaticPairIntervalClassNorm<in TSelf> : IStaticPairNorm<TSelf, IntervalClass>
    where TSelf : IValueObject
{
    /// <summary>
    ///     Computes the norm the two value objects
    /// </summary>
    /// <param name="obj1">The first <typeparamref name="TSelf" /> object</param>
    /// <param name="obj2">The second <typeparamref name="TSelf" /> object</param>
    /// <returns>The <see cref="IntervalClass" /></returns>
    public static IntervalClass GetNorm(TSelf obj1, TSelf obj2)
    {
        return IntervalClass.FromValue(Math.Abs(obj2.Value - obj1.Value));
    }
}
