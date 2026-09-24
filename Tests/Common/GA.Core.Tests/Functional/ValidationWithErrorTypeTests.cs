namespace GA.Core.Tests.Functional;

using Core.Functional;

/// <summary>
///     Tests verifying that Validation&lt;TValue, TError&gt;'s Map/Bind/Apply members
///     have the same error-accumulation semantics as Validation&lt;TValue&gt;.
/// </summary>
[TestFixture]
[Category("Functional")]
[Category("Validation")]
public class ValidationWithErrorTypeTests
{
    [Test]
    public void Map_Valid_TransformsValue()
    {
        // Arrange
        var validation = Validation<int, string>.Success(10);

        // Act
        var mapped = validation.Map(x => x * 2);

        // Assert
        Assert.That(mapped.IsValid, Is.True);
        Assert.That(mapped.Match(v => v, _ => -1), Is.EqualTo(20));
    }

    [Test]
    public void Map_Invalid_PreservesErrors()
    {
        // Arrange
        var validation = Validation<int, string>.Failure("error");

        // Act
        var mapped = validation.Map(x => x * 2);

        // Assert
        Assert.That(mapped.IsInvalid, Is.True);
        Assert.That(mapped.Errors, Has.Count.EqualTo(1));
        Assert.That(mapped.Errors[0], Is.EqualTo("error"));
    }

    [Test]
    public void Bind_Valid_ChainsToNextValidation()
    {
        // Arrange
        var validation = Validation<int, string>.Success(10);

        // Act
        var bound = validation.Bind(x => Validation<int, string>.Success(x * 2));

        // Assert
        Assert.That(bound.IsValid, Is.True);
        Assert.That(bound.Match(v => v, _ => -1), Is.EqualTo(20));
    }

    [Test]
    public void Bind_Invalid_DoesNotInvokeBinderAndPreservesErrors()
    {
        // Arrange
        var validation = Validation<int, string>.Failure("original error");
        var binderCalled = false;

        // Act
        var bound = validation.Bind(x =>
        {
            binderCalled = true;
            return Validation<int, string>.Success(x * 2);
        });

        // Assert
        Assert.That(binderCalled, Is.False);
        Assert.That(bound.IsInvalid, Is.True);
        Assert.That(bound.Errors, Has.Count.EqualTo(1));
        Assert.That(bound.Errors[0], Is.EqualTo("original error"));
    }

    [Test]
    public void Bind_ChainOfValidBinds_ProducesFinalResult()
    {
        // Arrange
        var validation = Validation<int, string>.Success(2);

        // Act
        var result = validation
            .Bind(x => Validation<int, string>.Success(x + 3))
            .Bind(x => Validation<int, string>.Success(x * 10));

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Match(v => v, _ => -1), Is.EqualTo(50));
    }

    [Test]
    public void Apply_BothValid_AppliesFunction()
    {
        // Arrange
        var validation = Validation<int, string>.Success(10);
        var validationFunc = Validation<Func<int, int>, string>.Success(x => x * 2);

        // Act
        var result = validation.Apply(validationFunc);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Match(v => v, _ => -1), Is.EqualTo(20));
    }

    [Test]
    public void Apply_BothInvalid_AccumulatesAllErrors()
    {
        // Arrange
        var validation = Validation<int, string>.Failure("value error");
        var validationFunc = Validation<Func<int, int>, string>.Failure("func error");

        // Act
        var result = validation.Apply(validationFunc);

        // Assert
        Assert.That(result.IsInvalid, Is.True);
        Assert.That(result.Errors, Has.Count.EqualTo(2));
        Assert.That(result.Errors, Is.EquivalentTo(new[] { "value error", "func error" }));
    }

    [Test]
    public void Apply_ValueInvalid_AccumulatesErrors()
    {
        // Arrange
        var validation = Validation<int, string>.Failure("value error");
        var validationFunc = Validation<Func<int, int>, string>.Success(x => x * 2);

        // Act
        var result = validation.Apply(validationFunc);

        // Assert
        Assert.That(result.IsInvalid, Is.True);
        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0], Is.EqualTo("value error"));
    }
}
