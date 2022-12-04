namespace GA.Business.Core.Atonal;

using GA.Core;
using Primitives;

/// <summary>
/// Interface for classes defining a <typeparamref name="TSelf"/> to <typeparamref name="TSelf"/> <see cref="IntervalClass"/> norm.
/// </summary>
/// <typeparam name="TSelf"></typeparam>
public interface IStaticIntervalClassNorm<in TSelf> : IStaticNorm<TSelf, IntervalClass>
{
}