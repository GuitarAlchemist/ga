namespace GA.Core.Collections.Abstractions;

/// <summary>
/// Interface for a class that declares a finite collection of <see cref="IRangeValueObject{TSelf}"/> elements
/// </summary>
/// <typeparam name="TSelf">The class type.</typeparam>
[PublicAPI]
public interface IStaticValueObjectList<TSelf> : IStaticReadonlyCollection<TSelf>,
                                                 IRangeValueObject<TSelf>
    where TSelf : struct, IRangeValueObject<TSelf>
{
    public static abstract IReadOnlyList<int> Values { get; }
}