# ILGPU Integration Guide

## Overview

This project leverages **ILGPU** for cross-platform GPU acceleration of vector search operations. ILGPU provides a high-level abstraction over GPU compute, supporting NVIDIA CUDA, AMD ROCm, and CPU fallback.

**Documentation**: https://ilgpu.net/docs/01-primers/01-setting-up-ilgpu/

## Architecture

### Components

1. **ILGPUKernels.cs** - GPU compute kernels
   - `CosineSimilarityKernel` - Calculate cosine similarity between query and all chord embeddings
   - `FilteredCosineSimilarityKernel` - Filtered similarity calculation
   - `BatchCosineSimilarityKernel` - Batch processing for multiple queries
   - `EuclideanDistanceKernel` - Distance-based similarity

2. **ILGPUVectorSearchStrategy.cs** - Vector search implementation
   - Implements `IVectorSearchStrategy` interface
   - Manages GPU memory and kernel execution
   - Supports semantic search, similar chord finding, and hybrid search

3. **ILGPUContextManager.cs** - Context lifecycle management
   - Singleton service for GPU resource management
   - Automatic CUDA → CPU fallback
   - Memory monitoring and device information

## Setup

### Prerequisites

#### For CUDA Support (NVIDIA GPUs)
```bash
# Install CUDA Toolkit 12.x
# https://developer.nvidia.com/cuda-downloads

# Set CUDA_PATH environment variable
$env:CUDA_PATH = "C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.x"
```

#### For CPU-Only (No GPU)
No additional setup required - ILGPU will automatically use CPU accelerator.

### Installation

ILGPU packages are already included in `GaApi.csproj`:
```xml
<PackageReference Include="ILGPU" Version="1.5.1"/>
<PackageReference Include="ILGPU.Algorithms" Version="1.5.1"/>
```

## Usage

### Dependency Injection Setup

Register services in `Program.cs`:

```csharp
// Register ILGPU context manager
services.AddSingleton<IILGPUContextManager, ILGPUContextManager>();

// Register vector search strategies
services.AddScoped<IVectorSearchStrategy, ILGPUVectorSearchStrategy>();
services.AddScoped<IVectorSearchStrategy, CudaVectorSearchStrategy>();
services.AddScoped<IVectorSearchStrategy, InMemoryVectorSearchStrategy>();
```

### Using ILGPU Vector Search

```csharp
public class ChordSearchService
{
    private readonly IVectorSearchStrategy _vectorSearch;

    public ChordSearchService(IVectorSearchStrategy vectorSearch)
    {
        _vectorSearch = vectorSearch;
    }

    public async Task InitializeAsync(IEnumerable<ChordEmbedding> chords)
    {
        // Initialize GPU memory with chord embeddings
        await _vectorSearch.InitializeAsync(chords);
    }

    public async Task<List<ChordSearchResult>> SearchAsync(double[] queryEmbedding)
    {
        // GPU-accelerated semantic search
        return await _vectorSearch.SemanticSearchAsync(queryEmbedding, limit: 10);
    }

    public async Task<List<ChordSearchResult>> FindSimilarAsync(int chordId)
    {
        // GPU-accelerated similarity search
        return await _vectorSearch.FindSimilarChordsAsync(chordId, limit: 10);
    }

    public async Task<List<ChordSearchResult>> HybridSearchAsync(
        double[] queryEmbedding,
        ChordSearchFilters filters)
    {
        // GPU-accelerated search with CPU-based filtering
        return await _vectorSearch.HybridSearchAsync(queryEmbedding, filters, limit: 10);
    }
}
```

### Monitoring Performance

```csharp
public class VectorSearchMonitor
{
    private readonly IVectorSearchStrategy _vectorSearch;

    public void PrintStats()
    {
        var stats = _vectorSearch.GetStats();
        Console.WriteLine($"Indexed Chords: {stats.IndexedChordCount}");
        Console.WriteLine($"GPU Memory: {stats.GpuMemoryUsageMB}MB");
        Console.WriteLine($"Avg Search Time: {stats.AverageSearchTime.TotalMilliseconds}ms");
        Console.WriteLine($"Total Searches: {stats.TotalSearches}");
    }
}
```

## Performance Characteristics

### Cosine Similarity Kernel

- **Parallelization**: One thread per chord embedding
- **Memory Access**: Coalesced reads for query vector, strided reads for embeddings
- **Computation**: Dot product + norm calculations
- **Expected Speedup**: 10-100x over CPU (depending on GPU and embedding count)

### Filtered Similarity Kernel

- **Parallelization**: One thread per allowed chord
- **Filtering**: CPU-based pre-filtering, GPU-based computation
- **Use Case**: Hybrid search with quality/extension/stacking filters

### Batch Similarity Kernel

- **Parallelization**: 2D grid (queries × chords)
- **Use Case**: Multiple query vectors simultaneously
- **Memory**: Requires more GPU memory but better throughput

## GPU Memory Management

### Memory Allocation

```csharp
// Embeddings: numChords × embeddingDim × sizeof(double)
// Query Vector: embeddingDim × sizeof(double)
// Similarities: numChords × sizeof(double)

// Example: 10,000 chords × 384 dimensions
// Total: 10,000 × 384 × 8 bytes = ~30 MB
```

### Best Practices

1. **Reuse GPU Memory**: Keep embeddings on GPU between searches
2. **Batch Operations**: Process multiple queries together
3. **Monitor Memory**: Check `ILGPUContextManager.AvailableGpuMemoryMB`
4. **Dispose Properly**: Always dispose accelerators and contexts

## Troubleshooting

### CUDA Not Detected

```csharp
// Check available accelerators
var contextManager = serviceProvider.GetRequiredService<IILGPUContextManager>();
var accelerators = contextManager.GetAvailableAccelerators();
foreach (var acc in accelerators)
{
    Console.WriteLine(acc);
}
```

### Fallback to CPU

If CUDA is unavailable, ILGPU automatically uses CPU accelerator:
- No code changes required
- Performance will be similar to CPU-based strategies
- Check logs for accelerator type

### Out of GPU Memory

```csharp
// Reduce batch size or embedding count
// Monitor with:
var contextManager = serviceProvider.GetRequiredService<IILGPUContextManager>();
Console.WriteLine($"Available: {contextManager.AvailableGpuMemoryMB}MB");
```

## Advanced Topics

### Custom Kernels

Add new kernels to `ILGPUKernels.cs`:

```csharp
public static void CustomKernel(
    Index1D index,
    ArrayView<double> input,
    ArrayView<double> output)
{
    if (index >= input.Length)
        return;
    
    output[index] = input[index] * 2.0;
}
```

### Multi-GPU Support

```csharp
var contextManager = serviceProvider.GetRequiredService<IILGPUContextManager>();
var gpu0 = contextManager.CreateAccelerator(0);
var gpu1 = contextManager.CreateAccelerator(1);
```

### Async Kernel Execution

ILGPU kernels execute asynchronously by default:

```csharp
// Kernel launches are non-blocking
_kernel(gridSize, _deviceInput, _deviceOutput);

// Synchronize when needed
_accelerator.Synchronize();
```

## References

- **ILGPU Documentation**: https://ilgpu.net/docs/01-primers/01-setting-up-ilgpu/
- **ILGPU GitHub**: https://github.com/m4rs-mt/ILGPU
- **CUDA Toolkit**: https://developer.nvidia.com/cuda-toolkit
- **ROCm Documentation**: https://rocmdocs.amd.com/

## Performance Tips

1. **Batch Size**: Larger batches = better GPU utilization
2. **Memory Coalescing**: Ensure contiguous memory access patterns
3. **Kernel Fusion**: Combine multiple operations in single kernel
4. **Profiling**: Use NVIDIA Nsight or AMD Rocprof for profiling
5. **Precision**: Use `float` instead of `double` for 2x speedup (if acceptable)

## Future Enhancements

- [ ] Multi-GPU support for distributed search
- [ ] Quantization kernels for reduced memory usage
- [ ] Approximate nearest neighbor (ANN) kernels
- [ ] Tensor operations for advanced music analysis
- [ ] Integration with ONNX Runtime for ML inference

