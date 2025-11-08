# Grothendieck Monoid & Group Operations

## Overview

The Grothendieck module provides algebraic operations on pitch-class sets using the Grothendieck group construction.
This enables:

- Computing signed deltas between interval-class vectors (ICVs)
- Measuring harmonic distance between pitch-class sets
- Finding nearby sets in harmonic space
- Computing shortest paths through harmonic transformations

## Theoretical Foundation

### Grothendieck Monoid

**Monoid Elements**: Pitch-class multisets (mod 12)

- Operation: Multiset union (⊎)
- Identity: Empty set (∅)

**Grothendieck Group**: K₀(M)

- Allows subtraction: "What notes to add/remove to transform A → B?"
- Signed interval-content deltas

**Canonical Invariants**:

- ICV (Interval-Class Vector): φ: M → ℕ⁶
- Extended to G: φ: G → ℤ⁶
- Result: φ(B) - φ(A) = signed interval-content change

### Interval-Class Vector (ICV)

The ICV is a 6-dimensional vector counting intervals:

- **ic1**: Minor 2nd (semitone)
- **ic2**: Major 2nd (whole tone)
- **ic3**: Minor 3rd
- **ic4**: Major 3rd
- **ic5**: Perfect 4th
- **ic6**: Tritone

Example: Major scale = <2, 5, 4, 3, 6, 1>

### Grothendieck Delta

The delta φ(B) - φ(A) is a signed vector showing harmonic change:

```
C Major: <2, 5, 4, 3, 6, 1>
C Minor: <2, 4, 5, 4, 5, 1>
Delta:   <0, -1, +1, +1, -1, 0>
```

Interpretation: "Lose 1 whole tone, gain 1 minor 3rd and 1 major 3rd, lose 1 perfect 4th"

## Usage

### Compute ICV from Pitch Classes

```csharp
var service = serviceProvider.GetRequiredService<IGrothendieckService>();

// C Major scale: C, D, E, F, G, A, B
var pitchClasses = new[] { 0, 2, 4, 5, 7, 9, 11 };
var icv = service.ComputeICV(pitchClasses);

Console.WriteLine(icv); // <2, 5, 4, 3, 6, 1>
```

### Compute Delta Between ICVs

```csharp
var cMajorICV = service.ComputeICV(new[] { 0, 2, 4, 5, 7, 9, 11 });
var cMinorICV = service.ComputeICV(new[] { 0, 2, 3, 5, 7, 8, 10 });

var delta = service.ComputeDelta(cMajorICV, cMinorICV);

Console.WriteLine(delta); // <0, -1, +1, +1, -1, 0>
Console.WriteLine(delta.Explain()); // "+1 ic3 (minor 3rd), +1 ic4 (major 3rd), -1 ic2 (whole tone), -1 ic5 (perfect 4th) → more chromatic color"
```

### Compute Harmonic Cost

```csharp
var cost = service.ComputeHarmonicCost(delta);
Console.WriteLine($"Harmonic cost: {cost}"); // L1 norm * 0.6
```

### Find Nearby Pitch-Class Sets

```csharp
var cMajor = PitchClassSet.Parse("024579B");
var nearby = service.FindNearby(cMajor, maxDistance: 2);

foreach (var (set, delta, cost) in nearby.Take(10))
{
    Console.WriteLine($"{set}: {delta.Explain()} (cost: {cost:F2})");
}
```

### Find Shortest Harmonic Path

```csharp
var source = PitchClassSet.Parse("024579B"); // C Major
var target = PitchClassSet.Parse("02357AB"); // C Dorian

var path = service.FindShortestPath(source, target, maxSteps: 5);

Console.WriteLine("Harmonic path:");
foreach (var step in path)
{
    Console.WriteLine($"  {step}");
}
```

## GrothendieckDelta API

### Properties

```csharp
public sealed record GrothendieckDelta
{
    public int Ic1 { get; init; }  // Change in ic1 (semitone)
    public int Ic2 { get; init; }  // Change in ic2 (whole tone)
    public int Ic3 { get; init; }  // Change in ic3 (minor 3rd)
    public int Ic4 { get; init; }  // Change in ic4 (major 3rd)
    public int Ic5 { get; init; }  // Change in ic5 (perfect 4th)
    public int Ic6 { get; init; }  // Change in ic6 (tritone)
    
    public int L1Norm { get; }     // Manhattan distance
    public double L2Norm { get; }  // Euclidean distance
    public bool IsZero { get; }    // No change
}
```

### Methods

```csharp
// Create from ICVs
var delta = GrothendieckDelta.FromICVs(sourceICV, targetICV);

// Arithmetic
var sum = delta1 + delta2;
var negated = -delta;
var difference = delta1 - delta2;

// Explanation
var explanation = delta.Explain();
// "+1 ic2, -1 ic5 → brighter color"
```

## Musical Interpretation

The delta explanation provides musical context:

- **More chromatic color**: Increased ic1 (semitones) or ic2 (whole tones)
- **Increased tension**: Increased ic6 (tritones)
- **More consonant**: Increased ic3, ic4, or ic5 (3rds and 4ths)
- **Less chromatic**: Decreased ic1 or ic2
- **Reduced tension**: Decreased ic6

## Applications

### 1. Harmonic Distance Metric

Use L1 norm to measure how "far apart" two chords/scales are:

```csharp
var distance = delta.L1Norm;
// 0 = identical
// 1-2 = very close (modal interchange)
// 3-5 = moderate distance
// 6+ = distant
```

### 2. Modulation Planning

Find smooth modulations by minimizing harmonic cost:

```csharp
var nearby = service.FindNearby(currentKey, maxDistance: 2);
var smoothestModulation = nearby.First(); // Lowest cost
```

### 3. Chord Progression Analysis

Analyze harmonic motion in a progression:

```csharp
var chords = new[] { cMajor, fMajor, gMajor, cMajor };
for (int i = 0; i < chords.Length - 1; i++)
{
    var delta = service.ComputeDelta(chords[i].ICV, chords[i+1].ICV);
    Console.WriteLine($"{chords[i]} → {chords[i+1]}: {delta.Explain()}");
}
```

### 4. Scale Similarity

Find scales similar to a given scale:

```csharp
var dorian = PitchClassSet.Parse("02357AB");
var similar = service.FindNearby(dorian, maxDistance: 3)
    .OrderBy(r => r.Cost)
    .Take(10);
```

## Performance

- **ICV Computation**: < 1ms
- **Delta Computation**: < 1ms
- **Find Nearby (4096 sets)**: ~50ms
- **Shortest Path (BFS)**: ~100ms

## Future Enhancements

- [ ] Weighted harmonic costs (context-dependent)
- [ ] Higher-order Markov chains (2-3 step memory)
- [ ] Hidden Markov Model for hand position
- [ ] Semi-Markov for duration modeling
- [ ] Personalization via bandit learning
- [ ] Chaos modulation for organic variation

## References

- [Grothendieck Group (Wikipedia)](https://en.wikipedia.org/wiki/Grothendieck_group)
- [Interval-Class Vector (Music Theory)](https://musictheory.pugetsound.edu/mt21c/IntervalVector.html)
- [Harmonious App - Equivalence Groups](https://harmoniousapp.net/p/ec/Equivalence-Groups)
- [Implementation Plan](../../../../docs/IMPLEMENTATION_PLAN_BLENDER_GROTHENDIECK.md)
- [Implementation Status](../../../../docs/IMPLEMENTATION_STATUS.md)

## See Also

- `IntervalClassVector.cs` - ICV implementation
- `PitchClassSet.cs` - Pitch-class set implementation
- `SetClass.cs` - Set-class (TI-equivalence) implementation

