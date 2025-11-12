# VoicingGenerator Usage Examples

## Overview

The `VoicingGenerator` uses `IAsyncEnumerable<Voicing>` for elegant streaming of voicings with channels under the hood.

## Basic Usage

### 1. Collect all voicings into a list (backward compatible)

```csharp
var voicings = VoicingGenerator.GenerateAllVoicings(
    fretboard,
    windowSize: 4,
    minPlayedNotes: 2,
    parallel: true);

Console.WriteLine($"Generated {voicings.Count} voicings");
```

### 2. Stream voicings with async enumerable (memory efficient)

```csharp
await foreach (var voicing in VoicingGenerator.GenerateAllVoicingsAsync(fretboard))
{
    Console.WriteLine(voicing.GetPositionDiagram());
    // Process each voicing as it's generated
}
```

### 3. Stream with LINQ operations

```csharp
var barreChords = VoicingGenerator.GenerateAllVoicingsAsync(fretboard)
    .Where(v => v.HasBarre())
    .Take(100);

await foreach (var voicing in barreChords)
{
    Console.WriteLine($"Barre chord: {voicing.GetPositionDiagram()}");
}
```

### 4. Stream to database (memory efficient for large datasets)

```csharp
var count = 0;
await foreach (var voicing in VoicingGenerator.GenerateAllVoicingsAsync(fretboard))
{
    await db.Voicings.AddAsync(new VoicingEntity
    {
        Diagram = voicing.GetPositionDiagram(),
        FretSpan = voicing.GetFretSpan(),
        NoteCount = voicing.GetPlayedNoteCount()
    });
    
    if (++count % 1000 == 0)
    {
        await db.SaveChangesAsync();
        Console.WriteLine($"Saved {count} voicings...");
    }
}
await db.SaveChangesAsync();
```

### 5. Stream to file

```csharp
using var writer = new StreamWriter("voicings.txt");

await foreach (var voicing in VoicingGenerator.GenerateAllVoicingsAsync(fretboard))
{
    await writer.WriteLineAsync(voicing.GetPositionDiagram());
}
```

### 6. Parallel processing with custom consumer

```csharp
var channel = Channel.CreateUnbounded<Voicing>();

// Producer: Generate voicings
var producerTask = Task.Run(async () =>
{
    await foreach (var voicing in VoicingGenerator.GenerateAllVoicingsAsync(fretboard))
    {
        await channel.Writer.WriteAsync(voicing);
    }
    channel.Writer.Complete();
});

// Consumer: Process voicings in parallel
var consumerTasks = Enumerable.Range(0, 4).Select(async _ =>
{
    await foreach (var voicing in channel.Reader.ReadAllAsync())
    {
        // Process voicing (e.g., analyze, save to DB, etc.)
        await ProcessVoicingAsync(voicing);
    }
}).ToArray();

await Task.WhenAll(consumerTasks);
await producerTask;
```

### 7. Filter and transform

```csharp
var openChords = VoicingGenerator.GenerateAllVoicingsAsync(fretboard)
    .Where(v => v.GetMinFret() == 0)  // Has open strings
    .Where(v => v.GetFretSpan() <= 3) // Max 3-fret span
    .Select(v => new
    {
        Diagram = v.GetPositionDiagram(),
        Span = v.GetFretSpan(),
        Notes = v.Notes
    });

await foreach (var chord in openChords)
{
    Console.WriteLine($"{chord.Diagram} (span: {chord.Span})");
}
```

### 8. Cancellation support

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

try
{
    await foreach (var voicing in VoicingGenerator.GenerateAllVoicingsAsync(
        fretboard,
        cancellationToken: cts.Token))
    {
        Console.WriteLine(voicing.GetPositionDiagram());
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Generation cancelled after 10 seconds");
}
```

## Performance Characteristics

- **Memory**: O(1) - streams voicings instead of collecting all in memory
- **Throughput**: Parallel processing with channels for maximum CPU utilization
- **Deduplication**: Built-in HashSet-based deduplication by position diagram
- **Cancellation**: Full support for `CancellationToken` throughout the pipeline

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                  VoicingGenerator                           │
│                                                             │
│  ┌──────────────┐      ┌──────────────┐                   │
│  │   Producer   │─────▶│   Channel    │                   │
│  │  (Parallel)  │      │  (Unbounded) │                   │
│  └──────────────┘      └──────────────┘                   │
│         │                      │                            │
│         │                      ▼                            │
│         │              ┌──────────────┐                    │
│         │              │ Deduplicator │                    │
│         │              │  (HashSet)   │                    │
│         │              └──────────────┘                    │
│         │                      │                            │
│         │                      ▼                            │
│         │              IAsyncEnumerable<Voicing>           │
│         │                      │                            │
│         └──────────────────────┘                            │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## Benefits of IAsyncEnumerable

1. **Composable**: Use LINQ operators (Where, Select, Take, etc.)
2. **Memory efficient**: Stream data instead of buffering
3. **Cancellable**: Built-in cancellation token support
4. **Idiomatic**: Standard C# async pattern
5. **Flexible**: Easy to integrate with other async streams
6. **Backpressure**: Natural flow control via async iteration

