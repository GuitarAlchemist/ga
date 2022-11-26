namespace GA.Core;

/// <summary>
/// A 2-tuple where the 2 elements have the same type.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public interface IPair<out T>
{
    /// <summary>
    /// Gets the first <see cref="T"/>.
    /// </summary>
    T Item1 { get; }

    /// <summary>
    /// Gets the second <see cref="T"/>.
    /// </summary>
    T Item2 { get; }
}