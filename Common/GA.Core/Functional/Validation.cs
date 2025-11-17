namespace GA.Core.Functional;

/// <summary>
///     Represents a validation result that can accumulate multiple errors.
///     Unlike Result which fails fast, Validation collects all errors.
/// </summary>
/// <typeparam name="TValue">The type of the success value</typeparam>
/// <remarks>
///     This type is useful when you want to validate multiple fields and collect all errors
///     before returning to the user, rather than failing on the first error.
///     Example usage:
///     <code>
/// var nameValidation = ValidateName(name);
/// var ageValidation = ValidateAge(age);
/// var emailValidation = ValidateEmail(email);
/// 
/// var result = Validation&lt;User&gt;.Combine(
///     nameValidation,
///     ageValidation,
///     emailValidation,
///     (n, a, e) => new User(n, a, e));
/// 
/// result.Match(
///     onValid: user => SaveUser(user),
///     onInvalid: errors => DisplayErrors(errors)
/// );
/// </code>
/// </remarks>
[PublicAPI]
public readonly record struct Validation<TValue>
{
    private readonly ImmutableList<string> _errors;
    private readonly TValue? _value;

    private Validation(TValue value)
    {
        _value = value;
        _errors = [];
    }

    private Validation(ImmutableList<string> errors)
    {
        _value = default;
        _errors = errors;
    }

    /// <summary>
    ///     Gets whether this validation is valid (no errors).
    /// </summary>
    public bool IsValid => _errors.IsEmpty;

    /// <summary>
    ///     Gets whether this validation is invalid (has errors).
    /// </summary>
    public bool IsInvalid => !IsValid;

    /// <summary>
    ///     Gets the list of validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    ///     Creates a valid validation with the given value.
    /// </summary>
    public static Validation<TValue> Valid(TValue value)
    {
        return new Validation<TValue>(value);
    }

    /// <summary>
    ///     Creates an invalid validation with a single error.
    /// </summary>
    public static Validation<TValue> Invalid(string error)
    {
        return new Validation<TValue>(ImmutableList.Create(error));
    }

    /// <summary>
    ///     Creates an invalid validation with multiple errors.
    /// </summary>
    public static Validation<TValue> Invalid(params string[] errors)
    {
        return new Validation<TValue>([.. errors]);
    }

    /// <summary>
    ///     Creates an invalid validation with multiple errors.
    /// </summary>
    public static Validation<TValue> Invalid(IEnumerable<string> errors)
    {
        return new Validation<TValue>([.. errors]);
    }

    /// <summary>
    ///     Functor: Maps the success value to a new value using the provided function.
    ///     If this validation is invalid, the errors are propagated unchanged.
    /// </summary>
    public Validation<TResult> Map<TResult>(Func<TValue, TResult> mapper)
    {
        return IsValid
            ? Validation<TResult>.Valid(mapper(_value!))
            : Validation<TResult>.Invalid(_errors);
    }

    /// <summary>
    ///     Applicative: Applies a validation of a function to this validation.
    ///     Accumulates errors from both validations if both are invalid.
    /// </summary>
    public Validation<TResult> Apply<TResult>(Validation<Func<TValue, TResult>> validationFunc)
    {
        if (IsValid && validationFunc.IsValid)
        {
            return Validation<TResult>.Valid(validationFunc._value!(_value!));
        }

        var allErrors = _errors.AddRange(validationFunc._errors);
        return Validation<TResult>.Invalid(allErrors);
    }

    /// <summary>
    ///     Pattern matching: Executes one of two functions depending on whether this is valid or invalid.
    /// </summary>
    public TResult Match<TResult>(Func<TValue, TResult> onValid, Func<IReadOnlyList<string>, TResult> onInvalid)
    {
        return IsValid ? onValid(_value!) : onInvalid(_errors);
    }

    /// <summary>
    ///     Pattern matching (void): Executes one of two actions depending on whether this is valid or invalid.
    /// </summary>
    public void Match(Action<TValue> onValid, Action<IReadOnlyList<string>> onInvalid)
    {
        if (IsValid)
        {
            onValid(_value!);
        }
        else
        {
            onInvalid(_errors);
        }
    }

    /// <summary>
    ///     Gets the success value or returns the provided default value if this is invalid.
    /// </summary>
    public TValue GetValueOrDefault(TValue defaultValue = default!)
    {
        return IsValid ? _value! : defaultValue;
    }

    /// <summary>
    ///     Gets the success value or throws an exception if this is invalid.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the validation is invalid.</exception>
    public TValue GetValueOrThrow()
    {
        return IsValid
            ? _value!
            : throw new InvalidOperationException($"Validation failed with errors: {string.Join(", ", _errors)}");
    }

    /// <summary>
    ///     Executes the provided action if this is valid, and returns this validation unchanged.
    ///     Useful for side effects in a chain.
    /// </summary>
    public Validation<TValue> Tap(Action<TValue> action)
    {
        if (IsValid)
        {
            action(_value!);
        }

        return this;
    }

    /// <summary>
    ///     Executes the provided action if this is invalid, and returns this validation unchanged.
    ///     Useful for logging errors in a chain.
    /// </summary>
    public Validation<TValue> TapError(Action<IReadOnlyList<string>> action)
    {
        if (IsInvalid)
        {
            action(_errors);
        }

        return this;
    }

    /// <summary>
    ///     Converts this validation to a Result.
    ///     If invalid, combines all errors into a single error message.
    /// </summary>
    public Result<TValue, string> ToResult()
    {
        return IsValid
            ? Result<TValue, string>.Success(_value!)
            : Result<TValue, string>.Failure(string.Join("; ", _errors));
    }

    /// <summary>
    ///     Converts this validation to a Result with a custom error combiner.
    /// </summary>
    public Result<TValue, TError> ToResult<TError>(Func<IReadOnlyList<string>, TError> errorCombiner)
    {
        return IsValid
            ? Result<TValue, TError>.Success(_value!)
            : Result<TValue, TError>.Failure(errorCombiner(_errors));
    }

    /// <summary>
    ///     Implicit conversion from TValue to Validation (valid).
    /// </summary>
    public static implicit operator Validation<TValue>(TValue value)
    {
        return Valid(value);
    }

    public override string ToString()
    {
        return IsValid ? $"Valid({_value})" : $"Invalid({_errors.Count} errors: {string.Join(", ", _errors)})";
    }
}

/// <summary>
///     Extension methods for Validation monad.
/// </summary>
[PublicAPI]
public static class ValidationExtensions
{
    /// <summary>
    ///     Combines two validations using the provided combiner function.
    ///     Accumulates errors from both validations if either is invalid.
    /// </summary>
    public static Validation<TResult> Combine<T1, T2, TResult>(
        this Validation<T1> validation1,
        Validation<T2> validation2,
        Func<T1, T2, TResult> combiner)
    {
        if (validation1.IsValid && validation2.IsValid)
        {
            return Validation<TResult>.Valid(combiner(
                validation1.GetValueOrThrow(),
                validation2.GetValueOrThrow()));
        }

        var errors = ImmutableList.CreateBuilder<string>();
        if (validation1.IsInvalid)
        {
            errors.AddRange(validation1.Errors);
        }

        if (validation2.IsInvalid)
        {
            errors.AddRange(validation2.Errors);
        }

        return Validation<TResult>.Invalid(errors.ToImmutable());
    }

    /// <summary>
    ///     Combines three validations using the provided combiner function.
    ///     Accumulates errors from all validations if any are invalid.
    /// </summary>
    public static Validation<TResult> Combine<T1, T2, T3, TResult>(
        Validation<T1> validation1,
        Validation<T2> validation2,
        Validation<T3> validation3,
        Func<T1, T2, T3, TResult> combiner)
    {
        if (validation1.IsValid && validation2.IsValid && validation3.IsValid)
        {
            return Validation<TResult>.Valid(combiner(
                validation1.GetValueOrThrow(),
                validation2.GetValueOrThrow(),
                validation3.GetValueOrThrow()));
        }

        var errors = ImmutableList.CreateBuilder<string>();
        if (validation1.IsInvalid)
        {
            errors.AddRange(validation1.Errors);
        }

        if (validation2.IsInvalid)
        {
            errors.AddRange(validation2.Errors);
        }

        if (validation3.IsInvalid)
        {
            errors.AddRange(validation3.Errors);
        }

        return Validation<TResult>.Invalid(errors.ToImmutable());
    }

    /// <summary>
    ///     Sequences a collection of validations into a validation of a collection.
    ///     Accumulates all errors from all invalid validations.
    /// </summary>
    public static Validation<ImmutableList<TValue>> Sequence<TValue>(
        this IEnumerable<Validation<TValue>> validations)
    {
        var values = ImmutableList.CreateBuilder<TValue>();
        var errors = ImmutableList.CreateBuilder<string>();

        foreach (var validation in validations)
        {
            if (validation.IsValid)
            {
                values.Add(validation.GetValueOrThrow());
            }
            else
            {
                errors.AddRange(validation.Errors);
            }
        }

        return errors.Count > 0
            ? Validation<ImmutableList<TValue>>.Invalid(errors.ToImmutable())
            : Validation<ImmutableList<TValue>>.Valid(values.ToImmutable());
    }

    /// <summary>
    ///     Traverses a collection, applying a function that returns a Validation to each element,
    ///     and sequences the results. Accumulates all errors.
    /// </summary>
    public static Validation<ImmutableList<TResult>> Traverse<TValue, TResult>(
        this IEnumerable<TValue> values,
        Func<TValue, Validation<TResult>> func)
    {
        return values.Select(func).Sequence();
    }

    /// <summary>
    ///     Converts a Result to a Validation.
    /// </summary>
    public static Validation<TValue> ToValidation<TValue>(this Result<TValue, string> result)
    {
        return result.Match(
            Validation<TValue>.Valid,
            Validation<TValue>.Invalid);
    }

    /// <summary>
    ///     Converts a Result with any error type to a Validation.
    /// </summary>
    public static Validation<TValue> ToValidation<TValue, TError>(
        this Result<TValue, TError> result,
        Func<TError, string> errorMapper)
    {
        return result.Match(
            Validation<TValue>.Valid,
            error => Validation<TValue>.Invalid(errorMapper(error)));
    }
}
