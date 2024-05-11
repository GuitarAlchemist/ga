namespace GA.Core.Abstractions;

/// <summary>
/// Abstraction for a <typeparamref name="TSelf"/> to <typeparamref name="TSelf"/> <see cref="TNorm"/> norm, defined at the class level
/// </summary>
/// <typeparam name="TSelf">The class type</typeparam>
/// <typeparam name="TNorm">The norm type</typeparam>
[PublicAPI]
public interface IStaticPairNorm<in TSelf, out TNorm>
    where TNorm : struct
{
    /// <summary>
    /// Gets the norm between a pair of <see cref="TSelf"/> objects
    /// </summary>
    /// <param name="obj1">The first <typeparamref name="TSelf"/> object</param>
    /// <param name="obj2">The second <typeparamref name="TSelf"/> object</param>
    /// <returns>The <typeparamref name="TNorm"/></returns>
    static abstract TNorm GetPairNorm(TSelf obj1, TSelf obj2);
}