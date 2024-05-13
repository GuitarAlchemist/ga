namespace GA.Core.Collections.Abstractions;

/// <summary>
/// Interface for classes that define a read-only collection at the class level (Items are retrieved from values)
/// </summary>
/// <remarks>
/// Derives from <see cref="IStaticReadonlyCollection{TSelf}"/> | <see cref="IRangeValueObject{TSelf}"/>
/// At an upper level IValueObject interface derives from <see cref="IComparable{TSelf}"/> | <see cref="IEquatable{TSelf}"/>
/// </remarks>
public interface IStaticReadonlyCollectionFromValues<TSelf> : IStaticReadonlyCollection<TSelf>, 
                                                              IRangeValueObject<TSelf>
    where TSelf : IRangeValueObject<TSelf>
{
    /// <summary>
    /// Gets the <see cref="IReadOnlyCollection{TSelf}"/>
    /// </summary>
    new static IReadOnlyCollection<TSelf> Items => ValueObjectUtils<TSelf>.Items;
}