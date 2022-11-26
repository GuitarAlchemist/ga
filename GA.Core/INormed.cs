namespace GA.Core;

/// <summary>
/// Interface for classes that define a norm between two object instances 
/// </summary>
/// <typeparam name="TSelf">The normed object type</typeparam>
/// <typeparam name="TNorm">The norm type</typeparam>
[PublicAPI]
public interface INormed<in TSelf, out TNorm>
    where TNorm : struct
{
    /// <summary>
    /// Gets the norm (Absolute distance between 2 items).
    /// </summary>
    /// <param name="item1">The <see cref="TSelf"/> item 1.</param>
    /// <param name="item2">The <see cref="TSelf"/> item 1.</param>
    /// <returns>The <see cref="TNorm"/></returns>
    static abstract TNorm GetNorm(TSelf item1, TSelf item2);
}

/// <summary>
/// Interface for classes that define a norm between two object instances (Integer norm).
/// </summary>
/// <typeparam name="TSelf"></typeparam>
public interface INormed<in TSelf> : INormed<TSelf, int>
{
}