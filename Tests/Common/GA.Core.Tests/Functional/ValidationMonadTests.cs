namespace GA.Core.Tests.Functional;

using System.Collections.Immutable;
using Core.Functional;

/// <summary>
///     Tests verifying that Validation&lt;T&gt; works correctly for error accumulation.
/// </summary>
[TestFixture]
[Category("Functional")]
[Category("Validation")]
public class ValidationMonadTests
{
    [Test]
    public void Valid_CreatesValidValidation()
    {
        // Arrange & Act
        var validation = Validation<int>.Valid(42);

        // Assert
        Assert.That(validation.IsValid, Is.True);
        Assert.That(validation.IsInvalid, Is.False);
        Assert.That(validation.GetValueOrThrow(), Is.EqualTo(42));
        Assert.That(validation.Errors, Is.Empty);
    }

    [Test]
    public void Invalid_SingleError_CreatesInvalidValidation()
    {
        // Arrange & Act
        var validation = Validation<int>.Invalid("error message");

        // Assert
        Assert.That(validation.IsValid, Is.False);
        Assert.That(validation.IsInvalid, Is.True);
        Assert.That(validation.Errors, Has.Count.EqualTo(1));
        Assert.That(validation.Errors[0], Is.EqualTo("error message"));
    }

    [Test]
    public void Invalid_MultipleErrors_CreatesInvalidValidation()
    {
        // Arrange
        var errors = ImmutableList.Create("error1", "error2", "error3");

        // Act
        var validation = Validation<int>.Invalid(errors);

        // Assert
        Assert.That(validation.IsValid, Is.False);
        Assert.That(validation.IsInvalid, Is.True);
        Assert.That(validation.Errors, Has.Count.EqualTo(3));
        Assert.That(validation.Errors, Is.EquivalentTo(errors));
    }

    [Test]
    public void Map_Valid_TransformsValue()
    {
        // Arrange
        var validation = Validation<int>.Valid(10);

        // Act
        var mapped = validation.Map(x => x * 2);

        // Assert
        Assert.That(mapped.IsValid, Is.True);
        Assert.That(mapped.GetValueOrThrow(), Is.EqualTo(20));
    }

    [Test]
    public void Map_Invalid_PreservesErrors()
    {
        // Arrange
        var validation = Validation<int>.Invalid("error");

        // Act
        var mapped = validation.Map(x => x * 2);

        // Assert
        Assert.That(mapped.IsInvalid, Is.True);
        Assert.That(mapped.Errors, Has.Count.EqualTo(1));
        Assert.That(mapped.Errors[0], Is.EqualTo("error"));
    }

    [Test]
    public void Apply_BothValid_AppliesFunction()
    {
        // Arrange
        var validation = Validation<int>.Valid(10);
        var validationFunc = Validation<Func<int, int>>.Valid(x => x * 2);

        // Act
        var result = validation.Apply(validationFunc);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.GetValueOrThrow(), Is.EqualTo(20));
    }

    [Test]
    public void Apply_ValueInvalid_AccumulatesErrors()
    {
        // Arrange
        var validation = Validation<int>.Invalid("value error");
        var validationFunc = Validation<Func<int, int>>.Valid(x => x * 2);

        // Act
        var result = validation.Apply(validationFunc);

        // Assert
        Assert.That(result.IsInvalid, Is.True);
        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0], Is.EqualTo("value error"));
    }

    [Test]
    public void Apply_FunctionInvalid_AccumulatesErrors()
    {
        // Arrange
        var validation = Validation<int>.Valid(10);
        var validationFunc = Validation<Func<int, int>>.Invalid("function error");

        // Act
        var result = validation.Apply(validationFunc);

        // Assert
        Assert.That(result.IsInvalid, Is.True);
        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0], Is.EqualTo("function error"));
    }

    [Test]
    public void Apply_BothInvalid_AccumulatesAllErrors()
    {
        // Arrange
        var validation = Validation<int>.Invalid("value error");
        var validationFunc = Validation<Func<int, int>>.Invalid("function error");

        // Act
        var result = validation.Apply(validationFunc);

        // Assert
        Assert.That(result.IsInvalid, Is.True);
        Assert.That(result.Errors, Has.Count.EqualTo(2));
        Assert.That(result.Errors, Is.EquivalentTo(new[] { "value error", "function error" }));
    }

    [Test]
    public void Combine_BothValid_CombinesValues()
    {
        // Arrange
        var validation1 = Validation<int>.Valid(10);
        var validation2 = Validation<int>.Valid(20);

        // Act
        var result = validation1.Combine(validation2, (a, b) => a + b);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.GetValueOrThrow(), Is.EqualTo(30));
    }

    [Test]
    public void Combine_FirstInvalid_AccumulatesErrors()
    {
        // Arrange
        var validation1 = Validation<int>.Invalid("error1");
        var validation2 = Validation<int>.Valid(20);

        // Act
        var result = validation1.Combine(validation2, (a, b) => a + b);

        // Assert
        Assert.That(result.IsInvalid, Is.True);
        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0], Is.EqualTo("error1"));
    }

    [Test]
    public void Combine_SecondInvalid_AccumulatesErrors()
    {
        // Arrange
        var validation1 = Validation<int>.Valid(10);
        var validation2 = Validation<int>.Invalid("error2");

        // Act
        var result = validation1.Combine(validation2, (a, b) => a + b);

        // Assert
        Assert.That(result.IsInvalid, Is.True);
        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0], Is.EqualTo("error2"));
    }

    [Test]
    public void Combine_BothInvalid_AccumulatesAllErrors()
    {
        // Arrange
        var validation1 = Validation<int>.Invalid("error1");
        var validation2 = Validation<int>.Invalid("error2");

        // Act
        var result = validation1.Combine(validation2, (a, b) => a + b);

        // Assert
        Assert.That(result.IsInvalid, Is.True);
        Assert.That(result.Errors, Has.Count.EqualTo(2));
        Assert.That(result.Errors, Is.EquivalentTo(new[] { "error1", "error2" }));
    }

    [Test]
    public void Combine_ThreeValidations_AllValid()
    {
        // Arrange
        var validation1 = Validation<int>.Valid(10);
        var validation2 = Validation<int>.Valid(20);
        var validation3 = Validation<int>.Valid(30);

        // Act
        var result = ValidationExtensions.Combine(
            validation1,
            validation2,
            validation3,
            (a, b, c) => a + b + c);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.GetValueOrThrow(), Is.EqualTo(60));
    }

    [Test]
    public void Combine_ThreeValidations_AllInvalid_AccumulatesAllErrors()
    {
        // Arrange
        var validation1 = Validation<int>.Invalid("error1");
        var validation2 = Validation<int>.Invalid("error2");
        var validation3 = Validation<int>.Invalid("error3");

        // Act
        var result = ValidationExtensions.Combine(
            validation1,
            validation2,
            validation3,
            (a, b, c) => a + b + c);

        // Assert
        Assert.That(result.IsInvalid, Is.True);
        Assert.That(result.Errors, Has.Count.EqualTo(3));
        Assert.That(result.Errors, Is.EquivalentTo(new[] { "error1", "error2", "error3" }));
    }

    [Test]
    public void Sequence_AllValid_ReturnsValidList()
    {
        // Arrange
        var validations = new[]
        {
            Validation<int>.Valid(1),
            Validation<int>.Valid(2),
            Validation<int>.Valid(3)
        };

        // Act
        var result = validations.Sequence();

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.GetValueOrThrow(), Is.EquivalentTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void Sequence_SomeInvalid_AccumulatesAllErrors()
    {
        // Arrange
        var validations = new[]
        {
            Validation<int>.Valid(1),
            Validation<int>.Invalid("error2"),
            Validation<int>.Valid(3),
            Validation<int>.Invalid("error4")
        };

        // Act
        var result = validations.Sequence();

        // Assert
        Assert.That(result.IsInvalid, Is.True);
        Assert.That(result.Errors, Has.Count.EqualTo(2));
        Assert.That(result.Errors, Is.EquivalentTo(new[] { "error2", "error4" }));
    }

    [Test]
    public void Sequence_EmptyList_ReturnsValidEmptyList()
    {
        // Arrange
        var validations = Array.Empty<Validation<int>>();

        // Act
        var result = validations.Sequence();

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.GetValueOrThrow(), Is.Empty);
    }

    [Test]
    public void Traverse_AllValid_ReturnsValidList()
    {
        // Arrange
        var values = new[] { 1, 2, 3 };
        Func<int, Validation<int>> f = x => Validation<int>.Valid(x * 2);

        // Act
        var result = values.Traverse(f);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.GetValueOrThrow(), Is.EquivalentTo(new[] { 2, 4, 6 }));
    }

    [Test]
    public void Traverse_SomeInvalid_AccumulatesAllErrors()
    {
        // Arrange
        var values = new[] { 1, 2, 3, 4 };
        Func<int, Validation<int>> f = x =>
            x % 2 == 0
                ? Validation<int>.Invalid($"error{x}")
                : Validation<int>.Valid(x * 2);

        // Act
        var result = values.Traverse(f);

        // Assert
        Assert.That(result.IsInvalid, Is.True);
        Assert.That(result.Errors, Has.Count.EqualTo(2));
        Assert.That(result.Errors, Is.EquivalentTo(new[] { "error2", "error4" }));
    }

    private record UserRegistration(string Username, string Email, int Age);

    private static Validation<string> ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return Validation<string>.Invalid("Username is required");
        }

        if (username.Length < 3)
        {
            return Validation<string>.Invalid("Username must be at least 3 characters");
        }

        return Validation<string>.Valid(username);
    }

    private static Validation<string> ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Validation<string>.Invalid("Email is required");
        }

        if (!email.Contains('@'))
        {
            return Validation<string>.Invalid("Email must contain @");
        }

        return Validation<string>.Valid(email);
    }

    private static Validation<int> ValidateAge(int age)
    {
        if (age < 18)
        {
            return Validation<int>.Invalid("Must be at least 18 years old");
        }

        if (age > 120)
        {
            return Validation<int>.Invalid("Age must be realistic");
        }

        return Validation<int>.Valid(age);
    }

    [Test]
    public void FormValidation_AllValid_CreatesUser()
    {
        // Arrange
        var usernameValidation = ValidateUsername("john_doe");
        var emailValidation = ValidateEmail("john@example.com");
        var ageValidation = ValidateAge(25);

        // Act
        var result = ValidationExtensions.Combine(
            usernameValidation,
            emailValidation,
            ageValidation,
            (username, email, age) => new UserRegistration(username, email, age));

        // Assert
        Assert.That(result.IsValid, Is.True);
        var user = result.GetValueOrThrow();
        Assert.That(user.Username, Is.EqualTo("john_doe"));
        Assert.That(user.Email, Is.EqualTo("john@example.com"));
        Assert.That(user.Age, Is.EqualTo(25));
    }

    [Test]
    public void FormValidation_MultipleErrors_AccumulatesAll()
    {
        // Arrange
        var usernameValidation = ValidateUsername("ab"); // Too short
        var emailValidation = ValidateEmail("invalid"); // No @
        var ageValidation = ValidateAge(15); // Too young

        // Act
        var result = ValidationExtensions.Combine(
            usernameValidation,
            emailValidation,
            ageValidation,
            (username, email, age) => new UserRegistration(username, email, age));

        // Assert
        Assert.That(result.IsInvalid, Is.True);
        Assert.That(result.Errors, Has.Count.EqualTo(3));
        Assert.That(result.Errors, Is.EquivalentTo(new[]
        {
            "Username must be at least 3 characters",
            "Email must contain @",
            "Must be at least 18 years old"
        }));
    }

    [Test]
    public void FormValidation_PartialErrors_AccumulatesOnlyErrors()
    {
        // Arrange
        var usernameValidation = ValidateUsername("john_doe"); // Valid
        var emailValidation = ValidateEmail("invalid"); // No @
        var ageValidation = ValidateAge(15); // Too young

        // Act
        var result = ValidationExtensions.Combine(
            usernameValidation,
            emailValidation,
            ageValidation,
            (username, email, age) => new UserRegistration(username, email, age));

        // Assert
        Assert.That(result.IsInvalid, Is.True);
        Assert.That(result.Errors, Has.Count.EqualTo(2));
        Assert.That(result.Errors, Is.EquivalentTo(new[]
        {
            "Email must contain @",
            "Must be at least 18 years old"
        }));
    }
}
