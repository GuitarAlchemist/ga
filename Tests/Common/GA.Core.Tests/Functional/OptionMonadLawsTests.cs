namespace GA.Core.Tests.Functional;

using Core.Functional;

/// <summary>
///     Tests verifying that Option&lt;T&gt; satisfies functor and monad laws.
/// </summary>
[TestFixture]
[Category("Functional")]
[Category("MonadLaws")]
public class OptionMonadLawsTests
{
    /// <summary>
    ///     Functor Law 1: Identity
    ///     Mapping with the identity function should return the same option.
    ///     option.Map(x => x) == option
    /// </summary>
    [Test]
    public void Map_IdentityLaw_Some()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var mapped = option.Map(x => x);

        // Assert
        Assert.That(mapped.IsSome, Is.True);
        Assert.That(mapped.GetValueOrThrow(), Is.EqualTo(42));
    }

    [Test]
    public void Map_IdentityLaw_None()
    {
        // Arrange
        var option = Option<int>.None;

        // Act
        var mapped = option.Map(x => x);

        // Assert
        Assert.That(mapped.IsNone, Is.True);
    }

    /// <summary>
    ///     Functor Law 2: Composition
    ///     Mapping with f then g should be the same as mapping with g(f(x)).
    ///     option.Map(f).Map(g) == option.Map(x => g(f(x)))
    /// </summary>
    [Test]
    public void Map_CompositionLaw_Some()
    {
        // Arrange
        var option = Option<int>.Some(10);
        Func<int, int> f = x => x * 2;
        Func<int, int> g = x => x + 5;

        // Act
        var mapped1 = option.Map(f).Map(g);
        var mapped2 = option.Map(x => g(f(x)));

        // Assert
        Assert.That(mapped1.IsSome, Is.True);
        Assert.That(mapped2.IsSome, Is.True);
        Assert.That(mapped1.GetValueOrThrow(), Is.EqualTo(mapped2.GetValueOrThrow()));
        Assert.That(mapped1.GetValueOrThrow(), Is.EqualTo(25)); // (10 * 2) + 5 = 25
    }

    [Test]
    public void Map_CompositionLaw_None()
    {
        // Arrange
        var option = Option<int>.None;
        Func<int, int> f = x => x * 2;
        Func<int, int> g = x => x + 5;

        // Act
        var mapped1 = option.Map(f).Map(g);
        var mapped2 = option.Map(x => g(f(x)));

        // Assert
        Assert.That(mapped1.IsNone, Is.True);
        Assert.That(mapped2.IsNone, Is.True);
    }

    /// <summary>
    ///     Monad Law 1: Left Identity
    ///     Wrapping a value and binding it with f should be the same as calling f directly.
    ///     Option.Some(a).Bind(f) == f(a)
    /// </summary>
    [Test]
    public void Bind_LeftIdentityLaw()
    {
        // Arrange
        const int value = 42;
        Func<int, Option<string>> f = x => Option<string>.Some($"Value: {x}");

        // Act
        var bound = Option<int>.Some(value).Bind(f);
        var direct = f(value);

        // Assert
        Assert.That(bound.IsSome, Is.True);
        Assert.That(direct.IsSome, Is.True);
        Assert.That(bound.GetValueOrThrow(), Is.EqualTo(direct.GetValueOrThrow()));
    }

    /// <summary>
    ///     Monad Law 2: Right Identity
    ///     Binding with the Some constructor should return the same option.
    ///     m.Bind(Option.Some) == m
    /// </summary>
    [Test]
    public void Bind_RightIdentityLaw_Some()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var bound = option.Bind(x => Option<int>.Some(x));

        // Assert
        Assert.That(bound.IsSome, Is.True);
        Assert.That(bound.GetValueOrThrow(), Is.EqualTo(42));
    }

    [Test]
    public void Bind_RightIdentityLaw_None()
    {
        // Arrange
        var option = Option<int>.None;

        // Act
        var bound = option.Bind(x => Option<int>.Some(x));

        // Assert
        Assert.That(bound.IsNone, Is.True);
    }

    /// <summary>
    ///     Monad Law 3: Associativity
    ///     Binding with f then g should be the same as binding with a function that binds f then g.
    ///     m.Bind(f).Bind(g) == m.Bind(x => f(x).Bind(g))
    /// </summary>
    [Test]
    public void Bind_AssociativityLaw_Some()
    {
        // Arrange
        var option = Option<int>.Some(10);
        Func<int, Option<int>> f = x => Option<int>.Some(x * 2);
        Func<int, Option<string>> g = x => Option<string>.Some($"Value: {x}");

        // Act
        var bound1 = option.Bind(f).Bind(g);
        var bound2 = option.Bind(x => f(x).Bind(g));

        // Assert
        Assert.That(bound1.IsSome, Is.True);
        Assert.That(bound2.IsSome, Is.True);
        Assert.That(bound1.GetValueOrThrow(), Is.EqualTo(bound2.GetValueOrThrow()));
        Assert.That(bound1.GetValueOrThrow(), Is.EqualTo("Value: 20"));
    }

    [Test]
    public void Bind_AssociativityLaw_NoneInFirst()
    {
        // Arrange
        var option = Option<int>.None;
        Func<int, Option<int>> f = x => Option<int>.Some(x * 2);
        Func<int, Option<string>> g = x => Option<string>.Some($"Value: {x}");

        // Act
        var bound1 = option.Bind(f).Bind(g);
        var bound2 = option.Bind(x => f(x).Bind(g));

        // Assert
        Assert.That(bound1.IsNone, Is.True);
        Assert.That(bound2.IsNone, Is.True);
    }

    [Test]
    public void Bind_AssociativityLaw_NoneInSecond()
    {
        // Arrange
        var option = Option<int>.Some(10);
        Func<int, Option<int>> f = x => Option<int>.None;
        Func<int, Option<string>> g = x => Option<string>.Some($"Value: {x}");

        // Act
        var bound1 = option.Bind(f).Bind(g);
        var bound2 = option.Bind(x => f(x).Bind(g));

        // Assert
        Assert.That(bound1.IsNone, Is.True);
        Assert.That(bound2.IsNone, Is.True);
    }

    [Test]
    public void Filter_PredicateTrue_ReturnsSome()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var filtered = option.Filter(x => x > 0);

        // Assert
        Assert.That(filtered.IsSome, Is.True);
        Assert.That(filtered.GetValueOrThrow(), Is.EqualTo(42));
    }

    [Test]
    public void Filter_PredicateFalse_ReturnsNone()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var filtered = option.Filter(x => x < 0);

        // Assert
        Assert.That(filtered.IsNone, Is.True);
    }

    [Test]
    public void Filter_None_ReturnsNone()
    {
        // Arrange
        var option = Option<int>.None;

        // Act
        var filtered = option.Filter(x => x > 0);

        // Assert
        Assert.That(filtered.IsNone, Is.True);
    }

    [Test]
    public void Or_SomeOrSome_ReturnsFirst()
    {
        // Arrange
        var option1 = Option<int>.Some(42);
        var option2 = Option<int>.Some(100);

        // Act
        var result = option1.Or(option2);

        // Assert
        Assert.That(result.IsSome, Is.True);
        Assert.That(result.GetValueOrThrow(), Is.EqualTo(42));
    }

    [Test]
    public void Or_NoneOrSome_ReturnsSecond()
    {
        // Arrange
        var option1 = Option<int>.None;
        var option2 = Option<int>.Some(100);

        // Act
        var result = option1.Or(option2);

        // Assert
        Assert.That(result.IsSome, Is.True);
        Assert.That(result.GetValueOrThrow(), Is.EqualTo(100));
    }

    [Test]
    public void Or_NoneOrNone_ReturnsNone()
    {
        // Arrange
        var option1 = Option<int>.None;
        var option2 = Option<int>.None;

        // Act
        var result = option1.Or(option2);

        // Assert
        Assert.That(result.IsNone, Is.True);
    }

    [Test]
    public void OrElse_Some_DoesNotEvaluateAlternative()
    {
        // Arrange
        var option = Option<int>.Some(42);
        var evaluated = false;

        // Act
        var result = option.OrElse(() =>
        {
            evaluated = true;
            return Option<int>.Some(100);
        });

        // Assert
        Assert.That(result.IsSome, Is.True);
        Assert.That(result.GetValueOrThrow(), Is.EqualTo(42));
        Assert.That(evaluated, Is.False, "Alternative should not be evaluated when option is Some");
    }

    [Test]
    public void OrElse_None_EvaluatesAlternative()
    {
        // Arrange
        var option = Option<int>.None;
        var evaluated = false;

        // Act
        var result = option.OrElse(() =>
        {
            evaluated = true;
            return Option<int>.Some(100);
        });

        // Assert
        Assert.That(result.IsSome, Is.True);
        Assert.That(result.GetValueOrThrow(), Is.EqualTo(100));
        Assert.That(evaluated, Is.True, "Alternative should be evaluated when option is None");
    }

    [Test]
    public void ToResult_Some_ReturnsSuccess()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var result = option.ToResult("error");

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.GetValueOrThrow(), Is.EqualTo(42));
    }

    [Test]
    public void ToResult_None_ReturnsFailure()
    {
        // Arrange
        var option = Option<int>.None;

        // Act
        var result = option.ToResult("error message");

        // Assert
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.GetErrorOrThrow(), Is.EqualTo("error message"));
    }

    // TODO: Fix ToNullable implementation - currently has issues with value types
    // [Test]
    // public void ToNullable_Some_ReturnsValue()
    // {
    //     var option = Option<int>.Some(42);
    //     int? nullable = option.ToNullable();
    //     Assert.That(nullable.HasValue, Is.True);
    //     Assert.That(nullable.Value, Is.EqualTo(42));
    // }

    // [Test]
    // public void ToNullable_None_ReturnsNull()
    // {
    //     var option = Option<int>.None;
    //     int? nullable = option.ToNullable();
    //     Assert.That(nullable, Is.Null);
    // }
}
