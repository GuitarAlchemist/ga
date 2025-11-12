# GPU Kernel Implementation Plan

## Overview

This document outlines the plan to implement actual ILGPU kernels for GPU-accelerated cosine similarity calculations in the voicing search system.

## Current Status

**Current Implementation**: CPU-based computation even when GPU memory is allocated (see line 275-281 in `ILGPUVoicingSearchStrategy.cs`)

```csharp
// CPU-based computation (ILGPU kernel compilation is complex)
// TODO: Implement actual GPU kernel for better performance
var similarities = new double[_voicings.Count];
for (var i = 0; i < _voicings.Count; i++)
{
    similarities[i] = CalculateCosineSimilarity(queryEmbedding, i);
}
```

**Problem**: This defeats the purpose of GPU acceleration. We're allocating GPU memory but not using it.

**Goal**: Implement actual ILGPU kernels to perform cosine similarity calculations on the GPU.

---

## Implementation Plan

### Phase 1: Create Basic GPU Kernel

**Step 1.1**: Define the cosine similarity kernel

```csharp
// Add this static method to ILGPUVoicingSearchStrategy class
private static void CosineSimilarityKernel(
    Index1D index,
    ArrayView<double> queryEmbedding,
    ArrayView2D<double, Stride2D.DenseX> embeddings,
    ArrayView<double> similarities,
    int embeddingDim)
{
    // Each thread computes similarity for one voicing
    var voicingIndex = index;
    
    // Compute dot product
    double dotProduct = 0.0;
    double queryMagnitude = 0.0;
    double embeddingMagnitude = 0.0;
    
    for (int i = 0; i < embeddingDim; i++)
    {
        var queryVal = queryEmbedding[i];
        var embeddingVal = embeddings[voicingIndex, i];
        
        dotProduct += queryVal * embeddingVal;
        queryMagnitude += queryVal * queryVal;
        embeddingMagnitude += embeddingVal * embeddingVal;
    }
    
    // Compute cosine similarity
    var magnitude = XMath.Sqrt(queryMagnitude * embeddingMagnitude);
    similarities[voicingIndex] = magnitude > 0.0 ? dotProduct / magnitude : 0.0;
}
```

**Step 1.2**: Compile and load the kernel

```csharp
// Add this field to the class
private Action<Index1D, ArrayView<double>, ArrayView2D<double, Stride2D.DenseX>, ArrayView<double>, int>? _cosineSimilarityKernel;

// Add this to the Initialize method (after accelerator creation)
private void CompileKernels()
{
    if (_accelerator == null)
        throw new InvalidOperationException("Accelerator not initialized");
    
    _cosineSimilarityKernel = _accelerator.LoadAutoGroupedStreamKernel<
        Index1D,
        ArrayView<double>,
        ArrayView2D<double, Stride2D.DenseX>,
        ArrayView<double>,
        int>(CosineSimilarityKernel);
    
    _logger.LogInformation("GPU kernels compiled successfully");
}
```

**Step 1.3**: Restructure embeddings as 2D array

```csharp
// Change _hostEmbeddings from double[] to double[,]
private double[,]? _hostEmbeddings2D;

// Update PrepareEmbeddings method
private void PrepareEmbeddings(List<VoicingEmbedding> voicings)
{
    var count = voicings.Count;
    _embeddingDimensions = voicings[0].Embedding.Count;
    
    // Create 2D array for GPU
    _hostEmbeddings2D = new double[count, _embeddingDimensions];
    _voicingIds = new string[count];
    
    for (var i = 0; i < count; i++)
    {
        _voicingIds[i] = voicings[i].Id;
        for (var j = 0; j < _embeddingDimensions; j++)
        {
            _hostEmbeddings2D[i, j] = voicings[i].Embedding[j];
        }
    }
    
    _logger.LogInformation("Prepared {Count} embeddings for GPU acceleration", count);
}
```

**Step 1.4**: Update CalculateSimilaritiesILGPU to use the kernel

```csharp
private IEnumerable<(string VoicingId, double Score)> CalculateSimilaritiesILGPU(double[] queryEmbedding)
{
    if (_accelerator == null || _hostEmbeddings2D == null || _voicingIds == null || _cosineSimilarityKernel == null)
        throw new InvalidOperationException("ILGPU not properly initialized");

    if (_isDisposed || _accelerator.AcceleratorType == AcceleratorType.CPU)
    {
        _logger.LogDebug("Using CPU fallback for similarity calculation");
        return CalculateSimilaritiesCPU(queryEmbedding);
    }

    try
    {
        var count = _voicingIds.Length;
        
        // Allocate GPU memory
        using var deviceQueryVector = _accelerator.Allocate1D(queryEmbedding);
        using var deviceEmbeddings = _accelerator.Allocate2DDenseX<double>(
            new Index2D(count, _embeddingDimensions));
        using var deviceSimilarities = _accelerator.Allocate1D<double>(count);
        
        // Copy embeddings to GPU
        deviceEmbeddings.CopyFromCPU(_hostEmbeddings2D);
        
        // Launch kernel
        _cosineSimilarityKernel(
            count,
            deviceQueryVector.View,
            deviceEmbeddings.View,
            deviceSimilarities.View,
            _embeddingDimensions);
        
        // Wait for completion
        _accelerator.Synchronize();
        
        // Copy results back to CPU
        var similarities = deviceSimilarities.GetAsArray1D();
        
        return _voicingIds.Select((id, idx) => (id, similarities[idx]));
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "GPU kernel execution failed, falling back to CPU");
        return CalculateSimilaritiesCPU(queryEmbedding);
    }
}
```

### Phase 2: Optimize Memory Management

**Step 2.1**: Cache GPU embeddings to avoid repeated transfers

```csharp
// Add these fields
private MemoryBuffer2D<double, Stride2D.DenseX>? _cachedDeviceEmbeddings;

// Update Initialize method to cache embeddings
private void CacheEmbeddingsOnGPU()
{
    if (_accelerator == null || _hostEmbeddings2D == null)
        return;
    
    var count = _hostEmbeddings2D.GetLength(0);
    var dim = _hostEmbeddings2D.GetLength(1);
    
    _cachedDeviceEmbeddings = _accelerator.Allocate2DDenseX<double>(new Index2D(count, dim));
    _cachedDeviceEmbeddings.CopyFromCPU(_hostEmbeddings2D);
    
    _logger.LogInformation("Cached {Count} embeddings on GPU ({Size:F2} MB)",
        count,
        (count * dim * sizeof(double)) / (1024.0 * 1024.0));
}

// Update CalculateSimilaritiesILGPU to use cached embeddings
private IEnumerable<(string VoicingId, double Score)> CalculateSimilaritiesILGPU(double[] queryEmbedding)
{
    // ... validation code ...
    
    try
    {
        var count = _voicingIds.Length;
        
        // Allocate GPU memory only for query and results
        using var deviceQueryVector = _accelerator.Allocate1D(queryEmbedding);
        using var deviceSimilarities = _accelerator.Allocate1D<double>(count);
        
        // Use cached embeddings (no transfer needed!)
        _cosineSimilarityKernel(
            count,
            deviceQueryVector.View,
            _cachedDeviceEmbeddings!.View,
            deviceSimilarities.View,
            _embeddingDimensions);
        
        _accelerator.Synchronize();
        var similarities = deviceSimilarities.GetAsArray1D();
        
        return _voicingIds.Select((id, idx) => (id, similarities[idx]));
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "GPU kernel execution failed, falling back to CPU");
        return CalculateSimilaritiesCPU(queryEmbedding);
    }
}
```

**Step 2.2**: Update Dispose method to clean up GPU memory

```csharp
public void Dispose()
{
    if (_isDisposed) return;
    
    _cachedDeviceEmbeddings?.Dispose();
    _accelerator?.Dispose();
    _context?.Dispose();
    
    _isDisposed = true;
    GC.SuppressFinalize(this);
}
```

### Phase 3: Benchmark and Optimize

**Step 3.1**: Add performance metrics

```csharp
// Add fields for metrics
private long _gpuSearches;
private long _cpuFallbacks;
private TimeSpan _totalGpuTime;
private TimeSpan _totalCpuTime;

// Update CalculateSimilaritiesILGPU to track metrics
private IEnumerable<(string VoicingId, double Score)> CalculateSimilaritiesILGPU(double[] queryEmbedding)
{
    var sw = Stopwatch.StartNew();
    
    try
    {
        // ... GPU kernel execution ...
        
        sw.Stop();
        _gpuSearches++;
        _totalGpuTime += sw.Elapsed;
        
        return _voicingIds.Select((id, idx) => (id, similarities[idx]));
    }
    catch (Exception ex)
    {
        sw.Stop();
        _cpuFallbacks++;
        _totalCpuTime += sw.Elapsed;
        
        _logger.LogWarning(ex, "GPU kernel execution failed, falling back to CPU");
        return CalculateSimilaritiesCPU(queryEmbedding);
    }
}

// Add method to get performance stats
public (long GpuSearches, long CpuFallbacks, double AvgGpuTimeMs, double AvgCpuTimeMs) GetPerformanceStats()
{
    return (
        _gpuSearches,
        _cpuFallbacks,
        _gpuSearches > 0 ? _totalGpuTime.TotalMilliseconds / _gpuSearches : 0,
        _cpuFallbacks > 0 ? _totalCpuTime.TotalMilliseconds / _cpuFallbacks : 0
    );
}
```

**Step 3.2**: Create benchmark test

```csharp
// Add to test project
[Test]
public async Task BenchmarkGpuVsCpu()
{
    // Setup
    var strategy = new ILGPUVoicingSearchStrategy(logger, embeddingService);
    await strategy.Initialize(testVoicings);
    
    // Warm up
    for (int i = 0; i < 10; i++)
    {
        await strategy.SearchAsync("test query", 10);
    }
    
    // Benchmark
    var sw = Stopwatch.StartNew();
    for (int i = 0; i < 1000; i++)
    {
        await strategy.SearchAsync("test query", 10);
    }
    sw.Stop();
    
    var stats = strategy.GetPerformanceStats();
    Console.WriteLine($"GPU searches: {stats.GpuSearches}");
    Console.WriteLine($"CPU fallbacks: {stats.CpuFallbacks}");
    Console.WriteLine($"Avg GPU time: {stats.AvgGpuTimeMs:F2}ms");
    Console.WriteLine($"Avg CPU time: {stats.AvgCpuTimeMs:F2}ms");
    Console.WriteLine($"Total time: {sw.ElapsedMilliseconds}ms");
    Console.WriteLine($"Throughput: {1000.0 / sw.Elapsed.TotalSeconds:F2} searches/sec");
}
```

---

## Expected Performance Improvements

Based on typical GPU acceleration patterns:

| Metric | CPU (Current) | GPU (Expected) | Improvement |
|--------|---------------|----------------|-------------|
| Single search | ~8.86ms | ~0.1-1ms | **10-100x faster** |
| Throughput | ~113 searches/sec | ~1,000-10,000 searches/sec | **10-100x faster** |
| Memory usage | 0 MB GPU | ~50-100 MB GPU | Acceptable |

---

## Implementation Checklist

- [ ] Phase 1: Create Basic GPU Kernel
  - [ ] Define CosineSimilarityKernel method
  - [ ] Compile and load kernel
  - [ ] Restructure embeddings as 2D array
  - [ ] Update CalculateSimilaritiesILGPU to use kernel
  - [ ] Test basic functionality

- [ ] Phase 2: Optimize Memory Management
  - [ ] Cache embeddings on GPU
  - [ ] Update Dispose method
  - [ ] Test memory usage

- [ ] Phase 3: Benchmark and Optimize
  - [ ] Add performance metrics
  - [ ] Create benchmark test
  - [ ] Compare GPU vs CPU performance
  - [ ] Document results

---

## Risks and Mitigation

**Risk 1**: ILGPU kernel compilation errors
- **Mitigation**: Start with simple kernel, test incrementally, keep CPU fallback

**Risk 2**: GPU memory limitations
- **Mitigation**: Monitor memory usage, implement batching if needed

**Risk 3**: Performance not as expected
- **Mitigation**: Profile with ILGPU profiler, optimize kernel, consider alternative approaches

---

## Next Steps

1. Implement Phase 1 (Basic GPU Kernel)
2. Test with small dataset
3. Implement Phase 2 (Memory Optimization)
4. Run benchmarks
5. Document results
6. Iterate based on performance data

