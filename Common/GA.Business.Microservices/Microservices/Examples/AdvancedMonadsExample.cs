namespace GA.Business.Core.Microservices.Microservices.Examples;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
///     Advanced monads examples: Try, Either, Validation, Writer, IO, Lazy, ServiceLocator, Scope
///     Demonstrates Spring Boot-like patterns with functional programming
/// </summary>

#region Domain Models

public record User(string Id, string Name, string Email, int Age);

public record ValidationError(string Field, string Message);

public record LogEntry(DateTime Timestamp, string Level, string Message);

#endregion

#region Example 1: Try Monad - Exception Handling (Spring @ExceptionHandler)

public static class TryMonadExamples
{
    // Wrap exception-throwing code
    public static Try<int> ParseInt(string input)
    {
        return Try.Of(() => int.Parse(input));
    }

    // Chain operations that might throw
    public static Try<User> LoadUser(string userId)
    {
        return Try.Of(() =>
        {
            if (userId == "invalid")
            {
                throw new ArgumentException("Invalid user ID");
            }

            return new User(userId, "John Doe", "john@example.com", 30);
        });
    }

    // Example: Parse and validate
    public static void Example1()
    {
        var result = ParseInt("42")
            .Map(x => x * 2)
            .Match(
                value => $"Result: {value}",
                ex => $"Error: {ex.Message}"
            );

        Console.WriteLine(result); // Output: Result: 84
    }

    // Example: Recover from exception
    public static void Example2()
    {
        var result = ParseInt("invalid")
            .Recover(_ => 0) // Provide default value
            .Match(
                value => $"Result: {value}",
                ex => $"Error: {ex.Message}"
            );

        Console.WriteLine(result); // Output: Result: 0
    }

    // Example: Convert to Result monad
    public static void Example3()
    {
        var result = ParseInt("42").ToResult();

        result.Match<Unit>(
            value =>
            {
                Console.WriteLine($"Success: {value}");
                return Unit.Value;
            },
            ex =>
            {
                Console.WriteLine($"Error: {ex.Message}");
                return Unit.Value;
            }
        );
    }
}

#endregion

#region Example 2: Either Monad - Two Valid Paths

public static class EitherMonadExamples
{
    // Either can represent two different valid outcomes
    public static Either<string, int> ParseOrGetLength(string input)
    {
        if (int.TryParse(input, out var number))
        {
            return new Either<string, int>.Right(number);
        }

        return new Either<string, int>.Left(input);
    }

    // Example: Process either path
    public static void Example1()
    {
        var result1 = ParseOrGetLength("42")
            .Map(x => x * 2)
            .Match(
                str => $"String: {str}",
                num => $"Number: {num}"
            );

        var result2 = ParseOrGetLength("hello")
            .MapLeft(str => str.ToUpper())
            .Match(
                str => $"String: {str}",
                num => $"Number: {num}"
            );

        Console.WriteLine(result1); // Output: Number: 84
        Console.WriteLine(result2); // Output: String: HELLO
    }

    // Example: Swap sides
    public static void Example2()
    {
        var either = new Either<string, int>.Left("error");
        var swapped = either.Swap(); // Now Right("error")

        swapped.Match<Unit>(
            num =>
            {
                Console.WriteLine($"Number: {num}");
                return Unit.Value;
            },
            str =>
            {
                Console.WriteLine($"String: {str}");
                return Unit.Value;
            }
        );
    }
}

#endregion

#region Example 3: Validation Monad - Accumulating Errors (Spring @Valid)

public static class ValidationMonadExamples
{
    // Validate individual fields
    public static Validation<string, ValidationError> ValidateName(string name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? Validation.Fail<string, ValidationError>(new ValidationError("Name", "Name is required"))
            : Validation.Success<string, ValidationError>(name);
    }

    public static Validation<string, ValidationError> ValidateEmail(string email)
    {
        return !email.Contains('@')
            ? Validation.Fail<string, ValidationError>(new ValidationError("Email", "Invalid email format"))
            : Validation.Success<string, ValidationError>(email);
    }

    public static Validation<int, ValidationError> ValidateAge(int age)
    {
        return age < 0 || age > 150
            ? Validation.Fail<int, ValidationError>(new ValidationError("Age", "Age must be between 0 and 150"))
            : Validation.Success<int, ValidationError>(age);
    }

    // Example: Accumulate all validation errors (like Spring's BindingResult)
    public static void Example1()
    {
        var nameValidation = ValidateName("");
        var emailValidation = ValidateEmail("invalid");
        var ageValidation = ValidateAge(-5);

        // Combine validations - accumulates ALL errors
        var result = Validation<User, ValidationError>.Combine(
            nameValidation.Map(_ => new User("1", "", "", 0)),
            emailValidation.Map(_ => new User("1", "", "", 0)),
            ageValidation.Map(_ => new User("1", "", "", 0))
        );

        result.Match<Unit>(
            user =>
            {
                Console.WriteLine($"Valid user: {user.Name}");
                return Unit.Value;
            },
            errors =>
            {
                Console.WriteLine("Validation errors:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"  - {error.Field}: {error.Message}");
                }

                return Unit.Value;
            }
        );
        // Output:
        // Validation errors:
        //   - Name: Name is required
        //   - Email: Invalid email format
        //   - Age: Age must be between 0 and 150
    }
}

#endregion

#region Example 4: Writer Monad - Logging (Spring Logging Aspects)

public static class WriterMonadExamples
{
    // Computation with logging
    public static Writer<LogEntry, int> Add(int a, int b)
    {
        var result = a + b;
        var log = new LogEntry(DateTime.UtcNow, "INFO", $"Added {a} + {b} = {result}");
        return new Writer<LogEntry, int>(result, [log]);
    }

    public static Writer<LogEntry, int> Multiply(int a, int b)
    {
        var result = a * b;
        var log = new LogEntry(DateTime.UtcNow, "INFO", $"Multiplied {a} * {b} = {result}");
        return new Writer<LogEntry, int>(result, [log]);
    }

    // Example: Chain operations with accumulated logs
    public static void Example1()
    {
        var computation = from sum in Add(5, 3)
            from product in Multiply(sum, 2)
            select product;

        Console.WriteLine($"Result: {computation.Value}");
        Console.WriteLine("Logs:");
        foreach (var log in computation.Log)
        {
            Console.WriteLine($"  [{log.Level}] {log.Message}");
        }

        // Output:
        // Result: 16
        // Logs:
        //   [INFO] Added 5 + 3 = 8
        //   [INFO] Multiplied 8 * 2 = 16
    }

    // Example: Add custom log entries
    public static void Example2()
    {
        var computation = Writer.Return<LogEntry, int>(42)
            .Tell(new LogEntry(DateTime.UtcNow, "DEBUG", "Starting computation"))
            .Map(x => x * 2)
            .Tell(new LogEntry(DateTime.UtcNow, "INFO", "Doubled the value"));

        Console.WriteLine($"Result: {computation.Value}");
        foreach (var log in computation.Log)
        {
            Console.WriteLine($"  [{log.Level}] {log.Message}");
        }
    }
}

#endregion

#region Example 5: IO Monad - Side Effects (Spring @Transactional)

public static class IoMonadExamples
{
    // Pure description of side effects
    public static Io<string> ReadFile(string path)
    {
        return Io.Of(() => File.ReadAllText(path));
    }

    public static Io<Unit> WriteFile(string path, string content)
    {
        return Io.Run(() => File.WriteAllText(path, content));
    }

    public static Io<Unit> LogMessage(string message)
    {
        return Io.Run(() => Console.WriteLine($"[LOG] {message}"));
    }

    // Example: Compose side effects
    public static void Example1()
    {
        var program = from _ in LogMessage("Starting operation")
            from content in ReadFile("test.txt")
            from __ in LogMessage($"Read {content.Length} characters")
            from ___ in WriteFile("output.txt", content.ToUpper())
            from ____ in LogMessage("Operation complete")
            select Unit.Value;

        // Nothing happens until we run it!
        // program.UnsafeRun();
    }

    // Example: Retry on failure
    public static void Example2()
    {
        var unreliableOperation = Io.Of(() =>
        {
            if (Random.Shared.Next(10) < 7)
            {
                throw new Exception("Random failure");
            }

            return "Success!";
        });

        var withRetry = unreliableOperation.Retry(3, TimeSpan.FromSeconds(1));

        // var result = withRetry.UnsafeRun();
    }
}

#endregion

#region Example 6: Lazy Monad - Lazy Initialization (Spring @Lazy)

public static class LazyMonadExamples
{
    // Expensive computation
    public static LazyM<int> ExpensiveComputation()
    {
        return LazyM.Of(() =>
        {
            Console.WriteLine("Computing expensive value...");
            Thread.Sleep(1000);
            return 42;
        });
    }

    // Example: Deferred evaluation
    public static void Example1()
    {
        var lazy = ExpensiveComputation();
        Console.WriteLine("Lazy value created (not computed yet)");

        // Value is computed on first access
        Console.WriteLine($"Value: {lazy.Value}");
        Console.WriteLine($"Value again: {lazy.Value}"); // Uses cached value

        // Output:
        // Lazy value created (not computed yet)
        // Computing expensive value...
        // Value: 42
        // Value again: 42
    }

    // Example: Chain lazy computations
    public static void Example2()
    {
        var lazy = from x in ExpensiveComputation()
            from y in LazyM.Return(10)
            select x + y;

        Console.WriteLine($"Result: {lazy.Value}");
    }
}

#endregion

#region Example 7: ServiceLocator Monad - DI (Spring ApplicationContext)

public static class ServiceLocatorExamples
{
    // Example: Type-safe service location
    public static void Example1(IServiceProvider provider)
    {
        var locator = from logger in ServiceLocator.Get<ILogger<object>>()
            from config in ServiceLocator.Get<IConfiguration>()
            select (logger, config);

        locator.Run(provider).Match<Unit>(
            services =>
            {
                var (logger, config) = services;
                logger.LogInformation("Got services!");
                return Unit.Value;
            },
            () =>
            {
                Console.WriteLine("Services not found");
                return Unit.Value;
            }
        );
    }

    // Example: Required service
    public static void Example2(IServiceProvider provider)
    {
        var logger = ServiceLocator.GetRequired<ILogger<object>>()
            .GetOrThrow(provider);

        logger.LogInformation("Got required service");
    }
}

#endregion

#region Example 8: Scope Monad - Scoped Dependencies (Spring @Scope)

public static class ScopeMonadExamples
{
    // Example: Run in new scope
    public static void Example1(IServiceProvider provider)
    {
        var scopedOperation = Scope.Of<ILogger<object>>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<object>>();
            logger.LogInformation("Running in scope");
            return logger;
        });

        // Run with new scope
        var logger = scopedOperation.RunWithNewScope(provider);
    }

    // Example: Chain scoped operations
    public static void Example2(IServiceProvider provider)
    {
        var loggerScope = Scope.Of<ILogger<object>>(sp => sp.GetRequiredService<ILogger<object>>());
        var configScope = Scope.Of<IConfiguration>(sp => sp.GetRequiredService<IConfiguration>());

        var logger = loggerScope.RunInScope(provider);
        var config = configScope.RunInScope(provider);

        logger.LogInformation("Got scoped services");
    }
}

#endregion
