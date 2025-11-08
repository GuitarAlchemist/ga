# Guitar Alchemist - Advanced Harmonic Analysis Applications

This directory contains high-level application classes that integrate multiple advanced mathematical techniques for
practical musical analysis and optimization.

---

## 🎯 Overview

The applications in this directory combine:

- **Spectral Graph Theory** - Chord families, centrality, connectivity
- **Information Theory** - Complexity, entropy, predictability
- **Dynamical Systems** - Attractors, stability, chaos
- **Topological Data Analysis** - Multi-scale features
- **Differential Geometry** - Voice leading optimization
- **Optimal Transport** - Distribution comparison

---

## 📦 Applications

### 1. HarmonicAnalysisEngine

**Purpose**: Comprehensive harmonic analysis combining all mathematical techniques.

**Key Features**:

- Full graph analysis with parallel execution
- Progression analysis and comparison
- Optimal practice path generation
- Configurable analysis options

**Example Usage**:

```csharp
using GA.Business.Core.Fretboard.Shapes.Applications;

var engine = new HarmonicAnalysisEngine(loggerFactory);

// Comprehensive analysis
var report = await engine.AnalyzeAsync(graph, new HarmonicAnalysisOptions
{
    IncludeSpectralAnalysis = true,
    IncludeDynamicalAnalysis = true,
    IncludeTopologicalAnalysis = true,
    ClusterCount = 5,
    TopCentralShapes = 10
});

// Results
Console.WriteLine($"Graph size: {report.GraphSize}");
Console.WriteLine($"Connectivity: {report.Spectral.AlgebraicConnectivity:F2}");
Console.WriteLine($"Chord families: {report.ChordFamilies.Count}");
Console.WriteLine($"Attractors: {report.Dynamics.Attractors.Count}");

// Analyze a progression
var progression = new[] { "shape1", "shape2", "shape3", "shape4" };
var progReport = engine.AnalyzeProgression(graph, progression);

Console.WriteLine($"Entropy: {progReport.Entropy:F2} bits");
Console.WriteLine($"Complexity: {progReport.Complexity:F2}");
Console.WriteLine($"Diversity: {progReport.Diversity:F2}");

// Compare two progressions
var comparison = engine.CompareProgressions(graph, prog1, prog2);
Console.WriteLine($"Similarity: {comparison.Similarity:F2}");

// Find optimal practice path
var path = engine.FindOptimalPracticePath(
    graph, 
    startShapeId, 
    pathLength: 8, 
    PracticeGoal.MaximizeInformationGain
);
```

**Analysis Options**:

```csharp
public sealed record HarmonicAnalysisOptions
{
    public bool IncludeSpectralAnalysis { get; init; } = true;
    public bool IncludeDynamicalAnalysis { get; init; } = true;
    public bool IncludeTopologicalAnalysis { get; init; } = true;
    public int ClusterCount { get; init; } = 5;
    public int TopCentralShapes { get; init; } = 10;
    public int TopBottlenecks { get; init; } = 5;
    public double TopologyMaxEpsilon { get; init; } = 10.0;
    public int TopologySteps { get; init; } = 20;
}
```

**Report Structure**:

```csharp
public sealed record HarmonicAnalysisReport
{
    public int GraphSize { get; init; }
    public SpectralMetrics? Spectral { get; init; }
    public IReadOnlyList<ChordFamily> ChordFamilies { get; init; }
    public IReadOnlyList<(string ShapeId, double Centrality)> CentralShapes { get; init; }
    public IReadOnlyList<(string ShapeId, double Bottleneck)> Bottlenecks { get; init; }
    public DynamicalSystemInfo? Dynamics { get; init; }
    public PersistenceDiagram? Topology { get; init; }
    public DateTime AnalysisTimestamp { get; init; }
}
```

---

### 2. ProgressionOptimizer

**Purpose**: Generate and optimize chord progressions for practice and composition.

**Optimization Strategies**:

1. **MaximizeInformationGain** - Maximize learning efficiency
2. **MinimizeVoiceLeading** - Smoothest voice transitions
3. **ExploreFamilies** - Visit different chord families
4. **FollowAttractors** - Stay in stable harmonic regions
5. **Balanced** - Combine all strategies

**Example Usage**:

```csharp
using GA.Business.Core.Fretboard.Shapes.Applications;

var optimizer = new ProgressionOptimizer(loggerFactory);

// Generate optimal practice progression
var result = optimizer.GeneratePracticeProgression(graph, new ProgressionConstraints
{
    TargetLength = 8,
    Strategy = OptimizationStrategy.Balanced,
    PreferCentralShapes = true,
    AllowRandomness = true,
    MinErgonomics = 0.6
});

Console.WriteLine($"Generated: {string.Join(" -> ", result.ShapeIds)}");
Console.WriteLine($"Quality: {result.Quality:F2}");
Console.WriteLine($"Entropy: {result.Entropy:F2}");
Console.WriteLine($"Diversity: {result.Diversity:F2}");

// Improve existing progression
var improved = optimizer.ImproveProgression(
    graph, 
    existingProgression, 
    ImprovementGoal.SmoothVoiceLeading
);
```

**Constraints**:

```csharp
public sealed record ProgressionConstraints
{
    public int TargetLength { get; init; } = 8;
    public string? StartShapeId { get; init; }
    public OptimizationStrategy Strategy { get; init; } = OptimizationStrategy.Balanced;
    public bool PreferCentralShapes { get; init; } = true;
    public bool PreferAttractors { get; init; } = false;
    public bool AllowRandomness { get; init; } = true;
    public double MinErgonomics { get; init; } = 0.0;
    public double MaxSpan { get; init; } = double.MaxValue;
}
```

**Result Structure**:

```csharp
public sealed record OptimizedProgression
{
    public List<string> ShapeIds { get; init; }
    public OptimizationStrategy Strategy { get; init; }
    public double Entropy { get; init; }
    public double Complexity { get; init; }
    public double Predictability { get; init; }
    public double Diversity { get; init; }
    public double Quality { get; init; }
    public DateTime Timestamp { get; init; }
}
```

---

## 🎓 Use Cases

### 1. Practice Optimization

Generate optimal practice sequences that maximize learning:

```csharp
var path = engine.FindOptimalPracticePath(
    graph, 
    startShape, 
    pathLength: 10, 
    PracticeGoal.MaximizeInformationGain
);
```

### 2. Chord Family Discovery

Find groups of related chords:

```csharp
var report = await engine.AnalyzeAsync(graph, new HarmonicAnalysisOptions
{
    ClusterCount = 5
});

foreach (var family in report.ChordFamilies)
{
    Console.WriteLine($"Family {family.Id}: {family.Size} shapes");
}
```

### 3. Progression Complexity Analysis

Measure how complex a progression is:

```csharp
var analysis = engine.AnalyzeProgression(graph, progression);
Console.WriteLine($"Complexity: {analysis.Complexity:F2}");
Console.WriteLine($"Predictability: {analysis.Predictability:F2}");
```

### 4. Style Comparison

Compare different musical styles:

```csharp
var jazzProg = new[] { /* jazz progression */ };
var popProg = new[] { /* pop progression */ };
var comparison = engine.CompareProgressions(graph, jazzProg, popProg);
Console.WriteLine($"Style difference: {1 - comparison.Similarity:F2}");
```

### 5. Smooth Voice Leading

Generate progressions with minimal voice movement:

```csharp
var result = optimizer.GeneratePracticeProgression(graph, new ProgressionConstraints
{
    Strategy = OptimizationStrategy.MinimizeVoiceLeading
});
```

### 6. Harmonic Exploration

Explore different chord families systematically:

```csharp
var result = optimizer.GeneratePracticeProgression(graph, new ProgressionConstraints
{
    Strategy = OptimizationStrategy.ExploreFamilies,
    TargetLength = 12
});
```

---

## 📊 Metrics Explained

### Entropy

- **Range**: 0 to log₂(n) bits
- **Meaning**: Average uncertainty/surprise
- **High**: Unpredictable, complex
- **Low**: Predictable, simple

### Complexity

- **Range**: 0 to 1
- **Meaning**: Normalized complexity score
- **High**: Many unique shapes, varied transitions
- **Low**: Repetitive, simple patterns

### Predictability

- **Range**: 0 to 1
- **Meaning**: How predictable the next chord is
- **High**: Very predictable (low conditional entropy)
- **Low**: Unpredictable (high conditional entropy)

### Diversity

- **Range**: 0 to 1
- **Meaning**: Ratio of unique shapes to total length
- **High**: Many different shapes
- **Low**: Repetitive shapes

### Quality

- **Range**: 0 to 1
- **Meaning**: Weighted combination of metrics
- **Components**: Complexity (30%), Diversity (30%), Unpredictability (20%), Completeness (20%)

### Similarity

- **Range**: 0 to 1
- **Meaning**: How similar two progressions are
- **High**: Very similar
- **Low**: Very different

---

## 🔧 Integration

### With Existing Code

```csharp
// Build shape graph (existing code)
var graph = await shapeGraphBuilder.BuildGraphAsync(tuning, pitchClassSets, options);

// Analyze with new tools
var engine = new HarmonicAnalysisEngine(loggerFactory);
var report = await engine.AnalyzeAsync(graph);

// Use results in UI, API, etc.
```

### With Blazor UI

```csharp
@inject ILoggerFactory LoggerFactory

private HarmonicAnalysisEngine _engine;
private HarmonicAnalysisReport? _report;

protected override void OnInitialized()
{
    _engine = new HarmonicAnalysisEngine(LoggerFactory);
}

private async Task AnalyzeGraph()
{
    _report = await _engine.AnalyzeAsync(graph);
    StateHasChanged();
}
```

### With REST API

```csharp
[ApiController]
[Route("api/[controller]")]
public class HarmonicAnalysisController : ControllerBase
{
    private readonly HarmonicAnalysisEngine _engine;
    
    [HttpPost("analyze")]
    public async Task<HarmonicAnalysisReport> Analyze([FromBody] ShapeGraph graph)
    {
        return await _engine.AnalyzeAsync(graph);
    }
}
```

---

## 🧪 Testing

Comprehensive tests are available in:
`Tests/Common/GA.Business.Core.Tests/Fretboard/Shapes/Applications/`

Run tests:

```bash
dotnet test --filter "FullyQualifiedName~Applications"
```

---

## 📚 Further Reading

- **ADVANCED_MATHEMATICS.md** - Overview of all mathematical techniques
- **IMPLEMENTATION_GUIDE.md** - Detailed usage guide
- **COMPLETE_IMPLEMENTATION_SUMMARY.md** - Complete project summary
- Individual module READMEs in subdirectories

---

## 🎸 Musical Context

These applications are designed for:

- **Guitar players** - Optimize practice, discover new voicings
- **Composers** - Generate interesting progressions
- **Music theorists** - Analyze harmonic structures
- **Educators** - Create optimal learning sequences
- **Researchers** - Study computational music theory

---

*For questions or contributions, see the main Guitar Alchemist documentation.*

