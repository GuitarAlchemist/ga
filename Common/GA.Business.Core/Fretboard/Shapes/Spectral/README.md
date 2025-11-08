# Spectral Graph Theory for Guitar Alchemist

## Overview

This module implements **spectral graph theory** for analyzing fretboard shape graphs. Spectral methods use eigenvalues
and eigenvectors of graph matrices to reveal structural properties that are difficult to detect with traditional graph
algorithms.

## Key Concepts

### Graph Laplacian Matrix

The **Laplacian matrix** `L = D - A` where:

- `D` is the degree matrix (diagonal matrix of node degrees)
- `A` is the adjacency matrix (weighted edges between shapes)

**Properties:**

- Symmetric and positive semi-definite
- Smallest eigenvalue λ₁ = 0 (always)
- Number of zero eigenvalues = number of connected components
- Eigenvectors reveal graph structure

### Algebraic Connectivity (λ₂)

The **second smallest eigenvalue** (Fiedler value) measures how well-connected the graph is:

- λ₂ = 0: Graph is disconnected
- λ₂ > 0: Graph is connected (higher = more connected)
- Related to graph expansion, mixing time, and diameter

### Fiedler Vector

The **eigenvector corresponding to λ₂** can partition the graph:

- Sign of components indicates which partition each node belongs to
- Minimizes the normalized cut
- Used for spectral clustering

## Classes

### `SpectralMetrics`

Holds spectral analysis results:

```csharp
var metrics = analyzer.Analyze(graph);

Console.WriteLine($"Algebraic connectivity: {metrics.AlgebraicConnectivity}");
Console.WriteLine($"Spectral gap: {metrics.SpectralGap}");
Console.WriteLine($"Is connected: {metrics.IsConnected}");
Console.WriteLine($"Components: {metrics.EstimatedComponentCount}");

// Get Fiedler partition
var (partition1, partition2) = metrics.GetFiedlerPartition();
```

**Key Properties:**

- `Eigenvalues` - All eigenvalues (sorted ascending)
- `Eigenvectors` - Corresponding eigenvectors
- `AlgebraicConnectivity` - λ₂ (Fiedler value)
- `FiedlerVector` - Eigenvector for λ₂
- `SpectralGap` - λ₂ - λ₁
- `IsConnected` - True if graph is connected
- `EstimatedComponentCount` - Number of connected components

### `LaplacianMatrix`

Computes and stores graph Laplacian matrices:

```csharp
var laplacian = LaplacianMatrix.FromShapeGraph(graph, useWeights: true);

Console.WriteLine($"Nodes: {laplacian.NodeCount}");
Console.WriteLine($"Average degree: {laplacian.AverageDegree()}");
Console.WriteLine($"Is regular: {laplacian.IsRegular()}");

// Access matrices
var L = laplacian.L;              // Unnormalized Laplacian
var LSym = laplacian.LSymmetric;  // Symmetric normalized
var LRw = laplacian.LRandomWalk;  // Random walk normalized
```

**Variants:**

- **Unnormalized**: `L = D - A`
- **Symmetric normalized**: `L_sym = D^(-1/2) · L · D^(-1/2)`
- **Random walk normalized**: `L_rw = D^(-1) · L`

### `SpectralGraphAnalyzer`

Main analysis class with various spectral methods:

```csharp
var analyzer = new SpectralGraphAnalyzer(logger);

// Basic spectral analysis
var metrics = analyzer.Analyze(graph, useWeights: true, normalized: true);

// Find most central shapes (eigenvector centrality)
var centralShapes = analyzer.FindCentralShapes(graph, topK: 10);
foreach (var (shapeId, centrality) in centralShapes)
{
    Console.WriteLine($"{shapeId}: {centrality:F4}");
}

// Compute PageRank importance scores
var pageRank = analyzer.ComputePageRank(graph, dampingFactor: 0.85);

// Find bottleneck shapes (bridge chords)
var bottlenecks = analyzer.FindBottlenecks(graph, topK: 5);

// Estimate graph properties
var mixingTime = analyzer.EstimateMixingTime(metrics);
var diameter = analyzer.EstimateDiameter(metrics);
```

### `SpectralClustering`

Cluster shapes using spectral methods:

```csharp
var clustering = new SpectralClustering(logger, seed: 42);

// Cluster shapes into k groups
var assignments = clustering.Cluster(graph, k: 5);

// Get cluster statistics
var stats = clustering.GetClusterStats(graph, assignments);
foreach (var stat in stats.Values)
{
    Console.WriteLine(stat);
    // Output: Cluster 0: 15 shapes, avgErgo=0.75, avgDiag=0.42, avgSpan=4.2
}
```

## Musical Applications

### 1. Find Chord Families

Group similar chord shapes into families:

```csharp
var clusters = clustering.Cluster(graph, k: 8);
var stats = clustering.GetClusterStats(graph, clusters);

// Each cluster represents a chord family with similar characteristics
foreach (var (clusterId, stat) in stats)
{
    Console.WriteLine($"Family {clusterId}: {stat.Size} shapes");
    Console.WriteLine($"  Ergonomics: {stat.AvgErgonomics:F2}");
    Console.WriteLine($"  Diagness: {stat.AvgDiagness:F2}");
}
```

### 2. Identify Bridge Chords

Find shapes that connect different harmonic regions:

```csharp
var bottlenecks = analyzer.FindBottlenecks(graph, topK: 10);

// These shapes are important for modulation and transitions
foreach (var (shapeId, score) in bottlenecks)
{
    var shape = graph.Shapes[shapeId];
    Console.WriteLine($"{shapeId}: {shape.PitchClassSet} (bottleneck={score:F2})");
}
```

### 3. Generate Practice Paths

Use PageRank to prioritize important shapes:

```csharp
var pageRank = analyzer.ComputePageRank(graph);
var topShapes = pageRank
    .OrderByDescending(kvp => kvp.Value)
    .Take(20)
    .ToList();

// Practice these shapes first - they're most "central" to the harmonic network
```

### 4. Measure Harmonic Connectivity

Analyze how well-connected different harmonic regions are:

```csharp
var metrics = analyzer.Analyze(graph);

Console.WriteLine($"Algebraic connectivity: {metrics.AlgebraicConnectivity:F4}");
// Higher = easier to navigate between any two chords

Console.WriteLine($"Mixing time: {analyzer.EstimateMixingTime(metrics):F2}");
// How many steps to reach any chord from any other chord

Console.WriteLine($"Diameter: {analyzer.EstimateDiameter(metrics):F2}");
// Maximum distance between any two chords
```

### 5. Detect Harmonic Communities

Use Fiedler vector to partition the harmonic space:

```csharp
var metrics = analyzer.Analyze(graph);
var (partition1, partition2) = metrics.GetFiedlerPartition();

// Two main harmonic regions
Console.WriteLine($"Region 1: {partition1.Length} shapes");
Console.WriteLine($"Region 2: {partition2.Length} shapes");

// Shapes near the boundary (Fiedler vector ≈ 0) are bridge chords
```

## Theory References

### Books

- **Chung, F. R. K.** (1997). *Spectral Graph Theory*. American Mathematical Society.
- **Spielman, D.** (2019). *Spectral and Algebraic Graph Theory*. Yale University.

### Papers

- **Fiedler, M.** (1973). "Algebraic connectivity of graphs." *Czechoslovak Mathematical Journal*.
- **Ng, A., Jordan, M., & Weiss, Y.** (2002). "On spectral clustering: Analysis and an algorithm." *NIPS*.
- **Von Luxburg, U.** (2007). "A tutorial on spectral clustering." *Statistics and Computing*.

### Music Theory Applications

- **Tymoczko, D.** (2011). *A Geometry of Music*. Oxford University Press.
- **Callender, C., Quinn, I., & Tymoczko, D.** (2008). "Generalized voice-leading spaces." *Science*.

## Performance Considerations

- **Eigendecomposition**: O(n³) for dense matrices, O(n²) for sparse
- **Memory**: O(n²) for storing matrices
- **Recommended**: Use for graphs with < 10,000 nodes
- **Optimization**: Use sparse matrix representations for large graphs

## Future Enhancements

- [ ] Sparse matrix support for large graphs
- [ ] Incremental eigenvalue updates
- [ ] Multi-scale spectral analysis
- [ ] Spectral graph drawing (visualization)
- [ ] Cheeger cut computation
- [ ] Heat kernel methods
- [ ] Graph wavelets

