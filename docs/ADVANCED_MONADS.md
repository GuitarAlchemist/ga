# Advanced Monads - Exception Handling, Logging, DI & Side Effects

This document describes the **advanced monads** added to the functional microservices framework, bringing Spring Boot-like patterns to C#.

## Overview

We've expanded the framework from 5 core monads to **10 total monads**, adding:

### Exception Handling & Error Management
1. **Try Monad** - Exception handling (Spring @ExceptionHandler)
2. **Either Monad** - Two valid paths (more general than Result)
3. **Validation Monad** - Accumulating errors (Spring @Valid)

### Side Effects & Logging
4. **Writer Monad** - Logging alongside computation (Spring AOP)
5. **IO Monad** - Side effects (Spring @Transactional)

### Lazy Evaluation & DI
6. **Lazy Monad** - Lazy initialization (Spring @Lazy)
7. **ServiceLocator Monad** - Service location (Spring ApplicationContext)
8. **Scope Monad** - Scoped dependencies (Spring @Scope)

Plus the original 5 core monads: **Option**, **Result**, **Reader**, **State**, **Async**

---

## Exception Handling & Error Management

### 1. Try Monad (Spring @ExceptionHandler)

**Purpose:** Wraps exception-throwing code in a monad for safe error handling.

**Spring Boot Equivalent:** `@ExceptionHandler`, `@ControllerAdvice`

**Key Features:**
- Captures exceptions as values
- Railway-oriented error handling
- Recovery from exceptions
- Conversion to Result monad

**Example:**
```csharp
// Wrap exception-throwing code
Try<int> result = Try.Of(() => int.Parse("42"));

// Chain operations
var doubled = result
    .Map(x => x * 2)
    .Recover(ex => 0); // Provide default on error

// Pattern matching
doubled.Match(
    onSuccess: value => Console.WriteLine($"Result: {value}"),
    onFailure: ex => Console.WriteLine($"Error: {ex.Message}")
);

// Convert to Result monad
Result<int, Exception> resultMonad = result.ToResult();
```

**LINQ Support:**
```csharp
var result = from x in Try.Of(() => int.Parse("42"))
             from y in Try.Of(() => int.Parse("10"))
             select x + y;
```

---

### 2. Either Monad

**Purpose:** Represents one of two possible values (more general than Result).

**Key Features:**
- Both sides are valid values (not just success/failure)
- Map either left or right side
- Swap sides
- Pattern matching

**Example:**
```csharp
// Either can represent two different valid outcomes
Either<string, int> ParseOrGetLength(string input)
{
    if (int.TryParse(input, out var number))
        return new Either<string, int>.Right(number);
    else
        return new Either<string, int>.Left(input);
}

var result = ParseOrGetLength("42")
    .Map(x => x * 2) // Maps the Right side
    .MapLeft(str => str.ToUpper()); // Maps the Left side

result.Match(
    onLeft: str => Console.WriteLine($"String: {str}"),
    onRight: num => Console.WriteLine($"Number: {num}")
);

// Swap sides
var swapped = result.Swap();
```

---

### 3. Validation Monad (Spring @Valid)

**Purpose:** Accumulates errors instead of short-circuiting (like Spring's BindingResult).

**Spring Boot Equivalent:** `@Valid`, `BindingResult`, `@Validated`

**Key Features:**
- Accumulates ALL validation errors
- Applicative functor (combines validations)
- Perfect for form validation
- Similar to Spring Boot's @Valid annotation

**Example:**
```csharp
// Validate individual fields
Validation<string, ValidationError> ValidateName(string name)
    => string.IsNullOrWhiteSpace(name)
        ? Validation.Fail<string, ValidationError>(new ValidationError("Name", "Name is required"))
        : Validation.Success<string, ValidationError>(name);

Validation<string, ValidationError> ValidateEmail(string email)
    => !email.Contains('@')
        ? Validation.Fail<string, ValidationError>(new ValidationError("Email", "Invalid email"))
        : Validation.Success<string, ValidationError>(email);

// Combine validations - accumulates ALL errors
var nameValidation = ValidateName("");
var emailValidation = ValidateEmail("invalid");

var result = Validation<User, ValidationError>.Combine(
    nameValidation.Map(_ => user),
    emailValidation.Map(_ => user)
);

result.Match(
    onSuccess: user => Console.WriteLine($"Valid: {user}"),
    onFailure: errors =>
    {
        foreach (var error in errors)
            Console.WriteLine($"{error.Field}: {error.Message}");
    }
);

// Output:
// Name: Name is required
// Email: Invalid email
```

---

## Side Effects & Logging

### 4. Writer Monad (Spring Logging Aspects)

**Purpose:** Accumulates log messages alongside computation (like Spring AOP logging).

**Spring Boot Equivalent:** `@Aspect`, `@Around`, logging aspects

**Key Features:**
- Accumulates logs during computation
- Similar to Spring Boot's logging aspects
- Composable logging
- No side effects until executed

**Example:**
```csharp
Writer<LogEntry, int> Add(int a, int b)
{
    var result = a + b;
    var log = new LogEntry(DateTime.UtcNow, "INFO", $"Added {a} + {b} = {result}");
    return new Writer<LogEntry, int>(result, [log]);
}

Writer<LogEntry, int> Multiply(int a, int b)
{
    var result = a * b;
    var log = new LogEntry(DateTime.UtcNow, "INFO", $"Multiplied {a} * {b} = {result}");
    return new Writer<LogEntry, int>(result, [log]);
}

// Chain operations with accumulated logs
var computation = from sum in Add(5, 3)
                  from product in Multiply(sum, 2)
                  select product;

Console.WriteLine($"Result: {computation.Value}");
foreach (var log in computation.Log)
    Console.WriteLine($"[{log.Level}] {log.Message}");

// Output:
// Result: 16
// [INFO] Added 5 + 3 = 8
// [INFO] Multiplied 8 * 2 = 16
```

---

### 5. IO Monad (Spring @Transactional)

**Purpose:** Represents side-effectful computations (like Spring's transaction management).

**Spring Boot Equivalent:** `@Transactional`, `TransactionTemplate`

**Key Features:**
- Pure description of side effects
- Retry on failure
- Delay execution
- Composable side effects
- Nothing happens until `UnsafeRun()` is called

**Example:**
```csharp
IO<string> ReadFile(string path) => IO.Of(() => File.ReadAllText(path));
IO<Unit> WriteFile(string path, string content) => IO.Run(() => File.WriteAllText(path, content));
IO<Unit> LogMessage(string message) => IO.Run(() => Console.WriteLine($"[LOG] {message}"));

// Compose side effects (pure description)
var program = from _ in LogMessage("Starting operation")
              from content in ReadFile("test.txt")
              from __ in LogMessage($"Read {content.Length} characters")
              from ___ in WriteFile("output.txt", content.ToUpper())
              from ____ in LogMessage("Operation complete")
              select Unit.Value;

// Nothing happens until we run it!
program.UnsafeRun();

// Retry on failure
var unreliableOperation = IO.Of(() =>
{
    if (Random.Shared.Next(10) < 7)
        throw new Exception("Random failure");
    return "Success!";
});

var withRetry = unreliableOperation.Retry(maxAttempts: 3, delay: TimeSpan.FromSeconds(1));
var result = withRetry.UnsafeRun();
```

---

## Lazy Evaluation & DI

### 6. Lazy Monad (Spring @Lazy)

**Purpose:** Deferred computation (like Spring Boot's @Lazy annotation).

**Spring Boot Equivalent:** `@Lazy` annotation for lazy bean initialization

**Key Features:**
- Lazy initialization
- Value computed on first access
- Cached after first evaluation
- Composable lazy computations

**Example:**
```csharp
LazyM<int> ExpensiveComputation() => LazyM.Of(() =>
{
    Console.WriteLine("Computing expensive value...");
    Thread.Sleep(1000);
    return 42;
});

var lazy = ExpensiveComputation();
Console.WriteLine("Created (not computed yet)");

// Value is computed on first access
Console.WriteLine($"Value: {lazy.Value}"); // Computes here
Console.WriteLine($"Value: {lazy.Value}"); // Uses cached value

// Output:
// Created (not computed yet)
// Computing expensive value...
// Value: 42
// Value: 42

// Chain lazy computations
var result = from x in ExpensiveComputation()
             from y in LazyM.Return(10)
             select x + y;
```

---

### 7. ServiceLocator Monad (Spring ApplicationContext)

**Purpose:** Type-safe service location (like Spring's ApplicationContext.getBean()).

**Spring Boot Equivalent:** `ApplicationContext.getBean()`, `@Autowired`

**Key Features:**
- Type-safe service resolution
- Optional or required services
- Composable service location
- Integration with IServiceProvider

**Example:**
```csharp
// Type-safe service location
var locator = from logger in ServiceLocator.Get<ILogger<object>>()
              from config in ServiceLocator.Get<IConfiguration>()
              select (logger, config);

locator.Run(provider).Match(
    onSome: services =>
    {
        var (logger, config) = services;
        logger.LogInformation("Got services!");
        return Unit.Value;
    },
    onNone: () =>
    {
        Console.WriteLine("Services not found");
        return Unit.Value;
    }
);

// Required service (throws if not found)
var logger = ServiceLocator.GetRequired<ILogger<object>>()
    .GetOrThrow(provider);
```

---

### 8. Scope Monad (Spring @Scope)

**Purpose:** Manages scoped dependencies (like Spring Boot's @Scope annotation).

**Spring Boot Equivalent:** `@Scope("request")`, `@Scope("session")`, `@RequestScope`

**Key Features:**
- Scoped dependency management
- Run in existing or new scope
- Composable scoped operations
- Integration with IServiceProvider

**Example:**
```csharp
var scopedOperation = Scope.Of<ILogger<object>>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<object>>();
    logger.LogInformation("Running in scope");
    return logger;
});

// Run with new scope
var logger = scopedOperation.RunWithNewScope(provider);

// Run in existing scope
var logger2 = scopedOperation.RunInScope(provider);
```

---

## Comparison with Spring Boot

| Spring Boot Pattern | C# Monad Equivalent |
|---------------------|---------------------|
| `@ExceptionHandler` | `Try<T>` monad |
| `@Valid` + `BindingResult` | `Validation<T, E>` monad |
| `@Aspect` + logging | `Writer<TLog, T>` monad |
| `@Transactional` | `IO<T>` monad |
| `@Lazy` | `LazyM<T>` monad |
| `ApplicationContext.getBean()` | `ServiceLocator<T>` monad |
| `@Scope("request")` | `Scope<T>` monad |
| `Optional<T>` | `Option<T>` monad |
| `Result<T, E>` | `Result<T, E>` monad |

---

## All 10 Monads Summary

1. **Option<T>** - Explicit optionality (no null!)
2. **Result<T, E>** - Railway-oriented error handling
3. **Reader<TEnv, T>** - Dependency injection as a monad
4. **State<TState, T>** - Immutable state threading
5. **Async<T>** - Monadic async operations
6. **Try<T>** - Exception handling (NEW!)
7. **Either<TLeft, TRight>** - Two valid paths (NEW!)
8. **Validation<T, E>** - Accumulating errors (NEW!)
9. **Writer<TLog, T>** - Logging alongside computation (NEW!)
10. **IO<T>** - Side effects (NEW!)
11. **LazyM<T>** - Lazy initialization (NEW!)
12. **ServiceLocator<T>** - Service location (NEW!)
13. **Scope<T>** - Scoped dependencies (NEW!)

All monads support:
- ✅ Functor operations (Map)
- ✅ Monad operations (Bind/SelectMany)
- ✅ Full LINQ query syntax
- ✅ Pattern matching
- ✅ Immutability (C# records)

---

## See Also

- `Common/GA.Business.Core/Microservices/FunctionalBootstrap.cs` - All monad implementations
- `Common/GA.Business.Core/Microservices/Examples/MonadicServiceExample.cs` - Original 5 monads examples
- `Common/GA.Business.Core/Microservices/Examples/AdvancedMonadsExample.cs` - New 8 monads examples
- `docs/FUNCTIONAL_MICROSERVICES.md` - Original framework documentation

