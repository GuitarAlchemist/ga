namespace GA.Core.Collections;

/// <summary>
/// Interface for a class that that declare a finite collection of <see cref="IValueObject{TSelf}"/> elements.
/// </summary>
/// <typeparam name="TSelf">The class type.</typeparam>
[PublicAPI]
public interface IStaticValueObjectList<TSelf> : IStaticReadonlyCollection<TSelf>, 
                                                 IValueObject<TSelf>
    where TSelf : struct, IValueObject<TSelf>
{
    public static abstract IReadOnlyList<int> Values { get; }
}