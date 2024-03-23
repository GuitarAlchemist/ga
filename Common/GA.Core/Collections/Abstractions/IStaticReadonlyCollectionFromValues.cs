namespace GA.Core.Collections.Abstractions;

/// <summary>
/// Interface for classes that define a read-only collection at the class level (Items are retrieved from values)
/// </summary>
public interface IStaticReadonlyCollectionFromValues<out TSelf> : IStaticReadonlyCollection<TSelf>
    where TSelf : IRangeValueObject<TSelf>
{
    /// <summary>
    /// Gets the <see cref="IReadOnlyCollection{TSelf}"/>
    /// </summary>
    new static IReadOnlyCollection<TSelf> Items => ValueObjectUtils<TSelf>.Items;
}