namespace GA.Core.Collections;

public interface IItemCollection<out TSelf>
    where TSelf: notnull
{
    /// <summary>
    /// Gets the <see cref="IReadOnlyCollection{TSelf}"/>
    /// </summary>
    public static abstract IReadOnlyCollection<TSelf> Items { get; }
}