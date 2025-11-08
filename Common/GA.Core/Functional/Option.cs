namespace GA.Core.Functional;

/// <summary>
///     Represents an optional value that can be Some(value) or None.
///     Implements the Option monad for handling nullable values functionally.
/// </summary>
/// <typeparam name="T">The type of the optional value</typeparam>
/// <remarks>
///     This type provides a type-safe alternative to null references,
///     making the presence or absence of a value explicit in the type system.
///     Example usage:
///     <code>
/// var option = Option&lt;int&gt;.Some(42);
/// var result = option
///     .Map(x => x * 2)
///     .Filter(x => x > 50)
///     .GetValueOrDefault(0);
/// </code>
/// </remarks>
[PublicAPI]
public readonly record struct Option<T>
{
    private readonly T? _value;

    private Option(T value)
    {
        _value = value;
        IsSome = true;
    }

    /// <summary>
    ///     Gets whether this option contains a value.
    /// </summary>
    public bool IsSome { get; }

    /// <summary>
    ///     Gets whether this option is empty.
    /// </summary>
    public bool IsNone => !IsSome;

    /// <summary>
    ///     Creates an empty Option.
    /// </summary>
    public static Option<T> None => default;

    /// <summary>
    ///     Creates an Option containing the given value.
    /// </summary>
    public static Option<T> Some(T value)
    {
        return new Option<T>(value);
    }

    /// <summary>
    ///     Creates an Option from a nullable value.
    ///     Returns Some if the value is not null, None otherwise.
    /// </summary>
    public static Option<T> FromNullable(T? value)
    {
        return value is not null ? Some(value) : None;
    }

    /// <summary>
    ///     Functor: Maps the contained value to a new value using the provided function.
    ///     If this option is None, returns None.
    /// </summary>
    public Option<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        return IsSome ? Option<TResult>.Some(mapper(_value!)) : Option<TResult>.None;
    }

    /// <summary>
    ///     Monad: Binds (FlatMaps) the contained value to a new option using the provided function.
    ///     If this option is None, returns None.
    /// </summary>
    public Option<TResult> Bind<TResult>(Func<T, Option<TResult>> binder)
    {
        return IsSome ? binder(_value!) : Option<TResult>.None;
    }

    /// <summary>
    ///     Pattern matching: Executes one of two functions depending on whether this is Some or None.
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone)
    {
        return IsSome ? onSome(_value!) : onNone();
    }

    /// <summary>
    ///     Pattern matching (void): Executes one of two actions depending on whether this is Some or None.
    /// </summary>
    public void Match(Action<T> onSome, Action onNone)
    {
        if (IsSome)
        {
            onSome(_value!);
        }
        else
        {
            onNone();
        }
    }

    /// <summary>
    ///     Gets the contained value or returns the provided default value if this is None.
    /// </summary>
    public T GetValueOrDefault(T defaultValue = default!)
    {
        return IsSome ? _value! : defaultValue;
    }

    /// <summary>
    ///     Gets the contained value or computes a default value using the provided function if this is None.
    /// </summary>
    public T GetValueOrElse(Func<T> defaultProvider)
    {
        return IsSome ? _value! : defaultProvider();
    }

    /// <summary>
    ///     Gets the contained value or throws an exception if this is None.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the option is None.</exception>
    public T GetValueOrThrow()
    {
        return IsSome
            ? _value!
            : throw new InvalidOperationException("Option is None");
    }

    /// <summary>
    ///     Filters the option based on a predicate.
    ///     Returns this option if it is Some and the predicate returns true, otherwise returns None.
    /// </summary>
    public Option<T> Filter(Func<T, bool> predicate)
    {
        return IsSome && predicate(_value!) ? this : None;
    }

    /// <summary>
    ///     Executes the provided action if this is Some, and returns this option unchanged.
    ///     Useful for side effects in a chain.
    /// </summary>
    public Option<T> Tap(Action<T> action)
    {
        if (IsSome)
        {
            action(_value!);
        }

        return this;
    }

    /// <summary>
    ///     Returns this option if it is Some, otherwise returns the alternative option.
    /// </summary>
    public Option<T> Or(Option<T> alternative)
    {
        return IsSome ? this : alternative;
    }

    /// <summary>
    ///     Returns this option if it is Some, otherwise computes an alternative option.
    /// </summary>
    public Option<T> OrElse(Func<Option<T>> alternativeProvider)
    {
        return IsSome ? this : alternativeProvider();
    }

    /// <summary>
    ///     Converts this option to a Result.
    ///     Returns Success if Some, Failure with the provided error if None.
    /// </summary>
    public Result<T, TError> ToResult<TError>(TError error)
    {
        return IsSome
            ? Result<T, TError>.Success(_value!)
            : Result<T, TError>.Failure(error);
    }

    /// <summary>
    ///     Converts this option to a Result.
    ///     Returns Success if Some, Failure with a computed error if None.
    /// </summary>
    public Result<T, TError> ToResult<TError>(Func<TError> errorProvider)
    {
        return IsSome
            ? Result<T, TError>.Success(_value!)
            : Result<T, TError>.Failure(errorProvider());
    }

    /// <summary>
    ///     Converts this option to a nullable value.
    ///     For value types, returns null if None, otherwise returns the value.
    ///     For reference types, returns null if None, otherwise returns the value.
    /// </summary>
    public T? ToNullable()
    {
        return IsSome ? _value : default;
    }

    /// <summary>
    ///     Implicit conversion from T to Option (Some).
    /// </summary>
    public static implicit operator Option<T>(T value)
    {
        return Some(value);
    }

    public override string ToString()
    {
        return IsSome ? $"Some({_value})" : "None";
    }
}

/// <summary>
///     Extension methods for Option monad.
/// </summary>
[PublicAPI]
public static class OptionExtensions
{
    /// <summary>
    ///     Flattens a nested Option into a single Option.
    /// </summary>
    public static Option<T> Flatten<T>(this Option<Option<T>> option)
    {
        return option.Bind(inner => inner);
    }

    /// <summary>
    ///     Combines two options using the provided combiner function.
    ///     Returns Some if both options are Some, otherwise returns None.
    /// </summary>
    public static Option<TResult> Combine<T1, T2, TResult>(
        this Option<T1> option1,
        Option<T2> option2,
        Func<T1, T2, TResult> combiner)
    {
        if (option1.IsNone || option2.IsNone)
        {
            return Option<TResult>.None;
        }

        return Option<TResult>.Some(combiner(
            option1.GetValueOrThrow(),
            option2.GetValueOrThrow()));
    }

    /// <summary>
    ///     Sequences a collection of options into an option of a collection.
    ///     Returns Some if all options are Some, otherwise returns None.
    /// </summary>
    public static Option<ImmutableList<T>> Sequence<T>(
        this IEnumerable<Option<T>> options)
    {
        var values = ImmutableList.CreateBuilder<T>();

        foreach (var option in options)
        {
            if (option.IsNone)
            {
                return Option<ImmutableList<T>>.None;
            }

            values.Add(option.GetValueOrThrow());
        }

        return Option<ImmutableList<T>>.Some(values.ToImmutable());
    }

    /// <summary>
    ///     Traverses a collection, applying a function that returns an Option to each element,
    ///     and sequences the results.
    /// </summary>
    public static Option<ImmutableList<TResult>> Traverse<TValue, TResult>(
        this IEnumerable<TValue> values,
        Func<TValue, Option<TResult>> func)
    {
        return values.Select(func).Sequence();
    }

    /// <summary>
    ///     Converts a nullable value to an Option.
    /// </summary>
    public static Option<T> ToOption<T>(this T? value) where T : struct
    {
        return value.HasValue ? Option<T>.Some(value.Value) : Option<T>.None;
    }

    /// <summary>
    ///     Converts a nullable reference to an Option.
    /// </summary>
    public static Option<T> ToOption<T>(this T? value) where T : class
    {
        return value is not null ? Option<T>.Some(value) : Option<T>.None;
    }

    /// <summary>
    ///     Filters a collection to only the Some values, unwrapping them.
    /// </summary>
    public static IEnumerable<T> WhereSome<T>(this IEnumerable<Option<T>> options)
    {
        return options.Where(o => o.IsSome).Select(o => o.GetValueOrThrow());
    }
}
