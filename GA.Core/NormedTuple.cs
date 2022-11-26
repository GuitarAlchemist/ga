namespace GA.Core;

/// <summary>
/// A pair or items with an integer norm.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
[PublicAPI]
public readonly struct NormedPair<T> : INormedPair<T>
    where T : INormed<T>
{
    /// <summary>
    /// Creates a normed pair from a pair.
    /// </summary>
    /// <param name="pair">The <see cref="IPair{T}"/>.</param>
    /// <returns>The <see cref="NormedPair{T,TNorm}"/>.</returns>
    public static NormedPair<T> FromPair(IPair<T> pair) => new(pair);
    
    /// <summary>
    /// Creates a norm pair from a tuple.
    /// </summary>
    /// <param name="tuple">The <see cref="Tuple{T, T}"/>.</param>
    /// <returns>The <see cref="NormedPair{T}"/>.</returns>
    public static NormedPair<T> FromTuple(Tuple<T, T> tuple) => new(tuple.Item1, tuple.Item2);

    /// <summary>
    /// Creates a norm pair from a tuple.
    /// </summary>
    /// <param name="tuple">The <see cref="Tuple{T, T}"/>.</param>
    /// <returns>The <see cref="NormedPair{T}"/>.</returns>
    public static NormedPair<T> FromTuple((T, T) tuple) => new(tuple.Item1, tuple.Item2);

    public NormedPair(T item1, T item2)
    {
        Item1 = item1;
        Item2 = item2;
        Norm = T.GetNorm(item1, item2);
    }

    public NormedPair(IPair<T> pair) 
        : this(pair.Item1, pair.Item2)
    {
    }

    /// <summary>
    /// Get the first <see cref="T"/>
    /// </summary>
    public T Item1 { get; }

    /// <summary>
    /// Get the second <see cref="T"/>
    /// </summary>
    public T Item2 { get; }

    /// <summary>
    /// The <see cref="int"/> norm.
    /// </summary>
    public int Norm { get; }

    public override string ToString() => $"({Item1}, {Item2}) => {Norm}";
}

/// <summary>
/// A pair of items with a norm.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <typeparam name="TNorm">The norm type.</typeparam>
[PublicAPI]
public readonly struct NormedPair<T, TNorm> : INormedPair<T, TNorm>
    where T : INormed<T, TNorm>
    where TNorm : struct
{
    /// <summary>
    /// Creates a normed pair from a pair.
    /// </summary>
    /// <param name="pair">The <see cref="IPair{T}"/>.</param>
    /// <returns>The <see cref="NormedPair{T,TNorm}"/>.</returns>
    public static NormedPair<T, TNorm> FromPair(IPair<T> pair) => new(pair);

    /// <summary>
    /// Creates a norm pair from a tuple.
    /// </summary>
    /// <param name="tuple">The <see cref="Tuple{T, T}"/>.</param>
    /// <returns>The <see cref="NormedPair{T}"/>.</returns>
    public static NormedPair<T, TNorm> FromTuple(Tuple<T, T> tuple) => new(tuple.Item1, tuple.Item2);

    /// <summary>
    /// Creates a norm pair from a tuple.
    /// </summary>
    /// <param name="tuple">The <see cref="Tuple{T, T}"/>.</param>
    /// <returns>The <see cref="NormedPair{T}"/>.</returns>
    public static NormedPair<T, TNorm> FromTuple((T, T) tuple) => new(tuple.Item1, tuple.Item2);

    public NormedPair(T item1, T item2)
    {
        Item1 = item1;
        Item2 = item2;
        Norm = T.GetNorm(item1, item2);
    }

    public NormedPair(IPair<T> pair) 
        : this(pair.Item1, pair.Item2)
    {
    }

    /// <summary>
    /// Get the first <see cref="T"/>
    /// </summary>
    public T Item1 { get; }

    /// <summary>
    /// Get the second <see cref="T"/>
    /// </summary>
    public T Item2 { get; }

    /// <summary>
    /// Gets the <see cref="TNorm"/> norm.
    /// </summary>
    public TNorm Norm { get; }

    public override string ToString() => $"({Item1}, {Item2}) => {Norm}";
}