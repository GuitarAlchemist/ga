---
name: "Annotations Guidance"
description: "Guidelines for using JetBrains.Annotations vs native System.Diagnostics.CodeAnalysis attributes in C# 14/.NET 10+ codebases with nullable reference types."
---
# Annotations Guidance Skill

## Purpose
This skill provides guidance on when to use JetBrains.Annotations vs native `System.Diagnostics.CodeAnalysis` attributes in C# 14 / .NET 10+ codebases with nullable reference types enabled.

## Core Principle
With nullable reference types enabled (`<Nullable>enable</Nullable>`), most JetBrains nullability annotations are **redundant**. Prefer native C# nullability syntax and `System.Diagnostics.CodeAnalysis` attributes.

---

## JetBrains Annotations to KEEP

### `[PublicAPI]`
- **No native equivalent**
- Valuable for API documentation and static analysis tools
- Indicates a type or member is part of the public API
- **Action**: Keep all usages

### `[Pure]`
- **No native equivalent**
- Indicates a method has no side effects
- **Action**: Keep all usages

### `[MustUseReturnValue]`
- **No native equivalent**
- Indicates the return value should not be discarded
- **Action**: Keep all usages

---

## JetBrains Annotations to REMOVE (Redundant)

### `[NotNull]`
- **Native equivalent**: Non-nullable reference types (no `?` suffix)
- **Example**: `string name` is already non-nullable
- **Action**: Remove - redundant with nullable reference types

### `[CanBeNull]`
- **Native equivalent**: Nullable reference types (`?` suffix)
- **Example**: `string? name` is already nullable
- **Action**: Remove - use `?` suffix instead

### `[ItemNotNull]`
- **Native equivalent**: Non-nullable element types
- **Example**: `IEnumerable<string>` (elements are non-nullable)
- **Action**: Remove - redundant

### `[ItemCanBeNull]`
- **Native equivalent**: Nullable element types
- **Example**: `IEnumerable<string?>` (elements are nullable)
- **Action**: Remove - use `?` suffix on element type

---

## JetBrains Annotations to REPLACE

### `[ContractAnnotation]`
Replace with native `System.Diagnostics.CodeAnalysis` attributes:

| JetBrains Pattern | Native Replacement |
|-------------------|-------------------|
| `"null => halt"` | `[DoesNotReturn]` on throw helper |
| `"value:null => false"` | `[NotNullWhen(true)]` on out param |
| `"=> true, value:notnull"` | `[NotNullWhen(true)]` on out param |
| `"=> false, value:null"` | `[MaybeNullWhen(false)]` on out param |

---

## Native Attributes Reference (System.Diagnostics.CodeAnalysis)

### Nullability Flow Attributes

| Attribute | Purpose |
|-----------|---------|
| `[NotNullWhen(bool)]` | Parameter is not null when method returns specified bool |
| `[MaybeNullWhen(bool)]` | Parameter may be null when method returns specified bool |
| `[NotNullIfNotNull(string)]` | Return is not null if specified parameter is not null |
| `[MemberNotNull(string)]` | Member is not null after method returns |
| `[MemberNotNullWhen(bool, string)]` | Member is not null when method returns specified bool |

### Control Flow Attributes

| Attribute | Purpose |
|-----------|---------|
| `[DoesNotReturn]` | Method never returns (always throws) |
| `[DoesNotReturnIf(bool)]` | Method doesn't return if parameter equals specified bool |

---

## Examples

### TryParse Pattern
```csharp
public bool TryParse(string input, [NotNullWhen(true)] out Result? result)
{
    if (string.IsNullOrEmpty(input))
    {
        result = null;
        return false;
    }
    result = new Result(input);
    return true;
}
```

### Equals Override
```csharp
public bool Equals([NotNullWhen(true)] MyType? other)
{
    if (other is null) return false;
    return Value == other.Value;
}
```

### Throw Helper
```csharp
[DoesNotReturn]
private static void ThrowArgumentNull(string paramName)
    => throw new ArgumentNullException(paramName);
```

---

## Codebase Statistics (Guitar Alchemist)
- `[PublicAPI]`: ~185 usages → **Keep all**
- `[NotNull]`: 0 usages (previously 1, removed)
- `[CanBeNull]`: 0 usages
- `[Pure]`: 0 usages
- `[ContractAnnotation]`: 0 usages

## Package Dependency
Keep `JetBrains.Annotations` package for `[PublicAPI]` support. It's lightweight and provides value for API documentation.

