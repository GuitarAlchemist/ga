namespace GA.Core;

/// <summary>
/// Interface for a type that define a norm (Absolute distance between 2 items) between two object instances 
/// </summary>
/// <typeparam name="TSelf">The type that defines a norm</typeparam>
/// <typeparam name="TNorm">The norm type</typeparam>
[PublicAPI]
public interface INormedType<in TSelf, out TNorm>
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