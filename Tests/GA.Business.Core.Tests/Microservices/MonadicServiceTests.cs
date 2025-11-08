namespace GA.Business.Core.Tests.Microservices;

using GA.Business.Microservices;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Unit tests for monadic microservices framework
/// </summary>
[TestFixture]
public class MonadicServiceTests
{
    [Test]
    public void Option_Some_ShouldContainValue()
    {
        // Arrange & Act
        var option = new Option<int>.Some(42);

        // Assert
        Assert.That(option, Is.InstanceOf<Option<int>.Some>());
        var result = option.Match(
            onSome: value => value,
            onNone: () => 0
        );
        Assert.That(result, Is.EqualTo(42));
    }

    [Test]
    public void Option_None_ShouldBeEmpty()
    {
        // Arrange & Act
        var option = new Option<int>.None();

        // Assert
        Assert.That(option, Is.InstanceOf<Option<int>.None>());
        var result = option.Match(
            onSome: value => value,
            onNone: () => -1
        );
        Assert.That(result, Is.EqualTo(-1));
    }

    [Test]
    public void Option_Map_ShouldTransformValue()
    {
        // Arrange
        var option = new Option<int>.Some(42);

        // Act
        var mapped = option.Map(x => x * 2);

        // Assert
        var result = mapped.Match(
            onSome: value => value,
            onNone: () => 0
        );
        Assert.That(result, Is.EqualTo(84));
    }

    [Test]
    public void Result_Success_ShouldContainValue()
    {
        // Arrange & Act
        var result = new Result<int, string>.Success(42);

        // Assert
        Assert.That(result, Is.InstanceOf<Result<int, string>.Success>());
        var value = result.Match(
            onSuccess: v => v,
            onFailure: _ => 0
        );
        Assert.That(value, Is.EqualTo(42));
    }

    [Test]
    public void Result_Failure_ShouldContainError()
    {
        // Arrange & Act
        var result = new Result<int, string>.Failure("Error occurred");

        // Assert
        Assert.That(result, Is.InstanceOf<Result<int, string>.Failure>());
        var error = result.Match(
            onSuccess: _ => "No error",
            onFailure: e => e
        );
        Assert.That(error, Is.EqualTo("Error occurred"));
    }

    [Test]
    public void Result_Map_ShouldTransformSuccessValue()
    {
        // Arrange
        var result = new Result<int, string>.Success(42);

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        var value = mapped.Match(
            onSuccess: v => v,
            onFailure: _ => 0
        );
        Assert.That(value, Is.EqualTo(84));
    }

    [Test]
    public void Result_Map_ShouldPreserveFailure()
    {
        // Arrange
        var result = new Result<int, string>.Failure("Error");

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        var error = mapped.Match(
            onSuccess: _ => "No error",
            onFailure: e => e
        );
        Assert.That(error, Is.EqualTo("Error"));
    }

    [Test]
    public void Try_Success_ShouldContainValue()
    {
        // Arrange & Act
        var tryResult = new Try<int>.Success(42);

        // Assert
        Assert.That(tryResult, Is.InstanceOf<Try<int>.Success>());
        var value = tryResult.Match(
            onSuccess: v => v,
            onFailure: _ => 0
        );
        Assert.That(value, Is.EqualTo(42));
    }

    [Test]
    public void Try_Failure_ShouldContainException()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        // Act
        var tryResult = new Try<int>.Failure(exception);

        // Assert
        Assert.That(tryResult, Is.InstanceOf<Try<int>.Failure>());
        var error = tryResult.Match(
            onSuccess: _ => "No error",
            onFailure: ex => ex.Message
        );
        Assert.That(error, Is.EqualTo("Test error"));
    }

    [Test]
    public void Try_Of_ShouldCaptureException()
    {
        // Arrange & Act
        var tryResult = Try.Of<int>(() => throw new InvalidOperationException("Test error"));

        // Assert
        Assert.That(tryResult, Is.InstanceOf<Try<int>.Failure>());
        var error = tryResult.Match(
            onSuccess: _ => "No error",
            onFailure: ex => ex.Message
        );
        Assert.That(error, Is.EqualTo("Test error"));
    }

    [Test]
    public void Try_Of_ShouldCaptureSuccess()
    {
        // Arrange & Act
        var tryResult = Try.Of(() => 42);

        // Assert
        Assert.That(tryResult, Is.InstanceOf<Try<int>.Success>());
        var value = tryResult.Match(
            onSuccess: v => v,
            onFailure: _ => 0
        );
        Assert.That(value, Is.EqualTo(42));
    }

    [Test]
    public async Task Try_OfAsync_ShouldCaptureAsyncException()
    {
        // Arrange & Act
        var tryResult = await Try.OfAsync<int>(async () =>
        {
            await Task.Delay(10);
            throw new InvalidOperationException("Async error");
        });

        // Assert
        Assert.That(tryResult, Is.InstanceOf<Try<int>.Failure>());
        var error = tryResult.Match(
            onSuccess: _ => "No error",
            onFailure: ex => ex.Message
        );
        Assert.That(error, Is.EqualTo("Async error"));
    }

    [Test]
    public async Task Try_OfAsync_ShouldCaptureAsyncSuccess()
    {
        // Arrange & Act
        var tryResult = await Try.OfAsync(async () =>
        {
            await Task.Delay(10);
            return 42;
        });

        // Assert
        Assert.That(tryResult, Is.InstanceOf<Try<int>.Success>());
        var value = tryResult.Match(
            onSuccess: v => v,
            onFailure: _ => 0
        );
        Assert.That(value, Is.EqualTo(42));
    }

    [Test]
    public void Validation_Success_ShouldContainValue()
    {
        // Arrange & Act
        var validation = Validation.Success<int, string>(42);

        // Assert
        Assert.That(validation, Is.InstanceOf<Validation<int, string>.Success>());
        var value = validation.Match(
            onSuccess: v => v,
            onFailure: _ => 0
        );
        Assert.That(value, Is.EqualTo(42));
    }

    [Test]
    public void Validation_Failure_ShouldAccumulateErrors()
    {
        // Arrange & Act
        var validation = Validation.Fail<int, string>("Error 1", "Error 2", "Error 3");

        // Assert
        Assert.That(validation, Is.InstanceOf<Validation<int, string>.Failure>());
        var errors = validation.Match(
            onSuccess: _ => new List<string>(),
            onFailure: errs => errs.ToList()
        );
        Assert.That(errors, Has.Count.EqualTo(3));
        Assert.That(errors, Contains.Item("Error 1"));
        Assert.That(errors, Contains.Item("Error 2"));
        Assert.That(errors, Contains.Item("Error 3"));
    }

    [Test]
    public void Validation_Combine_ShouldAccumulateAllErrors()
    {
        // Arrange
        var v1 = Validation.Fail<int, string>("Error 1");
        var v2 = Validation.Fail<int, string>("Error 2");
        var v3 = Validation.Fail<int, string>("Error 3");

        // Act
        var combined = Validation.Combine(v1, v2, v3);

        // Assert
        Assert.That(combined, Is.InstanceOf<Validation<(int, int, int), string>.Failure>());
        var errors = combined.Match(
            onSuccess: _ => new List<string>(),
            onFailure: errs => errs.ToList()
        );
        Assert.That(errors, Has.Count.EqualTo(3));
    }

    [Test]
    public void Option_LINQ_ShouldComposeOperations()
    {
        // Arrange
        var option1 = new Option<int>.Some(10);
        var option2 = new Option<int>.Some(20);

        // Act
        var result = from x in option1
                     from y in option2
                     select x + y;

        // Assert
        var value = result.Match(
            onSome: v => v,
            onNone: () => 0
        );
        Assert.That(value, Is.EqualTo(30));
    }

    [Test]
    public void Result_LINQ_ShouldComposeOperations()
    {
        // Arrange
        var result1 = new Result<int, string>.Success(10);
        var result2 = new Result<int, string>.Success(20);

        // Act
        var combined = from x in result1
                       from y in result2
                       select x + y;

        // Assert
        var value = combined.Match(
            onSuccess: v => v,
            onFailure: _ => 0
        );
        Assert.That(value, Is.EqualTo(30));
    }

    [Test]
    public void Result_LINQ_ShouldShortCircuitOnFailure()
    {
        // Arrange
        var result1 = new Result<int, string>.Success(10);
        var result2 = new Result<int, string>.Failure("Error");

        // Act
        var combined = from x in result1
                       from y in result2
                       select x + y;

        // Assert
        var error = combined.Match(
            onSuccess: _ => "No error",
            onFailure: e => e
        );
        Assert.That(error, Is.EqualTo("Error"));
    }
}

