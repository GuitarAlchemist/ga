namespace GA.Core.Collections.Abstractions;

/// <summary>
///     Interface for classes that define a read-only collection at the class level
/// </summary>
public interface IStaticPrintableReadonlyCollection<TSelf>
    where TSelf : notnull
{
    /// <summary>
    ///     Gets the <see cref="PrintableReadOnlyCollection{TSelf}" />
    /// </summary>
    public static abstract PrintableReadOnlyCollection<TSelf> Items { get; }
}
