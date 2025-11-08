# Chord Query Planner Architecture

## Overview

The Chord Query Planner is a query optimization system for chord generation and filtering. It analyzes chord queries,
creates optimal execution plans, and ensures efficient generation of only the chords that match the requested filters.

## Architecture

```
┌─────────────────────────────────────────────────────┐
│ Controller                                          │
│ - Parses query parameters                          │
│ - Creates ChordFilters object                      │
└────────────────┬────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────┐
│ ContextualChordService                             │
│ - Creates ChordQuery from filters                  │
│ - Delegates to ChordQueryPlanner                   │
└────────────────┬────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────┐
│ ChordQueryPlanner                                   │
│ - Analyzes ChordQuery                              │
│ - Determines optimal execution plan                │
│ - Decides which generators to invoke               │
│ - Creates comprehensive cache key                  │
└────────────────┬────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────┐
│ ContextualChordService (Execution)                 │
│ - Executes plan with caching                       │
│ - Invokes only necessary generators                │
│ - Applies filters during generation (not after)    │
│ - Returns filtered results                         │
└────────────────┬────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────┐
│ Chord Generators (IChordGenerators)                │
│ - GenerateDiatonicChords                          │
│ - GenerateBorrowedChords                          │
│ - GenerateSecondaryDominants                      │
│ - GenerateSecondaryTwoFive                        │
│ - GenerateModalChords                             │
└─────────────────────────────────────────────────────┘
```

## Key Components

### 1. ChordQuery

Immutable query object that represents a chord query request.

```csharp
public record ChordQuery
{
    public ChordQueryType QueryType { get; init; }  // Key, Scale, or Mode
    public Key? Key { get; init; }
    public ScaleMode? ScaleMode { get; init; }
    public ChordFilters Filters { get; init; }
}
```

### 2. ChordQueryPlan

Execution plan created by the planner.

```csharp
public record ChordQueryPlan
{
    public string CacheKey { get; init; }
    public IReadOnlyList<ChordGeneratorType> GeneratorsToInvoke { get; init; }
    public IReadOnlyList<ChordFilterType> FiltersToApply { get; init; }
    public ChordQuery Query { get; init; }
}
```

### 3. ChordQueryPlanner

Analyzes queries and creates optimal execution plans.

**Key Logic:**

- If `OnlyNaturallyOccurring=true`, only invoke `Diatonic` generator
- Otherwise, invoke generators based on `Include*` flags
- Generate comprehensive cache keys including all filter parameters

### 4. IChordGenerators

Interface implemented by `ContextualChordService` to expose chord generators.

```csharp
public interface IChordGenerators
{
    IEnumerable<ChordInContext> GenerateDiatonicChords(Key key, ScaleMode scale, ChordFilters filters);
    IEnumerable<ChordInContext> GenerateBorrowedChords(Key key, ChordFilters filters);
    IEnumerable<ChordInContext> GenerateSecondaryDominants(Key key, ChordFilters filters);
    IEnumerable<ChordInContext> GenerateSecondaryTwoFive(Key key, ChordFilters filters);
    IEnumerable<ChordInContext> GenerateModalChords(ScaleMode mode, ChordFilters filters);
}
```

## Filter Semantics

### OnlyNaturallyOccurring

**Precedence:** HIGHEST - overrides all `Include*` flags

When `OnlyNaturallyOccurring=true`:

- Only diatonic chords are generated
- All `Include*` flags are ignored
- Most efficient - minimal generation

### Include* Flags

**Precedence:** NORMAL - only apply when `OnlyNaturallyOccurring=false`

- `IncludeBorrowedChords` - Add borrowed chords (modal interchange)
- `IncludeSecondaryDominants` - Add secondary dominants (V/x)
- `IncludeSecondaryTwoFive` - Add secondary ii-V progressions

**Default:** All `true` (maximum chord variety)

### MinCommonality

**Precedence:** LOWEST - applied after generation

Filters chords by commonality score (0.0-1.0).

## Cache Key Generation

Cache keys include ALL filter parameters to ensure correct cache hits/misses:

```
{queryType}_{key/scale/mode}_ext:{extension}_stack:{stackingType}_nat:{onlyNaturallyOccurring}_bor:{includeBorrowedChords}_sec:{includeSecondaryDominants}_ii-v:{includeSecondaryTwoFive}_min:{minCommonality}_lim:{limit}
```

**Example:**

```
key_CMajor_ext:Seventh_stack:Tertian_nat:true_bor:false_sec:false_ii-v:false_min:0.00_lim:50
```

## Query Optimization

### Before (Inefficient)

```csharp
// Generate ALL chords
var diatonic = GenerateDiatonicChords();
var borrowed = GenerateBorrowedChords();
var secondaryDominants = GenerateSecondaryDominants();
var secondaryTwoFive = GenerateSecondaryTwoFive();

// Combine all
var all = diatonic.Concat(borrowed).Concat(secondaryDominants).Concat(secondaryTwoFive);

// Filter out most of them
var filtered = all.Where(c => c.IsNaturallyOccurring);  // Wasteful!
```

### After (Efficient)

```csharp
// Planner determines: OnlyNaturallyOccurring=true → only invoke Diatonic generator
var plan = planner.CreatePlan(query);

// Only generate what's needed
var chords = GenerateDiatonicChords();  // No wasted generation!
```

## Benefits

1. **Performance** - Only generate chords that will be returned
2. **Correctness** - Clear filter semantics, no ambiguity
3. **Caching** - Comprehensive cache keys prevent stale results
4. **Maintainability** - Centralized query logic
5. **Testability** - Easy to test query plans independently
6. **Extensibility** - Easy to add new generators or filters

## Usage Example

```csharp
// Controller creates filters from query parameters
var filters = new ChordFilters
{
    Extension = ChordExtension.Seventh,
    OnlyNaturallyOccurring = true,  // Only diatonic chords
    Limit = 7
};

// Service creates query and gets plan
var query = new ChordQuery
{
    QueryType = ChordQueryType.Key,
    Key = cMajor,
    Filters = filters
};

var plan = queryPlanner.CreatePlan(query);
// Plan will only invoke Diatonic generator (efficient!)

// Execute plan
var chords = await ExecutePlanAsync(plan);
// Returns exactly 7 diatonic chords
```

## Future Enhancements

1. **Query Statistics** - Track execution times, cache hit rates
2. **Query Hints** - Allow manual optimization hints
3. **Parallel Generation** - Generate multiple chord types in parallel
4. **Incremental Results** - Stream results as they're generated
5. **Query Validation** - Validate queries before execution
6. **Cost-Based Optimization** - Choose generators based on estimated cost

