# Railway-Oriented Programming (ROP) Patterns

This skill guides correct use of the `GA.Core.Functional` monadic types in Guitar Alchemist.
Consult it whenever writing or reviewing **service-layer C#** code.

## The Four Types — When to Use Each

| Type | Use when | Success side | Failure side |
|------|----------|-------------|-------------|
| `Try<T>` | Wrapping an operation that might throw (infrastructure calls, parsing) | `T` value | `Exception` |
| `Result<T, TError>` | Business logic with explicit typed errors | `T` value | `TError` (e.g. `ChordError`) |
| `Validation<T>` | Input validation that should accumulate multiple errors | `T` value | `IReadOnlyList<string>` |
| `Option<T>` | A value that may legitimately be absent (lookup by id) | `Some(T)` | `None` |

## The Core Rule

**Service methods must never throw.** Return a monadic type instead.

```csharp
// ❌ WRONG — naked throw leaks past the service boundary
public async Task<List<Chord>> GetByQualityAsync(string quality)
{
    if (string.IsNullOrWhiteSpace(quality))
        throw new ArgumentException("Quality required");  // ← forces caller to catch
    return await _db.GetChordsByQualityAsync(quality);
}

// ✅ CORRECT — caller gets typed information, no try/catch needed
public async Task<Result<List<Chord>, ChordError>> GetByQualityAsync(string quality)
{
    var validation = ValidateQuality(quality);
    if (validation.IsInvalid)
        return Result<List<Chord>, ChordError>.Failure(
            new ChordError(ChordErrorType.ValidationError, validation.Errors.First()));

    var tryResult = await Try.OfAsync(() => _db.GetChordsByQualityAsync(quality));
    return tryResult.Match<Result<List<Chord>, ChordError>>(
        onSuccess: chords => Result<List<Chord>, ChordError>.Success(chords),
        onFailure: ex  => Result<List<Chord>, ChordError>.Failure(
            new ChordError(ChordErrorType.DatabaseError, ex.Message)));
}
```

## Boundary Rule — Controllers and CLI Entry Points May Throw

Exceptions from external inputs (invalid HTTP payloads, bad CLI args) are fine to catch
at the **boundary** and convert to HTTP status codes or exit codes. Internal service layers
must not throw.

```csharp
// ✅ Controller boundary — OK to pattern-match and return ActionResult
public async Task<ActionResult> GetByQuality(string quality)
{
    var result = await _service.GetByQualityAsync(quality);
    return result.Match<ActionResult>(
        onSuccess: chords => Ok(chords),
        onFailure: err => err.ErrorType switch
        {
            ChordErrorType.ValidationError => BadRequest(new { error = err.Message }),
            ChordErrorType.DatabaseError   => StatusCode(503, new { error = err.Message }),
            _                              => Problem(err.Message)
        });
}
```

## Wrapping Infrastructure with `Try`

Use `Try.Of` / `Try.OfAsync` at the point where you call MongoDB, HTTP APIs, or
other operations that throw.

```csharp
// Single async operation
var tryCount = await Try.OfAsync(() => _mongoDb.GetTotalChordCountAsync());

// Chain: wrap then project
return tryCount.Match<Result<long, ChordError>>(
    onSuccess: n    => Result<long, ChordError>.Success(n),
    onFailure: ex   => Result<long, ChordError>.Failure(
        new ChordError(ChordErrorType.DatabaseError, ex.Message)));
```

## Validation — Accumulate All Errors Before Failing

```csharp
private static Validation<string> ValidateQuality(string quality)
{
    if (string.IsNullOrWhiteSpace(quality))
        return Validation<string>.Invalid("Quality cannot be empty.");

    var allowed = new[] { "Major", "Minor", "Dominant7", "Major7", "Minor7" };
    return Array.Exists(allowed, q => q.Equals(quality, StringComparison.OrdinalIgnoreCase))
        ? Validation<string>.Valid(quality)
        : Validation<string>.Invalid($"'{quality}' is not a recognised chord quality.");
}
```

## Option — Lookups That May Return Nothing

```csharp
// ✅ Returns None instead of null / NotFoundException
public async Task<Option<Chord>> GetByIdAsync(string id)
{
    var tryChord = await Try.OfAsync(() => _db.FindChordByIdAsync(id));
    return tryChord.Match(
        onSuccess: chord => chord is null ? Option<Chord>.None() : Option<Chord>.Some(chord),
        onFailure: _     => Option<Chord>.None());
}

// Controller usage
var option = await _service.GetByIdAsync(id);
return option.Match<ActionResult>(
    onSome: chord => Ok(chord),
    onNone: ()    => NotFound());
```

## MonadicServiceBase Pattern

Services that do cache + DB calls inherit `MonadicServiceBase<T>` and use its helpers:

```csharp
public class MyService(MongoDbService db, IMemoryCache cache, ILogger<MyService> logger)
    : MonadicServiceBase<MyService>(logger, cache)
{
    public async Task<Try<long>> GetCountAsync() =>
        await GetOrSetCacheWithTryAsync(
            "my_count",
            () => db.GetCountAsync(),
            TimeSpan.FromMinutes(5));
}
```

## Anti-Patterns Checklist

| Anti-pattern | Fix |
|-------------|-----|
| `throw new ArgumentException(...)` in a service method | Return `Result.Failure(new MyError(ValidationError, ...))` |
| `return null` from a service method | Return `Option<T>.None()` |
| `.Result` on an async Task inside a service | `await` it inside `Try.OfAsync` |
| `try { } catch (Exception ex) { throw; }` re-throw | Let `Try.OfAsync` handle it |
| Unchecked `GetValueOrThrow()` without prior `IsSuccess` guard | Use `.Match()` instead |

## File Locations

- Monadic types: `Common/GA.Core/Functional/` (Try, Result, Validation, Option, Writer, Io, LazyM)
- Example service: `Apps/ga-server/GaApi/Services/MonadicChordService.cs`
- Base class: `Apps/ga-server/GaApi/Services/MonadicServiceBase.cs`
- Error types: `Apps/ga-server/GaApi/Models/ErrorCodes.cs`
