namespace GA.Core.Collections.Abstractions;

/// <summary>
/// Interface for classes that define a collection at the type level.
/// </summary>
/// <typeparam name="TSelf">The class type.</typeparam>
public interface IStaticEnumerable<out TSelf>
    where TSelf : notnull
{
    /// <summary>
    /// Gets the <see cref="IReadOnlyCollection{TSelf}"/>
    /// </summary>
    public static abstract IEnumerable<TSelf> Items { get; }
}