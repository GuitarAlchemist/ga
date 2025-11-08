# Streaming API Quick Start Guide

## 🚀 Add Your First Streaming Endpoint in 5 Minutes

This guide shows how to add streaming to the Guitar Alchemist API using .NET's `IAsyncEnumerable`.

---

## Step 1: Update ShapeGraphBuilder Interface

Add streaming method to `IShapeGraphBuilder`:

```csharp
// Common/GA.Business.Core/Fretboard/Shapes/IShapeGraphBuilder.cs

public interface IShapeGraphBuilder
{
    // Existing methods...
    Task<ShapeGraph> BuildGraphAsync(...);
    IEnumerable<FretboardShape> GenerateShapes(...);
    
    // NEW: Streaming method
    IAsyncEnumerable<FretboardShape> GenerateShapesStreamAsync(
        Tuning tuning,
        PitchClassSet pitchClassSet,
        ShapeGraphBuildOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

---

## Step 2: Implement Streaming in ShapeGraphBuilder

```csharp
// Common/GA.Business.Core/Fretboard/Shapes/ShapeGraphBuilder.cs

public async IAsyncEnumerable<FretboardShape> GenerateShapesStreamAsync(
    Tuning tuning,
    PitchClassSet pitchClassSet,
    ShapeGraphBuildOptions? options = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    options ??= new ShapeGraphBuildOptions();
    var tuningId = tuning.ToString();
    var targetPitchClasses = pitchClassSet.Select(pc => pc.Value).ToHashSet();
    
    // Try different root positions (starting frets)
    for (int rootFret = 0; rootFret <= options.MaxFret - options.MaxSpan; rootFret++)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            yield break;
        }
        
        // Generate shapes starting at this root fret
        var shapesAtRoot = GenerateShapesAtRoot(
            tuning,
            pitchClassSet,
            targetPitchClasses,
            rootFret,
            options
        );
        
        // Yield each shape as it's generated
        foreach (var shape in shapesAtRoot)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }
            
            yield return shape;
            
            // Optional: Add small delay for backpressure
            await Task.Delay(1, cancellationToken);
        }
    }
}
```

---

## Step 3: Add Streaming Controller Endpoint

```csharp
// Apps/ga-server/GaApi/Controllers/GrothendieckController.cs

/// <summary>
/// Generate fretboard shapes (streaming version)
/// Returns shapes progressively as they're generated
/// </summary>
/// <param name="request">Shape generation parameters</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Stream of fretboard shapes</returns>
[HttpGet("generate-shapes-stream")]
[ProducesResponseType(typeof(IAsyncEnumerable<FretboardShapeResponse>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async IAsyncEnumerable<FretboardShapeResponse> GenerateShapesStream(
    [FromQuery] string tuningId,
    [FromQuery] string pitchClasses,
    [FromQuery] int maxFret = 12,
    [FromQuery] int maxSpan = 5,
    [FromQuery] double minErgonomics = 0.3,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    using var _ = _metrics.TrackRegularRequest();
    
    // Parse parameters
    var tuning = Tuning.Parse(tuningId);
    var pcs = PitchClassSet.Parse(pitchClasses);
    
    var options = new ShapeGraphBuildOptions
    {
        MaxFret = maxFret,
        MaxSpan = maxSpan,
        MinErgonomics = minErgonomics
    };
    
    // Stream shapes as they're generated
    await foreach (var shape in _shapeGraphBuilder.GenerateShapesStreamAsync(tuning, pcs, options, cancellationToken))
    {
        yield return new FretboardShapeResponse
        {
            Id = shape.Id,
            Positions = shape.Positions.Select(p => new PositionResponse
            {
                String = p.Str.Value,
                Fret = p.Fret.Value,
                IsMuted = p.IsMuted
            }).ToArray(),
            MinFret = shape.MinFret,
            MaxFret = shape.MaxFret,
            Span = shape.Span,
            Diagness = shape.Diagness,
            Ergonomics = shape.Ergonomics,
            FingerCount = shape.FingerCount,
            Tags = shape.Tags
        };
    }
}
```

---

## Step 4: Test with curl

```bash
# Stream shapes for C major (0, 4, 7)
curl -N "https://localhost:7001/api/grothendieck/generate-shapes-stream?tuningId=standard&pitchClasses=047&maxFret=12&maxSpan=5"

# Output (NDJSON - Newline Delimited JSON):
{"id":"shape_std_047_0_4","positions":[...],"minFret":0,"maxFret":4,...}
{"id":"shape_std_047_1_5","positions":[...],"minFret":1,"maxFret":5,...}
{"id":"shape_std_047_2_6","positions":[...],"minFret":2,"maxFret":6,...}
...
```

**Note**: Use `-N` flag to disable buffering and see results as they arrive.

---

## Step 5: Consume in React Frontend

### Option A: Fetch API with ReadableStream

```typescript
// Apps/ga-client/src/hooks/useShapesStream.ts

import { useState, useEffect } from 'react';

interface FretboardShape {
    id: string;
    positions: Array<{ string: number; fret: number; isMuted: boolean }>;
    minFret: number;
    maxFret: number;
    span: number;
    diagness: number;
    ergonomics: number;
    fingerCount: number;
    tags: Record<string, string>;
}

export function useShapesStream(tuningId: string, pitchClasses: string) {
    const [shapes, setShapes] = useState<FretboardShape[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<Error | null>(null);
    
    useEffect(() => {
        const controller = new AbortController();
        
        (async () => {
            setLoading(true);
            setError(null);
            setShapes([]);
            
            try {
                const response = await fetch(
                    `/api/grothendieck/generate-shapes-stream?tuningId=${tuningId}&pitchClasses=${pitchClasses}`,
                    { signal: controller.signal }
                );
                
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }
                
                const reader = response.body!.getReader();
                const decoder = new TextDecoder();
                let buffer = '';
                
                while (true) {
                    const { done, value } = await reader.read();
                    
                    if (done) break;
                    
                    buffer += decoder.decode(value, { stream: true });
                    const lines = buffer.split('\n');
                    buffer = lines.pop() || '';
                    
                    for (const line of lines) {
                        if (line.trim()) {
                            const shape = JSON.parse(line) as FretboardShape;
                            setShapes(prev => [...prev, shape]);
                        }
                    }
                }
            } catch (err) {
                if (err instanceof Error && err.name !== 'AbortError') {
                    setError(err);
                }
            } finally {
                setLoading(false);
            }
        })();
        
        return () => controller.abort();
    }, [tuningId, pitchClasses]);
    
    return { shapes, loading, error };
}
```

### Option B: Server-Sent Events (SSE)

```typescript
// Apps/ga-client/src/hooks/useShapesSSE.ts

export function useShapesSSE(tuningId: string, pitchClasses: string) {
    const [shapes, setShapes] = useState<FretboardShape[]>([]);
    
    useEffect(() => {
        const eventSource = new EventSource(
            `/api/grothendieck/generate-shapes-sse?tuningId=${tuningId}&pitchClasses=${pitchClasses}`
        );
        
        eventSource.onmessage = (event) => {
            const shape = JSON.parse(event.data) as FretboardShape;
            setShapes(prev => [...prev, shape]);
        };
        
        eventSource.onerror = () => {
            eventSource.close();
        };
        
        return () => eventSource.close();
    }, [tuningId, pitchClasses]);
    
    return { shapes };
}
```

### Usage in Component

```tsx
// Apps/ga-client/src/components/ShapeExplorer.tsx

import { useShapesStream } from '../hooks/useShapesStream';

export function ShapeExplorer() {
    const { shapes, loading, error } = useShapesStream('standard', '047');
    
    return (
        <div>
            <h2>C Major Shapes</h2>
            
            {loading && <p>Loading shapes... ({shapes.length} so far)</p>}
            {error && <p>Error: {error.message}</p>}
            
            <div className="shapes-grid">
                {shapes.map(shape => (
                    <div key={shape.id} className="shape-card">
                        <h3>{shape.id}</h3>
                        <p>Frets: {shape.minFret}-{shape.maxFret}</p>
                        <p>Ergonomics: {(shape.ergonomics * 100).toFixed(0)}%</p>
                        {/* Render fretboard diagram */}
                    </div>
                ))}
            </div>
        </div>
    );
}
```

---

## Performance Comparison

### Before (Batch API)

```
User clicks "Generate Shapes"
  ↓
[5 seconds of waiting...]
  ↓
All 10,000 shapes appear at once
  ↓
Browser freezes rendering
```

### After (Streaming API)

```
User clicks "Generate Shapes"
  ↓
[0.3 seconds]
  ↓
First 10 shapes appear
  ↓
[0.1 seconds]
  ↓
Next 10 shapes appear
  ↓
... (progressive rendering)
  ↓
All shapes loaded smoothly
```

---

## Advanced: Backpressure Control

Add rate limiting to prevent overwhelming the client:

```csharp
public async IAsyncEnumerable<FretboardShapeResponse> GenerateShapesStream(
    [FromQuery] int batchSize = 10,
    [FromQuery] int delayMs = 100,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    var batch = new List<FretboardShapeResponse>();
    
    await foreach (var shape in _shapeGraphBuilder.GenerateShapesStreamAsync(...))
    {
        batch.Add(MapToResponse(shape));
        
        if (batch.Count >= batchSize)
        {
            foreach (var item in batch)
            {
                yield return item;
            }
            
            batch.Clear();
            await Task.Delay(delayMs, cancellationToken); // Backpressure
        }
    }
    
    // Yield remaining
    foreach (var item in batch)
    {
        yield return item;
    }
}
```

---

## Monitoring and Metrics

Track streaming performance:

```csharp
public async IAsyncEnumerable<FretboardShapeResponse> GenerateShapesStream(...)
{
    var sw = Stopwatch.StartNew();
    var count = 0;
    
    await foreach (var shape in _shapeGraphBuilder.GenerateShapesStreamAsync(...))
    {
        count++;
        yield return MapToResponse(shape);
        
        if (count % 100 == 0)
        {
            _logger.LogInformation(
                "Streamed {Count} shapes in {Elapsed}ms ({Rate} shapes/sec)",
                count,
                sw.ElapsedMilliseconds,
                count / sw.Elapsed.TotalSeconds
            );
        }
    }
    
    _metrics.RecordStreamingRequest(count, sw.Elapsed);
}
```

---

## Next Steps

1. ✅ Implement streaming for shape generation
2. ✅ Add streaming for MongoDB asset queries
3. ✅ Add streaming for BSP tree traversal
4. ✅ Add streaming for Redis vector search
5. ✅ Measure performance improvements
6. ✅ Document patterns for team

---

## See Also

- [STREAMING_API_ANALYSIS.md](STREAMING_API_ANALYSIS.md) - Full analysis and use cases
- [Microsoft Docs - IAsyncEnumerable](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/generate-consume-asynchronous-stream)
- [ASP.NET Core - Streaming](https://learn.microsoft.com/en-us/aspnet/core/web-api/action-return-types#iasyncenumerablet-type)

