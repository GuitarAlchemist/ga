---
name: C# Performance & Optimization
description: Guidelines for high-performance C# code in the Guitar Alchemist domain, focusing on memory efficiency, struct usage, and avoiding allocations in hot paths.
---

# C# Performance & Optimization Guidelines

Use this skill when optimizing core theory logic, analysis loops, or audio processing code.

## 1. Structs vs. Classes
**Prioritize `readonly record struct` for Domain Primitives.**

- **Rule**: Small, immutable, value-based objects (Pitch, Interval, FretCoordinate) MUST be `readonly record struct`.
- **Why**: Eliminates GC pressure and improves cache locality.
- **Verification**: Ensure `sizeof(T)` is small (<= 16 bytes ideally) and standard equality checks are used.

## 2. Allocation-Free Collections
**Minimize Heap Allocations in Hot Paths.**

- **Rule**: Prefer `ReadOnlySpan<T>` and `Span<T>` over `List<T>` or `T[]` for temporary buffers.
- **Rule**: Use `stackalloc` for small, fixed-size buffers within methods.
- **Rule**: Avoid LINQ (`.Select()`, `.Where()`) inside critical loops (e.g., iterating through thousands of beats). Use `foreach` or `for` loops instead.
- **Verification**: Use memory profilers or benchmarkdotnet to verify zero-allocation per operation.

## 3. Inlining and Aggressive Optimization
**Hint the JIT Compiler for Small Methods.**

- **Rule**: Use `[MethodImpl(MethodImplOptions.AggressiveInlining)]` for small, frequently called property getters or math operations (e.g., `Pitch.Value`, `Interval.Semitones`).
- **Why**: Removes method call overhead for primitives used millions of times.
- **Caution**: Do not use on large methods; it hurts instruction cache.

## 4. String Handling
**Avoid String Concatenation and Parsing in Logic.**

- **Rule**: NEVER use strings for internal logic (e.g., equality checks by Note Name "C#"). Use Integer IDs or Enums (`PitchClass.Value`, `Note.Chromatic.CSharp`).
- **Rule**: Use `ISpanFormattable` and `TryWrite` patterns for creating string outputs without allocating new strings.
- **Rule**: Pre-compute string representations for fixed domains (like we did with `PitchClass` lookups).

## 5. Collection Initialization
**Optimize sizing and access.**

- **Rule**: Always set generic `List<T>` capacity `new List<T>(count)` if size is known.
- **Rule**: Use `FrozenDictionary<K,V>` or `FrozenSet<T>` for static lookup tables initialized at startup.

## 6. Benchmarking
**Prove it.**

- **Rule**: If optimizing, you must provide a `BenchmarkDotNet` result comparison.
- **Template**:
  ```csharp
  [MemoryDiagnoser]
  public class ConfigBenchmarks
  {
      [Benchmark(Baseline = true)]
      public void OldMethod() { ... }

      [Benchmark]
      public void NewMethod() { ... }
  }
  ```
