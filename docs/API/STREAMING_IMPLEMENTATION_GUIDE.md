# Streaming API Implementation Guide

**Date**: 2025-11-01  
**Status**: Ready to Implement  
**.NET Version**: 10.0.100-rc.1 (Already Installed ✅)

---

## Quick Start

### Step 1: Upgrade to .NET 10 (Optional but Recommended)

**Current Status**: .NET 10 RC1 is already installed on this machine!

```bash
# Verify installation
dotnet --list-sdks
# Output: 10.0.100-rc.1.25451.107 [C:\Program Files\dotnet\sdk]
```

**Update global.json** (if you want to use .NET 10):
```json
{
  "sdk": {
    "version": "10.0.100-rc.1.25451.107",
    "rollForward": "latestFeature"
  }
}
```

**Or stay on .NET 9** (streaming works on .NET 9 too):
```json
{
  "sdk": {
    "version": "9.0.306",
    "rollForward": "latestFeature"
  }
}
```

---

## Implementation Patterns

### Pattern 1: Basic Streaming Endpoint

**Service Layer** (Add streaming method):
```csharp
// Common/GA.Business.Core/Chords/IChordService.cs
public interface IChordService
{
    // Existing batch method
    Task<List<Chord>> GetChordsByQualityAsync(string quality, int limit);
    
    // NEW: Streaming method
    IAsyncEnumerable<Chord> GetChordsByQualityStreamAsync(
        string quality, 
        int limit,
        CancellationToken cancellationToken = default);
}
```

**Service Implementation**:
```csharp
// Common/GA.Business.Core/Chords/ChordService.cs
using System.Runtime.CompilerServices;

public async IAsyncEnumerable<Chord> GetChordsByQualityStreamAsync(
    string quality,
    int limit,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    var filter = Builders<Chord>.Filter.Eq(c => c.Quality, quality);
    var cursor = await _collection.Find(filter).Limit(limit).ToCursorAsync(cancellationToken);
    
    while (await cursor.MoveNextAsync(cancellationToken))
    {
        foreach (var chord in cursor.Current)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;
                
            yield return chord;
            
            // Optional: Add backpressure control
            await Task.Delay(1, cancellationToken);
        }
    }
}
```

**Controller Endpoint**:
```csharp
// Apps/ga-server/GaApi/Controllers/ChordsController.cs
using System.Runtime.CompilerServices;

[HttpGet("quality/{quality}/stream")]
[ProducesResponseType(typeof(IAsyncEnumerable<Chord>), StatusCodes.Status200OK)]
public async IAsyncEnumerable<Chord> GetByQualityStream(
    [Required] string quality,
    [FromQuery] [Range(1, 1000)] int limit = 100,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    using var _ = _metrics.TrackRegularRequest();
    
    _logger.LogInformation("Streaming chords by quality: {Quality}, limit: {Limit}", quality, limit);
    
    var count = 0;
    await foreach (var chord in _chordService.GetChordsByQualityStreamAsync(quality, limit, cancellationToken))
    {
        count++;
        yield return chord;
        
        if (count % 10 == 0)
        {
            _logger.LogDebug("Streamed {Count} chords so far", count);
        }
    }
    
    _logger.LogInformation("Completed streaming {Count} chords", count);
}
```

---

### Pattern 2: MongoDB Cursor Streaming

**For MongoDB queries**, use `ToCursorAsync()` for efficient streaming:

```csharp
public async IAsyncEnumerable<Chord> GetChordsByExtensionStreamAsync(
    string extension,
    int limit,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    var filter = Builders<Chord>.Filter.Eq(c => c.Extension, extension);
    
    // Use cursor for efficient streaming
    using var cursor = await _collection
        .Find(filter)
        .Limit(limit)
        .ToCursorAsync(ct);
    
    while (await cursor.MoveNextAsync(ct))
    {
        foreach (var chord in cursor.Current)
        {
            if (ct.IsCancellationRequested) yield break;
            yield return chord;
        }
    }
}
```

---

### Pattern 3: BSP Tree Traversal Streaming

**For tree structures**, use depth-first or breadth-first streaming:

```csharp
public async IAsyncEnumerable<BSPNodeDto> TraverseTreeStreamAsync(
    BSPNode root,
    int maxDepth = 10,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    var stack = new Stack<(BSPNode node, int depth)>();
    stack.Push((root, 0));
    
    while (stack.Count > 0)
    {
        if (ct.IsCancellationRequested) yield break;
        
        var (node, depth) = stack.Pop();
        
        // Yield current node
        yield return new BSPNodeDto
        {
            Id = node.Id,
            Level = depth,
            PitchClasses = node.PitchClasses,
            ChildCount = node.Children.Count
        };
        
        // Add children to stack (if not at max depth)
        if (depth < maxDepth)
        {
            foreach (var child in node.Children.Reverse())
            {
                stack.Push((child, depth + 1));
            }
        }
        
        // Backpressure control
        await Task.Delay(1, ct);
    }
}
```

---

### Pattern 4: Room Generation Streaming

**For long-running generation**, stream results as they're created:

```csharp
public async IAsyncEnumerable<MusicRoom> GenerateRoomsStreamAsync(
    int floor,
    int floorSize,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    var musicData = GetMusicDataForFloor(floor);
    var bspTree = new BSPTree(floorSize, floorSize);
    
    // Generate rooms one at a time
    foreach (var item in musicData.Items)
    {
        if (ct.IsCancellationRequested) yield break;
        
        var room = await GenerateRoomAsync(item, bspTree, ct);
        
        // Yield immediately (don't wait for all rooms)
        yield return room;
        
        // Optional: Persist to MongoDB in background
        _ = Task.Run(() => PersistRoomAsync(room), ct);
    }
}
```

---

## Memory Optimization Patterns

### Pattern 5: Use `ReadOnlySpan<T>` for Array Parameters

**BEFORE** (Allocates array):
```csharp
public IntervalClassVector ComputeICV(int[] pitchClasses)
{
    var sorted = pitchClasses.OrderBy(x => x).ToArray(); // Allocation!
    // ...
}
```

**AFTER** (Zero allocation):
```csharp
public IntervalClassVector ComputeICV(ReadOnlySpan<int> pitchClasses)
{
    // Stack allocation (no heap allocation!)
    Span<int> sorted = stackalloc int[pitchClasses.Length];
    pitchClasses.CopyTo(sorted);
    sorted.Sort();
    
    // ... use sorted span ...
}
```

**API Compatibility**: Keep both versions
```csharp
// Public API (array version for compatibility)
public IntervalClassVector ComputeICV(int[] pitchClasses)
    => ComputeICV(pitchClasses.AsSpan());

// Internal implementation (span version for performance)
internal IntervalClassVector ComputeICV(ReadOnlySpan<int> pitchClasses)
{
    // ... optimized implementation ...
}
```

---

### Pattern 6: Use `ArrayPool<T>` for Temporary Buffers

**BEFORE** (Allocates and discards):
```csharp
private double[][] ConvertHeatMapToArray(double[,] heatMap)
{
    var result = new double[6][];
    for (int s = 0; s < 6; s++)
    {
        result[s] = new double[24]; // Allocation
        for (int f = 0; f < 24; f++)
        {
            result[s][f] = heatMap[s, f];
        }
    }
    return result;
}
```

**AFTER** (Reuses pooled arrays):
```csharp
using System.Buffers;

private double[][] ConvertHeatMapToArray(double[,] heatMap)
{
    var pool = ArrayPool<double>.Shared;
    var result = new double[6][];
    
    for (int s = 0; s < 6; s++)
    {
        // Rent from pool (may be larger than needed)
        var rented = pool.Rent(24);
        result[s] = new double[24];
        
        for (int f = 0; f < 24; f++)
        {
            result[s][f] = heatMap[s, f];
        }
        
        // Return to pool when done
        pool.Return(rented);
    }
    
    return result;
}
```

---

### Pattern 7: Use `ValueTask<T>` for Hot Paths

**BEFORE** (Always allocates Task):
```csharp
public async Task<IntervalClassVector> ComputeICV(int[] pitchClasses)
{
    var cacheKey = string.Join(",", pitchClasses);
    
    if (_cache.TryGetValue(cacheKey, out IntervalClassVector? cached))
        return cached; // Still allocates Task!
    
    // ... compute ...
}
```

**AFTER** (No allocation for cached results):
```csharp
public ValueTask<IntervalClassVector> ComputeICV(int[] pitchClasses)
{
    var cacheKey = string.Join(",", pitchClasses);
    
    if (_cache.TryGetValue(cacheKey, out IntervalClassVector? cached))
        return new ValueTask<IntervalClassVector>(cached); // No allocation!
    
    return ComputeICVAsync(pitchClasses);
}

private async ValueTask<IntervalClassVector> ComputeICVAsync(int[] pitchClasses)
{
    // ... actual async computation ...
}
```

---

## .NET 10 Specific Features

### Feature 1: Tensor Primitives for Vector Operations

**Install Package** (if not already included):
```bash
dotnet add package System.Numerics.Tensors --version 10.0.0-*
```

**Usage**:
```csharp
using System.Numerics.Tensors;

// BEFORE (Manual loop, slow)
public double CosineSimilarity(double[] a, double[] b)
{
    double dot = 0, magA = 0, magB = 0;
    for (int i = 0; i < a.Length; i++)
    {
        dot += a[i] * b[i];
        magA += a[i] * a[i];
        magB += b[i] * b[i];
    }
    return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
}

// AFTER (SIMD-accelerated, 10-20x faster!)
public double CosineSimilarity(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
{
    return TensorPrimitives.CosineSimilarity(a, b);
}

// Other useful operations
public void VectorOperations(ReadOnlySpan<double> a, ReadOnlySpan<double> b, Span<double> result)
{
    TensorPrimitives.Add(a, b, result);           // Element-wise addition
    TensorPrimitives.Multiply(a, b, result);      // Element-wise multiplication
    var dotProduct = TensorPrimitives.Dot(a, b);  // Dot product
    var norm = TensorPrimitives.Norm(a);          // L2 norm
}
```

**Where to Use**:
- `VectorSearchController` - Embedding similarity calculations
- `GrothendieckService` - ICV distance computations
- `SemanticSearchService` - Vector search operations

---

### Feature 2: SearchValues<T> for Fast String Matching

**Usage**:
```csharp
using System.Buffers;

// Define valid values once
private static readonly SearchValues<string> ValidQualities = SearchValues.Create([
    "Major", "Minor", "Dominant", "Augmented", "Diminished",
    "HalfDiminished", "Sus2", "Sus4", "Add9", "Add11"
]);

// Fast validation (optimized by compiler)
public bool IsValidQuality(string quality)
{
    return ValidQualities.Contains(quality);
}
```

---

## Testing Streaming Endpoints

### Test with cURL:
```bash
# Test streaming endpoint
curl -N "https://localhost:7001/api/chords/quality/Major/stream?limit=100"

# Test with cancellation (Ctrl+C to cancel)
curl -N "https://localhost:7001/api/grothendieck/generate-shapes-stream?pitchClasses=047&maxFret=12"
```

### Test with JavaScript:
```javascript
async function consumeStream(url) {
    const response = await fetch(url);
    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    
    while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        
        const chunk = decoder.decode(value, { stream: true });
        const lines = chunk.split('\n');
        
        for (const line of lines) {
            if (line.trim()) {
                const item = JSON.parse(line);
                console.log('Received:', item);
                // Update UI progressively
            }
        }
    }
}

// Usage
consumeStream('https://localhost:7001/api/chords/quality/Major/stream?limit=100');
```

---

## Next Steps

1. **Choose** which endpoints to convert first (see STREAMING_API_COMPREHENSIVE_ANALYSIS.md)
2. **Implement** streaming methods in service layer
3. **Add** streaming endpoints in controllers
4. **Test** with cURL and browser
5. **Measure** performance improvements
6. **Update** frontend to consume streams

**Ready to start implementing?** 🚀

