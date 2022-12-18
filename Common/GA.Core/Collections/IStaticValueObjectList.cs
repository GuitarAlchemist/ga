namespace GA.Core.Collections;

/// <summary>
/// Interface for a class that that declare a finite collection of <see cref="IRangeValueObject{TSelf}"/> elements.
/// </summary>
/// <typeparam name="TSelf">The class type.</typeparam>
[PublicAPI]
public interface IStaticValueObjectList<TSelf> : IStaticReadonlyCollection<TSelf>, 
                                                 IRangeValueObject<TSelf>
    where TSelf : struct, IRangeValueObject<TSelf>
{
    public static abstract IReadOnlyList<int> Values { get; }
}