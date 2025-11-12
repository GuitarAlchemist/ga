namespace GA.Core.Collections.Abstractions;

/// <summary>
///     Interface for a class that declares a finite collection of <see cref="IRangeValueObject{TSelf}" /> elements
///     with automatic memoization of both Items and Values collections.
/// </summary>
/// <remarks>
///     <para>
///     Derives from <see cref="IStaticReadonlyCollection{TSelf}" /> and <see cref="IRangeValueObject{TSelf}" />.
///     </para>
///     <para>
///     This interface provides default implementations for both <see cref="Items"/> and <see cref="Values"/>
///     that are automatically memoized via <see cref="ValueObjectUtils{TSelf}"/>. Implementations should
///     use these default implementations by simply declaring the properties without a body.
///     </para>
///     <para>
///     The memoization is performed by <see cref="ValueObjectCache{T}"/> which creates all instances
///     from <see cref="IRangeValueObject{TSelf}.Min"/> to <see cref="IRangeValueObject{TSelf}.Max"/>
///     on first access and caches them for the lifetime of the application.
///     </para>
///     <para>
///     Example implementation:
///     <code>
///     public readonly record struct MyType : IStaticValueObjectList&lt;MyType&gt;
///     {
///         // Items and Values use default memoized implementations
///         public static IReadOnlyCollection&lt;MyType&gt; Items =&gt; ValueObjectUtils&lt;MyType&gt;.Items;
///         public static IReadOnlyList&lt;int&gt; Values =&gt; ValueObjectUtils&lt;MyType&gt;.Values;
///     }
///     </code>
///     </para>
/// </remarks>
/// <typeparam name="TSelf">The class type.</typeparam>
[PublicAPI]
public interface IStaticValueObjectList<TSelf> : IStaticReadonlyCollection<TSelf>,
    IRangeValueObject<TSelf>
    where TSelf : struct, IRangeValueObject<TSelf>
{
    /// <summary>
    /// Gets the memoized list of all values from Min.Value to Max.Value.
    /// </summary>
    /// <remarks>
    /// Implementations should use the default memoized implementation:
    /// <code>public static IReadOnlyList&lt;int&gt; Values =&gt; ValueObjectUtils&lt;TSelf&gt;.Values;</code>
    /// </remarks>
    public static abstract IReadOnlyList<int> Values { get; }
}
