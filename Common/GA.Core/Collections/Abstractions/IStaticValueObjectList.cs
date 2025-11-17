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
///     Use the memoized utilities provided by <see cref="ValueObjectUtils{TSelf}"/> for consistency across all
///     implementers. In particular, expose:
///     </para>
///     <list type="bullet">
///         <item>
///             <description><see cref="Items"/> via <c>ValueObjectUtils&lt;TSelf&gt;.Items</c></description>
///         </item>
///         <item>
///             <description><see cref="Values"/> via <c>ValueObjectUtils&lt;TSelf&gt;.Values</c></description>
///         </item>
///     </list>
///     <para>
///     Optionally, implement the cached spans to avoid allocations in tight loops:
///     </para>
///     <code>
///     public static ReadOnlySpan&lt;TSelf&gt; ItemsSpan =&gt; ValueObjectUtils&lt;TSelf&gt;.ItemsSpan;
///     public static ReadOnlySpan&lt;int&gt;   ValuesSpan =&gt; ValueObjectUtils&lt;TSelf&gt;.ValuesSpan;
///     </code>
///     <para>
///     Memoization details: <see cref="ValueObjectCache{T}"/> materializes all instances from
///     <see cref="IRangeValueObject{TSelf}.Min"/> to <see cref="IRangeValueObject{TSelf}.Max"/> on the first access
///     and caches them for the lifetime of the application domain.
///     </para>
///     <para>
///     Minimal example:
///     <code>
///     public readonly record struct MyType : IStaticValueObjectList&lt;MyType&gt;
///     {
///         public static IReadOnlyCollection&lt;MyType&gt; Items =&gt; ValueObjectUtils&lt;MyType&gt;.Items;
///         public static IReadOnlyList&lt;int&gt; Values =&gt; ValueObjectUtils&lt;MyType&gt;.Values;
///         public static ReadOnlySpan&lt;MyType&gt; ItemsSpan =&gt; ValueObjectUtils&lt;MyType&gt;.ItemsSpan; // optional
///         public static ReadOnlySpan&lt;int&gt;   ValuesSpan =&gt; ValueObjectUtils&lt;MyType&gt;.ValuesSpan; // optional
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
