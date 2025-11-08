namespace GA.Core.Tests.Functional;

using Core.Functional;

/// <summary>
///     Tests verifying that Result&lt;T, E&gt; satisfies functor and monad laws.
/// </summary>
[TestFixture]
[Category("Functional")]
[Category("MonadLaws")]
public class ResultMonadLawsTests
{
    /// <summary>
    ///     Functor Law 1: Identity
    ///     Mapping with the identity function should return the same result.
    ///     result.Map(x => x) == result
    /// </summary>
    [Test]
    public void Map_IdentityLaw_Success()
    {
        // Arrange
        var result = Result<int, string>.Success(42);

        // Act
        var mapped = result.Map(x => x);

        // Assert
        Assert.That(mapped.IsSuccess, Is.True);
        Assert.That(mapped.GetValueOrThrow(), Is.EqualTo(42));
    }

    [Test]
    public void Map_IdentityLaw_Failure()
    {
        // Arrange
        var result = Result<int, string>.Failure("error");

        // Act
        var mapped = result.Map(x => x);

        // Assert
        Assert.That(mapped.IsFailure, Is.True);
        Assert.That(mapped.GetErrorOrThrow(), Is.EqualTo("error"));
    }

    /// <summary>
    ///     Functor Law 2: Composition
    ///     Mapping with f then g should be the same as mapping with g(f(x)).
    ///     result.Map(f).Map(g) == result.Map(x => g(f(x)))
    /// </summary>
    [Test]
    public void Map_CompositionLaw_Success()
    {
        // Arrange
        var result = Result<int, string>.Success(10);
        Func<int, int> f = x => x * 2;
        Func<int, int> g = x => x + 5;

        // Act
        var mapped1 = result.Map(f).Map(g);
        var mapped2 = result.Map(x => g(f(x)));

        // Assert
        Assert.That(mapped1.IsSuccess, Is.True);
        Assert.That(mapped2.IsSuccess, Is.True);
        Assert.That(mapped1.GetValueOrThrow(), Is.EqualTo(mapped2.GetValueOrThrow()));
        Assert.That(mapped1.GetValueOrThrow(), Is.EqualTo(25)); // (10 * 2) + 5 = 25
    }

    [Test]
    public void Map_CompositionLaw_Failure()
    {
        // Arrange
        var result = Result<int, string>.Failure("error");
        Func<int, int> f = x => x * 2;
        Func<int, int> g = x => x + 5;

        // Act
        var mapped1 = result.Map(f).Map(g);
        var mapped2 = result.Map(x => g(f(x)));

        // Assert
        Assert.That(mapped1.IsFailure, Is.True);
        Assert.That(mapped2.IsFailure, Is.True);
        Assert.That(mapped1.GetErrorOrThrow(), Is.EqualTo(mapped2.GetErrorOrThrow()));
    }

    /// <summary>
    ///     Monad Law 1: Left Identity
    ///     Wrapping a value and binding it with f should be the same as calling f directly.
    ///     Result.Success(a).Bind(f) == f(a)
    /// </summary>
    [Test]
    public void Bind_LeftIdentityLaw()
    {
        // Arrange
        const int value = 42;
        Func<int, Result<string, string>> f = x => Result<string, string>.Success($"Value: {x}");

        // Act
        var bound = Result<int, string>.Success(value).Bind(f);
        var direct = f(value);

        // Assert
        Assert.That(bound.IsSuccess, Is.True);
        Assert.That(direct.IsSuccess, Is.True);
        Assert.That(bound.GetValueOrThrow(), Is.EqualTo(direct.GetValueOrThrow()));
    }

    /// <summary>
    ///     Monad Law 2: Right Identity
    ///     Binding with the Success constructor should return the same result.
    ///     m.Bind(Result.Success) == m
    /// </summary>
    [Test]
    public void Bind_RightIdentityLaw_Success()
    {
        // Arrange
        var result = Result<int, string>.Success(42);

        // Act
        var bound = result.Bind(x => Result<int, string>.Success(x));

        // Assert
        Assert.That(bound.IsSuccess, Is.True);
        Assert.That(bound.GetValueOrThrow(), Is.EqualTo(42));
    }

    [Test]
    public void Bind_RightIdentityLaw_Failure()
    {
        // Arrange
        var result = Result<int, string>.Failure("error");

        // Act
        var bound = result.Bind(x => Result<int, string>.Success(x));

        // Assert
        Assert.That(bound.IsFailure, Is.True);
        Assert.That(bound.GetErrorOrThrow(), Is.EqualTo("error"));
    }

    /// <summary>
    ///     Monad Law 3: Associativity
    ///     Binding with f then g should be the same as binding with a function that binds f then g.
    ///     m.Bind(f).Bind(g) == m.Bind(x => f(x).Bind(g))
    /// </summary>
    [Test]
    public void Bind_AssociativityLaw_Success()
    {
        // Arrange
        var result = Result<int, string>.Success(10);
        Func<int, Result<int, string>> f = x => Result<int, string>.Success(x * 2);
        Func<int, Result<string, string>> g = x => Result<string, string>.Success($"Value: {x}");

        // Act
        var bound1 = result.Bind(f).Bind(g);
        var bound2 = result.Bind(x => f(x).Bind(g));

        // Assert
        Assert.That(bound1.IsSuccess, Is.True);
        Assert.That(bound2.IsSuccess, Is.True);
        Assert.That(bound1.GetValueOrThrow(), Is.EqualTo(bound2.GetValueOrThrow()));
        Assert.That(bound1.GetValueOrThrow(), Is.EqualTo("Value: 20"));
    }

    [Test]
    public void Bind_AssociativityLaw_FailureInFirst()
    {
        // Arrange
        var result = Result<int, string>.Failure("initial error");
        Func<int, Result<int, string>> f = x => Result<int, string>.Success(x * 2);
        Func<int, Result<string, string>> g = x => Result<string, string>.Success($"Value: {x}");

        // Act
        var bound1 = result.Bind(f).Bind(g);
        var bound2 = result.Bind(x => f(x).Bind(g));

        // Assert
        Assert.That(bound1.IsFailure, Is.True);
        Assert.That(bound2.IsFailure, Is.True);
        Assert.That(bound1.GetErrorOrThrow(), Is.EqualTo(bound2.GetErrorOrThrow()));
    }

    [Test]
    public void Bind_AssociativityLaw_FailureInSecond()
    {
        // Arrange
        var result = Result<int, string>.Success(10);
        Func<int, Result<int, string>> f = x => Result<int, string>.Failure("error in f");
        Func<int, Result<string, string>> g = x => Result<string, string>.Success($"Value: {x}");

        // Act
        var bound1 = result.Bind(f).Bind(g);
        var bound2 = result.Bind(x => f(x).Bind(g));

        // Assert
        Assert.That(bound1.IsFailure, Is.True);
        Assert.That(bound2.IsFailure, Is.True);
        Assert.That(bound1.GetErrorOrThrow(), Is.EqualTo(bound2.GetErrorOrThrow()));
    }

    [Test]
    public void RailwayOrientedProgramming_AllSuccess()
    {
        // Arrange
        Func<int, Result<int, string>> validatePositive = x =>
            x > 0 ? Result<int, string>.Success(x) : Result<int, string>.Failure("Must be positive");

        Func<int, Result<int, string>> validateLessThan100 = x =>
            x < 100 ? Result<int, string>.Success(x) : Result<int, string>.Failure("Must be less than 100");

        Func<int, Result<string, string>> formatResult = x =>
            Result<string, string>.Success($"Valid value: {x}");

        // Act
        var result = Result<int, string>.Success(42)
            .Bind(validatePositive)
            .Bind(validateLessThan100)
            .Bind(formatResult);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.GetValueOrThrow(), Is.EqualTo("Valid value: 42"));
    }

    [Test]
    public void RailwayOrientedProgramming_FailsAtFirstValidation()
    {
        // Arrange
        Func<int, Result<int, string>> validatePositive = x =>
            x > 0 ? Result<int, string>.Success(x) : Result<int, string>.Failure("Must be positive");

        Func<int, Result<int, string>> validateLessThan100 = x =>
            x < 100 ? Result<int, string>.Success(x) : Result<int, string>.Failure("Must be less than 100");

        // Act
        var result = Result<int, string>.Success(-5)
            .Bind(validatePositive)
            .Bind(validateLessThan100);

        // Assert
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.GetErrorOrThrow(), Is.EqualTo("Must be positive"));
    }

    [Test]
    public void RailwayOrientedProgramming_FailsAtSecondValidation()
    {
        // Arrange
        Func<int, Result<int, string>> validatePositive = x =>
            x > 0 ? Result<int, string>.Success(x) : Result<int, string>.Failure("Must be positive");

        Func<int, Result<int, string>> validateLessThan100 = x =>
            x < 100 ? Result<int, string>.Success(x) : Result<int, string>.Failure("Must be less than 100");

        // Act
        var result = Result<int, string>.Success(150)
            .Bind(validatePositive)
            .Bind(validateLessThan100);

        // Assert
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.GetErrorOrThrow(), Is.EqualTo("Must be less than 100"));
    }
}
