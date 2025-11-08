# Monadic Microservices - Implementation Guide

## Overview

This guide shows how to implement microservices using the monadic framework for better error handling, type safety, and composability.

---

## 🎯 Why Use Monadic Microservices?

### Traditional Approach Problems

```csharp
// ❌ Traditional approach - problems:
public async Task<FloorData?> GetFloorAsync(int floorNumber)
{
    try
    {
        var response = await _httpClient.GetAsync($"/api/floor/{floorNumber}");
        response.EnsureSuccessStatusCode(); // Throws exception
        
        var json = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<ApiResponse>(json);
        
        return apiResponse?.Data; // Nullable - can be null!
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error fetching floor");
        return null; // Lost error information!
    }
}
```

**Problems:**
1. ❌ Returns `null` - loses error information
2. ❌ Exceptions for control flow
3. ❌ No type safety for errors
4. ❌ Hard to compose operations
5. ❌ Difficult to test

### Monadic Approach Benefits

```csharp
// ✅ Monadic approach - benefits:
public async Task<Result<FloorData, FloorError>> GetFloorAsync(int floorNumber)
{
    var validation = ValidateFloorNumber(floorNumber);
    if (validation is Validation<int, ValidationError>.Failure failure)
    {
        return new Result<FloorData, FloorError>.Failure(
            new FloorError(FloorErrorType.ValidationError, failure.Errors.First().Message)
        );
    }

    var url = $"/api/floor/{floorNumber}";
    var httpResult = await _monadicHttpClient.GetWithResultAsync<ApiResponse>(url);
    
    return httpResult.Match(
        onSuccess: apiResponse => apiResponse.Data != null
            ? new Result<FloorData, FloorError>.Success(apiResponse.Data)
            : new Result<FloorData, FloorError>.Failure(
                new FloorError(FloorErrorType.DataNotFound, "Floor data is null")),
        onFailure: httpError => new Result<FloorData, FloorError>.Failure(
            new FloorError(FloorErrorType.NetworkError, httpError.ToString()))
    );
}
```

**Benefits:**
1. ✅ Explicit error types - no information loss
2. ✅ No exceptions for control flow
3. ✅ Type-safe errors
4. ✅ Easy to compose with LINQ
5. ✅ Easy to test

---

## 📦 Core Components

### 1. MonadicServiceBase

Base class for all monadic services:

```csharp
public class MyService : MonadicServiceBase<MyService>
{
    public MyService(ILogger<MyService> logger, IMemoryCache cache)
        : base(logger, cache)
    {
    }

    // Use helper methods:
    // - Execute<T>() - Try monad
    // - ExecuteWithResult<T>() - Result monad
    // - GetOrSetCacheAsync<T>() - Caching
    // - ExecuteWithLogging<T>() - Writer monad
    // - Validate<T>() - Validation monad
}
```

### 2. MonadicHttpClient

Type-safe HTTP client wrapper:

```csharp
public class MyService
{
    private readonly MonadicHttpClient _httpClient;

    public MyService(IHttpClientFactory factory, ILogger<MonadicHttpClient> logger)
    {
        _httpClient = factory.CreateMonadicClient("MyApi", logger);
    }

    // Use methods:
    // - GetAsync<T>() - Returns Try<T>
    // - GetWithResultAsync<T>() - Returns Result<T, HttpError>
    // - PostAsync<TReq, TRes>() - Returns Try<TRes>
    // - GetWithRetry<T>() - Returns IO<Try<T>>
}
```

---

## 🚀 Implementation Patterns

### Pattern 1: Simple GET with Try Monad

```csharp
public async Task<Try<User>> GetUserAsync(string userId)
{
    var url = $"/api/users/{userId}";
    var result = await _httpClient.GetAsync<User>(url);
    
    return result.Map(user =>
    {
        // Transform or enrich the user
        Logger.LogInformation("Got user: {UserId}", user.Id);
        return user;
    });
}

// Usage:
var userTry = await service.GetUserAsync("123");
userTry.Match(
    onSuccess: user => Console.WriteLine($"User: {user.Name}"),
    onFailure: ex => Console.WriteLine($"Error: {ex.Message}")
);
```

### Pattern 2: GET with Result Monad and Typed Errors

```csharp
public async Task<Result<User, UserError>> GetUserWithResultAsync(string userId)
{
    // Validate input
    var validation = ValidationHelpers.NotNullOrEmpty(userId, "UserId");
    if (validation is Validation<string, ValidationError>.Failure failure)
    {
        return new Result<User, UserError>.Failure(
            new UserError(UserErrorType.ValidationError, failure.Errors.First().Message)
        );
    }

    var url = $"/api/users/{userId}";
    var httpResult = await _httpClient.GetWithResultAsync<User>(url);
    
    return httpResult.Match(
        onSuccess: user => new Result<User, UserError>.Success(user),
        onFailure: httpError => new Result<User, UserError>.Failure(
            new UserError(
                httpError.StatusCode == 404 ? UserErrorType.NotFound : UserErrorType.NetworkError,
                httpError.ToString()
            )
        )
    );
}

// Usage:
var result = await service.GetUserWithResultAsync("123");
result.Match(
    onSuccess: user => Console.WriteLine($"User: {user.Name}"),
    onFailure: error => Console.WriteLine($"Error [{error.Type}]: {error.Message}")
);
```

### Pattern 3: Caching with Monads

```csharp
public async Task<Try<List<Product>>> GetProductsAsync(string category)
{
    var cacheKey = $"products_{category}";
    
    return await GetOrSetCacheWithTryAsync(
        cacheKey,
        async () =>
        {
            var url = $"/api/products?category={category}";
            var result = await _httpClient.GetAsync<List<Product>>(url);
            return result.Match(
                onSuccess: products => products,
                onFailure: ex => throw ex // Re-throw to be caught by Try
            );
        },
        expiration: TimeSpan.FromMinutes(10)
    );
}
```

### Pattern 4: Validation with Accumulating Errors

```csharp
public Result<CreateUserRequest, List<ValidationError>> ValidateCreateUserRequest(
    string name,
    string email,
    int age)
{
    var nameValidation = ValidationHelpers.NotNullOrEmpty(name, "Name");
    var emailValidation = ValidateEmail(email);
    var ageValidation = ValidationHelpers.InRange(age, 0, 150, "Age");

    var errors = new List<ValidationError>();
    
    if (nameValidation is Validation<string, ValidationError>.Failure nf)
        errors.AddRange(nf.Errors);
    
    if (emailValidation is Validation<string, ValidationError>.Failure ef)
        errors.AddRange(ef.Errors);
    
    if (ageValidation is Validation<int, ValidationError>.Failure af)
        errors.AddRange(af.Errors);

    return errors.Any()
        ? new Result<CreateUserRequest, List<ValidationError>>.Failure(errors)
        : new Result<CreateUserRequest, List<ValidationError>>.Success(
            new CreateUserRequest(name, email, age));
}

private Validation<string, ValidationError> ValidateEmail(string email)
{
    return email.Contains('@')
        ? Validation.Success<string, ValidationError>(email)
        : Validation.Fail<string, ValidationError>(
            new ValidationError("Email", "Invalid email format"));
}
```

### Pattern 5: Retry with IO Monad

```csharp
public IO<Try<Data>> GetDataWithRetry(string id)
{
    return IO.Of(async () =>
    {
        var url = $"/api/data/{id}";
        return await _httpClient.GetAsync<Data>(url);
    }).Retry(maxAttempts: 3, delay: TimeSpan.FromSeconds(1));
}

// Usage:
var io = service.GetDataWithRetry("123");
var tryData = io.UnsafeRun(); // Executes with retry
```

### Pattern 6: Lazy Loading

```csharp
public LazyM<Task<Try<ExpensiveData>>> GetExpensiveDataLazy(string id)
{
    return LazyM.Of(async () =>
    {
        Logger.LogInformation("Computing expensive data for {Id}", id);
        var url = $"/api/expensive/{id}";
        return await _httpClient.GetAsync<ExpensiveData>(url);
    });
}

// Usage:
var lazy = service.GetExpensiveDataLazy("123");
// Not computed yet!
var tryData = await lazy.Value; // Computed on first access
var tryData2 = await lazy.Value; // Uses cached value
```

### Pattern 7: Composing Multiple Operations

```csharp
public async Task<Result<OrderSummary, OrderError>> CreateOrderAsync(
    string userId,
    List<string> productIds)
{
    // Validate user
    var userResult = await GetUserWithResultAsync(userId);
    if (userResult is Result<User, UserError>.Failure userFailure)
    {
        return new Result<OrderSummary, OrderError>.Failure(
            new OrderError(OrderErrorType.InvalidUser, userFailure.Error.Message)
        );
    }

    var user = ((Result<User, UserError>.Success)userResult).Value;

    // Get all products
    var productResults = await Task.WhenAll(
        productIds.Select(id => GetProductAsync(id))
    );

    // Check if any product fetch failed
    var failedProducts = productResults
        .Where(r => r is Try<Product>.Failure)
        .ToList();

    if (failedProducts.Any())
    {
        return new Result<OrderSummary, OrderError>.Failure(
            new OrderError(OrderErrorType.ProductNotFound, "Some products not found")
        );
    }

    var products = productResults
        .Select(r => ((Try<Product>.Success)r).Value)
        .ToList();

    // Create order
    var order = new Order(user, products);
    var summary = new OrderSummary(order.Id, order.Total, products.Count);

    return new Result<OrderSummary, OrderError>.Success(summary);
}
```

---

## 📊 Comparison Table

| Pattern | Traditional | Monadic |
|---------|------------|---------|
| **Error Handling** | `try-catch`, returns `null` | `Try<T>`, `Result<T, E>` |
| **Null Safety** | `T?`, nullable references | `Option<T>` |
| **Validation** | Throw on first error | `Validation<T, E>` accumulates all errors |
| **HTTP Calls** | `HttpClient` with exceptions | `MonadicHttpClient` with `Try`/`Result` |
| **Caching** | Manual `TryGetValue` | `GetOrSetCacheAsync` |
| **Retry Logic** | Manual loops | `IO<T>.Retry()` |
| **Lazy Loading** | `Lazy<T>` | `LazyM<T>` with monadic composition |
| **Logging** | Side effects everywhere | `Writer<TLog, T>` monad |
| **Composition** | Difficult, nested `if`s | LINQ query syntax |

---

## ✅ Best Practices

1. **Use Result for business errors, Try for exceptions**
   - `Result<T, E>` - Expected errors (validation, not found, etc.)
   - `Try<T>` - Unexpected exceptions (network, parsing, etc.)

2. **Always validate input**
   - Use `Validation<T, E>` monad
   - Accumulate all errors before returning

3. **Cache aggressively**
   - Use `GetOrSetCacheAsync` helper
   - Set appropriate expiration times

4. **Log at boundaries**
   - Log in service methods, not in monads
   - Use `Writer<TLog, T>` for operation logging

5. **Compose with LINQ**
   - Use `from...select` syntax
   - Chain operations with `Map` and `Bind`

6. **Type your errors**
   - Create specific error types (e.g., `UserError`, `OrderError`)
   - Include error codes/types for client handling

---

## 🎸 Guitar Alchemist Examples

See these files for real-world examples:

- **`Apps/FloorManager/Services/MonadicFloorService.cs`** - Complete monadic service
- **`Common/GA.Business.Core/Microservices/MonadicHttpClient.cs`** - HTTP client wrapper
- **`Common/GA.Business.Core/Microservices/MonadicServiceBase.cs`** - Base class
- **`Common/GA.Business.Core/Microservices/Examples/AdvancedMonadsExample.cs`** - All monad examples

---

## 🚀 Migration Guide

### Step 1: Add MonadicServiceBase

```csharp
// Before:
public class MyService
{
    private readonly ILogger<MyService> _logger;
    
    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }
}

// After:
public class MyService : MonadicServiceBase<MyService>
{
    public MyService(ILogger<MyService> logger, IMemoryCache cache)
        : base(logger, cache)
    {
    }
}
```

### Step 2: Replace try-catch with Try/Result

```csharp
// Before:
public async Task<User?> GetUserAsync(string id)
{
    try
    {
        var response = await _httpClient.GetAsync($"/api/users/{id}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<User>();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to get user");
        return null;
    }
}

// After:
public async Task<Try<User>> GetUserAsync(string id)
{
    return await ExecuteAsync(async () =>
    {
        var response = await _httpClient.GetAsync($"/api/users/{id}");
        response.EnsureSuccessStatusCode();
        var user = await response.Content.ReadFromJsonAsync<User>();
        return user ?? throw new InvalidOperationException("User is null");
    }, $"GetUser({id})");
}
```

### Step 3: Use MonadicHttpClient

```csharp
// Before:
var response = await _httpClient.GetAsync(url);
response.EnsureSuccessStatusCode();
var data = await response.Content.ReadFromJsonAsync<Data>();

// After:
var tryData = await _monadicHttpClient.GetAsync<Data>(url);
```

---

**This guide demonstrates how to build robust, type-safe microservices using functional programming patterns!** 🎸✨

