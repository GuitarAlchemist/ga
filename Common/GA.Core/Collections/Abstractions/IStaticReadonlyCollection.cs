namespace GA.Core.Collections.Abstractions;

/// <summary>
///     Interface for classes that define a read-only collection at the class level
/// </summary>
public interface IStaticReadonlyCollection<out TSelf>
    where TSelf : notnull
{
    /// <summary>
    ///     Gets the <see cref="IReadOnlyCollection{TSelf}" />
    /// </summary>
    public static abstract IReadOnlyCollection<TSelf> Items { get; }
}
