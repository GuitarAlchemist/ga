namespace GA.Core;

/// <summary>
/// Interface for classes defining a <typeparamref name="TSelf"/> to <typeparamref name="TSelf"/> <see cref="TNorm"/> norm
/// </summary>
/// <typeparam name="TSelf">The class type</typeparam>
/// <typeparam name="TNorm">The norm type</typeparam>
[PublicAPI]
public interface IStaticNorm<in TSelf, out TNorm>
    where TNorm : struct
{
    /// <summary>
    /// Gets the norm between two <see cref="TSelf"/> items
    /// </summary>
    /// <param name="item1">The first <typeparamref name="TSelf"/> </param>
    /// <param name="item2">The second <typeparamref name="TSelf"/> </param>
    /// <returns>The <typeparamref name="TNorm"/></returns>
    static abstract TNorm GetNorm(TSelf item1, TSelf item2);
}