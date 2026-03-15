---
name: "API Error Enforcer"
description: "Standardises error handling and ApiResponse<T> usage across all 8 Guitar Alchemist microservices. Ensures ErrorHandlingMiddleware is registered, error codes are machine-readable, and the ApiResponse<T> wrapper is not duplicated per-service."
---

# API Error Enforcer

## Role
Find and fix inconsistencies in how the 8 microservices handle errors, structure error responses, and share the `ApiResponse<T>` model. The goal is a single, predictable error contract that every API consumer can rely on.

---

## 1. The Standard Error Contract

All services should return errors in this shape (from `GaApi/Models/ApiResponse.cs`):

```json
{
  "success": false,
  "error": "CHORD_NOT_FOUND",
  "errorDetails": "No chord with id 'xyz' was found in the database.",
  "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "timestamp": "2026-02-27T12:00:00Z"
}
```

Key fields:
- **`error`** — machine-readable `SCREAMING_SNAKE_CASE` error code (from `ErrorCodes.cs`)
- **`errorDetails`** — human-readable message safe for display
- **`correlationId`** — from the `X-Request-ID` request header or generated if absent

---

## 2. ApiResponse<T> Duplication Audit

Each service has its own `ApiResponse.cs`. Only GaApi's version is complete.

```bash
# Find all ApiResponse model files
Get-ChildItem Apps/ga-server -Recurse -Filter "ApiResponse.cs" | Select-Object FullName
```

**Target state:** A single `ApiResponse<T>` in `AllProjects.ServiceDefaults` (already referenced by all services). Delete per-service copies after migration.

**Migration steps per service:**
1. Add project reference to `AllProjects.ServiceDefaults` (if not already present).
2. Replace `using <Service>.Models;` → `using AllProjects.ServiceDefaults;` for ApiResponse.
3. Recompile; fix any remaining ambiguities.
4. Delete the local `ApiResponse.cs`.

---

## 3. ErrorHandlingMiddleware Registration

GaApi registers `ErrorHandlingMiddleware` in `Program.cs`. Microservices do not.

**Pattern to add to each `Program.cs`** (after `app.UseRouting()`):
```csharp
app.UseMiddleware<ErrorHandlingMiddleware>();
```

If `ErrorHandlingMiddleware` is GaApi-specific, copy/move it to `AllProjects.ServiceDefaults` first.

---

## 4. Controller Error Handling Anti-Patterns

### Anti-pattern 1 — Returning raw exception messages

```csharp
// VIOLATION
catch (Exception ex)
{
    return StatusCode(500, ex.Message); // Leaks internal details
}

// CORRECT
catch (Exception ex)
{
    logger.LogError(ex, "Failed to process request");
    return StatusCode(500, new { error = ErrorCodes.InternalError, errorDetails = "An unexpected error occurred." });
}
```

### Anti-pattern 2 — Inconsistent 400 vs 422

- `400 BadRequest` — malformed request (can't parse).
- `422 UnprocessableEntity` — well-formed but fails business validation.

Use `StatusCodes.Status422UnprocessableEntity` for domain validation failures.

### Anti-pattern 3 — Missing correlation ID propagation

Every error response **must** include `correlationId` for distributed tracing. If the middleware handles this automatically, verify the middleware is registered (see Section 3).

---

## 5. ErrorCodes Registry

Machine-readable error codes live in `Apps/ga-server/GaApi/Models/ErrorCodes.cs`.
When adding new error scenarios:
1. Add a `public const string NewError = "NEW_ERROR";` entry.
2. Use it in the controller: `ApiResponse<T>.Fail(ErrorCodes.NewError, "description")`.
3. Document the code and its meaning in a comment.

---

## 6. Workflow

1. Pick an `open` error-enforcer item from BACKLOG.
2. Identify the affected service(s).
3. Apply the smallest fix (often a one-line middleware registration or a model consolidation).
4. Run `pwsh Scripts/api-quality-check.ps1`.
5. Update BACKLOG: `done`.
