namespace GA.Core;

/// <summary>
/// Interface for classes defining a <typeparamref name="TSelf"/> to <typeparamref name="TSelf"/> <see cref="TNorm"/> norm.
/// </summary>
/// <typeparam name="TSelf">The class type.</typeparam>
/// <typeparam name="TNorm">The norm type.</typeparam>
[PublicAPI]
public interface IStaticNorm<in TSelf, out TNorm>
    where TNorm : struct
{
    /// <summary>
    /// Gets the norm.
    /// </summary>
    /// <param name="item1">The first <see cref="TSelf"/> item.</param>
    /// <param name="item2">The second <see cref="TSelf"/> item.</param>
    /// <returns>The <see cref="TNorm"/></returns>
    static abstract TNorm GetNorm(TSelf item1, TSelf item2);
}