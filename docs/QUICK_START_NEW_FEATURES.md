# Quick Start: New Features

## Overview

This guide helps you get started with the two new major features:
1. **3D Asset Management** for BSP DOOM Explorer
2. **Grothendieck Monoid Operations** for harmonic analysis

---

## 3D Asset Management

### What It Does

Manages 3D models (GLB files) for the BSP DOOM Explorer with:
- Asset categorization (Architecture, AlchemyProps, Gems, Jars, Torches, Artifacts, Decorative)
- Metadata tracking (name, poly count, license, source, tags)
- File storage with hash-based deduplication
- Search and retrieval

### Quick Example

```csharp
using GA.Business.Core.Assets;

// Get the service
var assetService = serviceProvider.GetRequiredService<IAssetLibraryService>();

// Import a GLB file
var metadata = await assetService.ImportGlbAsync(
    path: "Assets/ankh.glb",
    metadata: new AssetMetadata
    {
        Id = "", // Auto-generated
        Name = "Ankh Symbol",
        Category = AssetCategory.AlchemyProps,
        License = "CC Attribution",
        Source = "https://sketchfab.com/...",
        Tags = new Dictionary<string, string>
        {
            ["symbol"] = "ankh",
            ["theme"] = "egyptian"
        }
    }
);

Console.WriteLine($"Imported: {metadata.Name} ({metadata.PolyCount} polygons)");

// Get all gems
var gems = await assetService.GetAssetsByCategoryAsync(AssetCategory.Gems);

// Search by tags
var egyptian = await assetService.SearchByTagsAsync(new Dictionary<string, string>
{
    ["theme"] = "egyptian"
});

// Download GLB
var glbData = await assetService.DownloadGlbAsync(metadata.Id);
```

### Asset Categories

- **Architecture**: Pyramids, pillars, obelisks
- **AlchemyProps**: Ankh, Eye of Horus, flasks, scrolls
- **Gems**: Various gem cuts
- **Jars**: Canopic jars, vessels
- **Torches**: Light sources
- **Artifacts**: Scarabs, statues, masks
- **Decorative**: General decoration

### Where to Get Assets

See [3D_ASSET_LINKS.md](3D_ASSET_LINKS.md) for direct download links to free 3D models from:
- Sketchfab (CC Attribution)
- CGTrader (Free models)
- Free3D
- BlenderKit

### Next Steps

1. Download 3D models from asset links
2. Import using `ImportGlbAsync`
3. Tag and categorize
4. Use in BSP DOOM Explorer (frontend integration coming soon)

---

## Grothendieck Monoid Operations

### What It Does

Provides algebraic operations on pitch-class sets:
- Compute interval-class vectors (ICVs)
- Calculate signed deltas between ICVs
- Measure harmonic distance
- Find nearby sets in harmonic space
- Compute shortest paths through harmonic transformations

### Quick Example

```csharp
using GA.Business.Core.Atonal.Grothendieck;

// Get the service
var grothendieck = serviceProvider.GetRequiredService<IGrothendieckService>();

// Compute ICV for C Major scale
var cMajorPCs = new[] { 0, 2, 4, 5, 7, 9, 11 }; // C, D, E, F, G, A, B
var cMajorICV = grothendieck.ComputeICV(cMajorPCs);
Console.WriteLine(cMajorICV); // <2, 5, 4, 3, 6, 1>

// Compute ICV for C Minor scale
var cMinorPCs = new[] { 0, 2, 3, 5, 7, 8, 10 }; // C, D, Eb, F, G, Ab, Bb
var cMinorICV = grothendieck.ComputeICV(cMinorPCs);

// Compute delta (harmonic change)
var delta = grothendieck.ComputeDelta(cMajorICV, cMinorICV);
Console.WriteLine(delta); // <0, -1, +1, +1, -1, 0>
Console.WriteLine(delta.Explain()); 
// "+1 ic3 (minor 3rd), +1 ic4 (major 3rd), -1 ic2 (whole tone), -1 ic5 (perfect 4th) → more chromatic color"

// Compute harmonic cost
var cost = grothendieck.ComputeHarmonicCost(delta);
Console.WriteLine($"Harmonic cost: {cost:F2}"); // L1 norm * 0.6

// Find nearby pitch-class sets
var cMajor = PitchClassSet.Parse("024579B");
var nearby = grothendieck.FindNearby(cMajor, maxDistance: 2);

Console.WriteLine("Nearby sets:");
foreach (var (set, delta, cost) in nearby.Take(5))
{
    Console.WriteLine($"  {set}: {delta.Explain()} (cost: {cost:F2})");
}

// Find shortest harmonic path
var source = PitchClassSet.Parse("024579B"); // C Major
var target = PitchClassSet.Parse("02357AB"); // C Dorian
var path = grothendieck.FindShortestPath(source, target, maxSteps: 5);

Console.WriteLine("Harmonic path:");
foreach (var step in path)
{
    Console.WriteLine($"  {step}");
}
```

### Key Concepts

#### Interval-Class Vector (ICV)

A 6-dimensional vector counting intervals:
- **ic1**: Minor 2nd (semitone)
- **ic2**: Major 2nd (whole tone)
- **ic3**: Minor 3rd
- **ic4**: Major 3rd
- **ic5**: Perfect 4th
- **ic6**: Tritone

Example: C Major = <2, 5, 4, 3, 6, 1>

#### Grothendieck Delta

Signed difference between ICVs showing harmonic change:

```
C Major: <2, 5, 4, 3, 6, 1>
C Minor: <2, 4, 5, 4, 5, 1>
Delta:   <0, -1, +1, +1, -1, 0>
```

Interpretation: "Lose 1 whole tone, gain 1 minor 3rd and 1 major 3rd, lose 1 perfect 4th"

#### Harmonic Cost

L1 norm (Manhattan distance) of the delta:
- 0 = identical
- 1-2 = very close (modal interchange)
- 3-5 = moderate distance
- 6+ = distant

### Use Cases

#### 1. Analyze Chord Progressions

```csharp
var chords = new[] 
{ 
    PitchClassSet.Parse("047"),  // C Major
    PitchClassSet.Parse("05A"),  // F Major
    PitchClassSet.Parse("027"),  // G Major
    PitchClassSet.Parse("047")   // C Major
};

for (int i = 0; i < chords.Length - 1; i++)
{
    var delta = grothendieck.ComputeDelta(
        chords[i].IntervalClassVector, 
        chords[i+1].IntervalClassVector
    );
    Console.WriteLine($"{chords[i]} → {chords[i+1]}: {delta.Explain()}");
}
```

#### 2. Find Similar Scales

```csharp
var dorian = PitchClassSet.Parse("02357AB");
var similar = grothendieck.FindNearby(dorian, maxDistance: 3)
    .OrderBy(r => r.Cost)
    .Take(10);

Console.WriteLine("Scales similar to Dorian:");
foreach (var (set, delta, cost) in similar)
{
    Console.WriteLine($"  {set}: {delta.Explain()} (cost: {cost:F2})");
}
```

#### 3. Plan Smooth Modulations

```csharp
var currentKey = PitchClassSet.Parse("024579B"); // C Major
var nearby = grothendieck.FindNearby(currentKey, maxDistance: 2);
var smoothestModulation = nearby.First(); // Lowest cost

Console.WriteLine($"Smoothest modulation: {smoothestModulation.Set}");
Console.WriteLine($"Change: {smoothestModulation.Delta.Explain()}");
```

### Next Steps

1. Explore existing pitch-class sets: `PitchClassSet.Items`
2. Compute ICVs for your favorite scales/chords
3. Analyze harmonic relationships
4. Use in fretboard navigation (coming soon)

---

## Integration with Existing Code

### Asset Management

The asset management system integrates with:
- **MongoDB**: `GA.Data.MongoDB.Models.AssetDocument`
- **BSP DOOM Explorer**: Frontend integration (coming soon)
- **ScenesService**: GLB scene builder

### Grothendieck Operations

The Grothendieck module integrates with:
- **Existing Atonal Types**: `IntervalClassVector`, `PitchClassSet`, `SetClass`
- **Fretboard System**: Shape graph builder (coming soon)
- **API Endpoints**: REST API (coming soon)

---

## Documentation

### Comprehensive Guides
- [Implementation Plan](IMPLEMENTATION_PLAN_BLENDER_GROTHENDIECK.md) - Full implementation details
- [Implementation Status](IMPLEMENTATION_STATUS.md) - Current progress
- [3D Asset Links](3D_ASSET_LINKS.md) - Free 3D model resources

### API Documentation
- [Asset Management README](../Common/GA.Business.Core/Assets/README.md)
- [Grothendieck README](../Common/GA.Business.Core/Atonal/Grothendieck/README.md)

### Theory
- [Grothendieck Group (Wikipedia)](https://en.wikipedia.org/wiki/Grothendieck_group)
- [Interval-Class Vector (Music Theory)](https://musictheory.pugetsound.edu/mt21c/IntervalVector.html)
- [Harmonious App - Equivalence Groups](https://harmoniousapp.net/p/ec/Equivalence-Groups)

---

## What's Next?

### Coming Soon

#### Asset Management
- [ ] MongoDB persistence
- [ ] GridFS integration for large files
- [ ] GLB optimization
- [ ] Thumbnail generation
- [ ] Asset browser UI
- [ ] BSP DOOM Explorer integration

#### Grothendieck Operations
- [ ] Shape graph builder (fretboard shapes)
- [ ] Markov walker (probabilistic navigation)
- [ ] Heat map generation (fretboard probability)
- [ ] Practice path generator
- [ ] REST API endpoints
- [ ] React components (FretboardHeatMap)

### How to Contribute

1. **Download 3D Assets**: Help populate the asset library
2. **Test Grothendieck Operations**: Try different scales/chords
3. **Report Issues**: File bugs or feature requests
4. **Write Tests**: Add unit/integration tests
5. **Improve Documentation**: Add examples and tutorials

---

## Questions?

- Check the [Developer Guide](DEVELOPER_GUIDE.md)
- Review the [Implementation Plan](IMPLEMENTATION_PLAN_BLENDER_GROTHENDIECK.md)
- See [Implementation Status](IMPLEMENTATION_STATUS.md) for current progress

