# Fretboard Shape Graph System

## Overview

The Shape Graph system provides intelligent fretboard navigation using:

- **Shape Generation**: Automatic discovery of playable shapes for any pitch-class set
- **Graph Construction**: Building transition graphs with harmonic and physical costs
- **Markov Navigation**: Probabilistic exploration with temperature control
- **Heat Maps**: Visual probability distributions for next positions
- **Practice Paths**: Gradual difficulty progression for learning

## Core Concepts

### Fretboard Shape

A **FretboardShape** represents a playable fingering pattern on the fretboard:

```csharp
public sealed record FretboardShape
{
    public string Id { get; init; }                    // Unique hash
    public string TuningId { get; init; }              // Tuning identifier
    public PitchClassSet PitchClassSet { get; init; }  // Pitch-class content
    public IntervalClassVector ICV { get; init; }      // Interval content
    public IReadOnlyList<PositionLocation> Positions { get; init; }  // String/fret pairs
    public int StringMask { get; init; }               // Bitmask of used strings
    public int MinFret { get; init; }                  // Lowest fret
    public int MaxFret { get; init; }                  // Highest fret
    public int Span { get; }                           // Fret range (max - min)
    public double Diagness { get; init; }              // 0 = box, 1 = diagonal
    public double Ergonomics { get; init; }            // 0-1, higher is better
    public int FingerCount { get; init; }              // Fingers required
    public Dictionary<string, string> Tags { get; init; }  // Categorization
}
```

**Key Properties**:

- **Diagness**: Measures how diagonal the shape is (0 = box, 1 = diagonal)
- **Ergonomics**: Score based on span, stretch, and hand position (0-1)
- **Tags**: Categorization (shape: box/diagonal/mixed, type: open/barre, difficulty: easy/medium/hard)

### Shape Transition

A **ShapeTransition** represents a move from one shape to another:

```csharp
public sealed record ShapeTransition
{
    public string FromId { get; init; }              // Source shape
    public string ToId { get; init; }                // Target shape
    public GrothendieckDelta Delta { get; init; }    // Harmonic change
    public double HarmonicCost { get; init; }        // L1 norm of delta
    public double PhysicalCost { get; init; }        // Finger travel cost
    public double Score { get; }                     // Combined cost
    public double Weight { get; }                    // Probability weight
}
```

**Cost Components**:

- **Harmonic Cost**: L1 norm of Grothendieck delta (interval-class change)
- **Physical Cost**: Position shift + span change + string pattern change + diagness change
- **Score**: Total cost (lower is better)
- **Weight**: Inverse probability (higher is better)

### Shape Graph

A **ShapeGraph** is a directed graph of shapes with weighted transitions:

```csharp
public sealed record ShapeGraph
{
    public string TuningId { get; init; }
    public IReadOnlyDictionary<string, FretboardShape> Shapes { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyList<ShapeTransition>> Adjacency { get; init; }
    
    public int ShapeCount { get; }
    public int TransitionCount { get; }
}
```

## Usage

### 1. Build a Shape Graph

```csharp
using GA.Business.Core.Fretboard.Shapes;
using GA.Business.Core.Atonal;

// Get services
var builder = serviceProvider.GetRequiredService<IShapeGraphBuilder>();

// Define tuning
var tuning = Tuning.Default; // Standard tuning (E A D G B E)

// Select pitch-class sets (e.g., all 7-note scales)
var scales = PitchClassSet.Items.Where(pcs => pcs.Cardinality == 7).ToList();

// Build options
var options = new ShapeGraphBuildOptions
{
    MaxFret = 12,
    MaxSpan = 5,
    MinErgonomics = 0.3,
    MaxShapesPerSet = 20,
    MaxHarmonicDistance = 3,
    MaxPhysicalCost = 5.0
};

// Build graph
var graph = await builder.BuildGraphAsync(tuning, scales, options);

Console.WriteLine($"Built graph with {graph.ShapeCount} shapes and {graph.TransitionCount} transitions");
```

### 2. Generate Shapes for a Specific Pitch-Class Set

```csharp
// C Major scale
var cMajor = PitchClassSet.Parse("024579B");

// Generate all shapes
var shapes = builder.GenerateShapes(tuning, cMajor, options);

foreach (var shape in shapes.OrderByDescending(s => s.Ergonomics).Take(5))
{
    Console.WriteLine(shape);
    // Output: C Major [0 2 2 0 0 0] (span:2, diag:0.25, ergo:0.85)
}
```

### 3. Navigate with Markov Walker

```csharp
using GA.Business.Core.Atonal.Grothendieck;

var walker = serviceProvider.GetRequiredService<MarkovWalker>();

// Start from a shape
var startShape = graph.Shapes.Values.First();

// Walk options
var walkOptions = new WalkOptions
{
    Steps = 10,
    Temperature = 1.0,        // 1.0 = balanced, < 1.0 = greedy, > 1.0 = exploratory
    BoxPreference = true,     // Prefer box shapes
    MaxSpan = 5,              // Max 5-fret span
    MaxShift = 3.0            // Max position shift cost
};

// Generate walk
var path = walker.GenerateWalk(graph, startShape, walkOptions);

Console.WriteLine("Generated path:");
foreach (var shape in path)
{
    Console.WriteLine($"  {shape}");
}
```

### 4. Generate Heat Map

```csharp
// Generate heat map for next positions
var heatMap = walker.GenerateHeatMap(graph, currentShape, walkOptions);

// heatMap is a 6 strings × 24 frets grid
// Values are 0-1 (normalized probabilities)

Console.WriteLine("Heat map:");
for (int str = 0; str < 6; str++)
{
    Console.Write($"String {str + 1}: ");
    for (int fret = 0; fret < 12; fret++)
    {
        var prob = heatMap[str, fret];
        if (prob > 0.1)
        {
            Console.Write($"F{fret}:{prob:F2} ");
        }
    }
    Console.WriteLine();
}
```

### 5. Generate Practice Path

```csharp
// Generate practice path (gradual difficulty)
var practicePath = walker.GeneratePracticePath(graph, startShape, new WalkOptions
{
    Steps = 20,
    Temperature = 0.8,  // Slightly greedy
    MaxSpan = 5
});

Console.WriteLine("Practice path (gradual difficulty):");
for (int i = 0; i < practicePath.Count; i++)
{
    var shape = practicePath[i];
    Console.WriteLine($"{i + 1}. {shape}");
}
```

## Shape Classification

Shapes are automatically tagged based on their properties:

### Shape Type

- **box**: Diagness < 0.3 (compact, vertical shapes)
- **diagonal**: Diagness > 0.7 (spread across strings)
- **mixed**: 0.3 ≤ Diagness ≤ 0.7

### Chord Type

- **open**: Contains open strings
- **barre**: No open strings (movable)

### Difficulty

- **easy**: Span < 2 frets
- **medium**: 2 ≤ Span ≤ 4 frets
- **hard**: Span > 4 frets

## Algorithms

### Diagness Computation

Measures how diagonal a shape is:

```csharp
diagness = average_fret_change_per_string_change / 4.0
```

- 0 = box shape (no fret change between strings)
- 1 = diagonal shape (4+ fret change between strings)

### Ergonomics Computation

Combines multiple factors:

```csharp
ergonomics = (span_penalty + fret_penalty + stretch_penalty) / 3.0
```

- **Span penalty**: Penalizes large fret spans (> 5 frets)
- **Fret penalty**: Penalizes high fret positions (> 12th fret)
- **Stretch penalty**: Penalizes large stretches between adjacent strings (> 4 frets)

### Physical Cost Computation

Measures difficulty of transitioning between shapes:

```csharp
physical_cost = shift_cost + span_cost + pattern_cost + diagness_cost
```

- **Shift cost**: Position shift × 0.5
- **Span cost**: Span change × 0.3
- **Pattern cost**: String pattern change × 0.4
- **Diagness cost**: Diagness change × 0.6

### Softmax Probabilities

Converts transition scores to probabilities:

```csharp
P(transition) = exp(-score / temperature) / Σ exp(-score / temperature)
```

- **Temperature = 1.0**: Balanced exploration
- **Temperature < 1.0**: Greedy (prefer low-cost transitions)
- **Temperature > 1.0**: Exploratory (more random)

## Performance

- **Shape generation**: ~10ms per pitch-class set
- **Graph construction**: ~5s for 100 pitch-class sets (2000+ shapes)
- **Heat map generation**: ~50ms
- **Practice path generation**: ~100ms

## Integration

### With Grothendieck Service

The shape graph uses the Grothendieck service for harmonic analysis:

```csharp
var delta = grothendieckService.ComputeDelta(fromShape.ICV, toShape.ICV);
var harmonicCost = grothendieckService.ComputeHarmonicCost(delta);
```

### With Existing Fretboard System

Shapes use existing fretboard primitives:

- `PositionLocation` (string, fret pairs)
- `Tuning` (pitch per string)
- `PitchClassSet` (pitch-class content)

## Future Enhancements

- [ ] MongoDB storage for shape graphs
- [ ] Caching for frequently used graphs
- [ ] More sophisticated shape generation (barre chords, partial chords)
- [ ] Fingering optimization (which finger on which string)
- [ ] Voice leading analysis
- [ ] CAGED system integration
- [ ] Pattern recognition (common shapes, arpeggios)
- [ ] Personalization via reinforcement learning

## See Also

- [Grothendieck README](../../Atonal/Grothendieck/README.md) - Harmonic analysis
- [Implementation Plan](../../../../docs/IMPLEMENTATION_PLAN_BLENDER_GROTHENDIECK.md) - Full plan
- [Implementation Status](../../../../docs/IMPLEMENTATION_STATUS.md) - Current progress

