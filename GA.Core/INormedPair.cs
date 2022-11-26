namespace GA.Core;

/// <summary>
/// A 2-tuple where the element type can calculate a norm.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <typeparam name="TNorm">The norm type.</typeparam>
public interface INormedPair<out T, TNorm> : IPair<T>
    where T: INormed<T, TNorm> 
    where TNorm : struct
{
}

/// <summary>
/// A 2-tuple where the element type can calculate an integer norm.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <inheritdoc cref="INormedPair{T,TNorm}"/>
public interface INormedPair<out T> : INormedPair<T, int>
    where T : INormed<T>
{
}