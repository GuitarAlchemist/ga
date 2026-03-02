---
name: "C# Coding Standards & Best Practices"
description: "Definitive guide for C# development within the Guitar Alchemist ecosystem, emphasizing modern C# features, clean architecture, and maintainability."
---

# C# Coding Standards & Best Practices

This skill outlines the mandatory coding standards and best practices for all C# development in the Guitar Alchemist repository. Adherence to these guidelines ensures codebase consistency, readability, and leverage of modern language features.

## 1. Modern C# Features (C# 12+)

We explicitly encourage the latest C# features where they improve clarity and conciseness.

### 1.1 Records & Immutability
- **Prefer `record`** for Data Transfer Objects (DTOs), Domain Events, and Value Objects.
- Use **primary constructors** for concise initialization.
- **Immutability by default**: Use `init` accessors instead of `set` unless mutation is strictly required.

```csharp
// DO:
public record ChordVoicing(string Name, int[] Frets, PitchClass Root);

// DON'T:
public class ChordVoicing
{
    public string Name { get; set; }
    // ...
}
```

### 1.2 Pattern Matching
- Use **pattern matching** (`is`, `switch` expressions) over `if-else` chains and type casting.
- Leverage **list patterns** and **property patterns** for expressive logic.

```csharp
var description = voicing switch
{
    { Frets: [0, ..] } => "Open Chord",
    { Difficulty: > 0.8 } => "Advanced Voicing",
    _ => "Standard Voicing"
};
```

### 1.3 Collection Expressions
- Use **collection expressions** (`[]`) for initialization.
- **IDE0300/IDE0301/IDE0305**: Always fix these warnings by converting to collection expressions.

```csharp
// DO:
int[] notes = [0, 4, 7];
List<string> scale = ["C", "D", "E"];
var copy = new List<int>([..original]);

// DON'T:
int[] notes = new int[] { 0, 4, 7 };
var list = new List<string> { "A", "B" }; // IDE0300 Warning
```

## 2. Architecture & Organization

### 2.1 Namespace Structure
Follow the standard structure: `GA.<Layer>.<Module>.<Component>`
- **Common**: Shared logic, primitives, and utilities.
- **Domain**: Pure business logic, entities, and aggregates.
- **Business**: Application logic, services, and workflows.
- **Infrastructure**: External concerns (Database, GPU, Filesystem).

### 2.2 Dependency Injection
- Use **Constructor Injection** for all dependencies.
- **Avoid** Service Locator pattern.
- Register services in `ServiceCollectionExtensions` specific to each module.

### 2.3 Async/Await
- **Always** usage `async` and `await` for I/O bound operations.
- **Avoid** `.Result` or `.Wait()` which cause deadlocks.
- Use `ConfigureAwait(false)` in library code (Common/Domain layers).

## 3. Code Style & Formatting

### 3.1 Nullable Reference Types
- **Enable** `<Nullable>enable</Nullable>` in all projects.
- Design APIs to be null-safe.
- Use `?` annotation for optional values.

### 3.2 Naming Conventions
- **Classes/Methods/Properties**: PascalCase (`VoicingService`)
- **Parameters/Locals**: camelCase (`voicingService`)
- **Private Fields**: `_camelCase` (`_logger`)
- **Interfaces**: IPascalCase (`IVoicingService`)

### 3.3 Expression-Bodied Members
- Use expression bodies (`=>`) for properties, methods, constructors, and operators whenever the body is a single expression.
- **IDE0022/IDE0021**: Always fix these warnings.

```csharp
// DO:
public int NoteCount => Notes.Count;
public override string ToString() => Name;
public Point(int x, int y) => (X, Y) = (x, y);

// DON'T: (for single expressions)
public int NoteCount { get { return Notes.Count; } }
public override string ToString()
{
    return Name;
}
```

## 4. Performance Considerations
- Use `Span<T>` and `ReadOnlySpan<T>` for hot paths involving array slicing or string manipulation.
- Prefer `ValueTask` for methods that often complete synchronously.
- Use `ArrayPool<T>` for large temporary buffers.

## 5. Testing
- Write **unit tests** for all Domain logic.
- Use meaningful test names: `MethodName_State_ExpectedBehavior`.
- Mock external dependencies using `NSubstitute` or `Moq`.

## 6. Warning Resolution Policy
This repository aims for **Zero Warnings**. When you encounter build warnings, you MUST fix them immediately.

### Common Warnings & Fixes

| Warning | Description | Fix |
| :--- | :--- | :--- |
| **IDE0022** | Use expression body for method | Convert `{ return x; }` to `=> x;` |
| **IDE0021** | Use expression body for constructor | Convert `{ _x = x; }` to `=> _x = x;` |
| **IDE0300** | Collection initialization | Use `[...]` syntax instead of `new T[] { ... }` |
| **IDE0305** | Collection initialization | Use `[..items]` instead of `items.ToArray()` |
| **CS8600** | Null literal or possible null | Ensure null safety or use `!` if strictly guaranteed |
| **CS0162** | Unreachable code | Remove dead code |
| **CA1822** | Member does not access instance data | Make method `static` |

## 7. How to Use This Skill
When modifying existing code or creating new files:
1. **Check adherence** to these standards.
2. **Refactor** legacy code (e.g., converting legacy classes to records) when touching it.
3. **Question** implementations that deviate (e.g., "Why is this mutable?").
4. **Fix Warnings** proactively in any file you edit.
