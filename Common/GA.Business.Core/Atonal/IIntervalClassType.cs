namespace GA.Business.Core.Atonal;

using GA.Core;
using Primitives;

/// <summary>
/// Defines a <see cref="IntervalClass"/> norm between two <typeparamref name="TSelf"/> instances
/// </summary>
/// <typeparam name="TSelf"></typeparam>
public interface IIntervalClassType<in TSelf> : INormedType<TSelf, IntervalClass>
{
}