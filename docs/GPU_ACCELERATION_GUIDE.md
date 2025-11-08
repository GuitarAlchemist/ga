# GPU Acceleration Guide - Guitar Alchemist

## 🚀 **MAXIMUM PERFORMANCE WITH GPU ACCELERATION**

This guide shows how to leverage GPU power for best possible performance in the Guitar Alchemist application.

---

## 📊 **Current GPU Infrastructure**

### ✅ **Already Implemented**

1. **CUDA Kernels** (`Apps/ga-server/GaApi/CUDA/kernels/cosine_similarity.cu`)
   - Optimized cosine similarity calculations
   - Shared memory optimization
   - Warp-level primitives
   - Filtered search support

2. **CUDA Vector Search Strategy** (`Apps/ga-server/GaApi/Services/CudaVectorSearchStrategy.cs`)
   - GPU-accelerated semantic search
   - ~1ms search time (vs ~100ms CPU)
   - Handles 400,000+ chords efficiently

3. **WebGPU Frontend** (React Components)
   - `Guitar3D.tsx` - WebGPU renderer with 8x MSAA
   - `MinimalThreeInstrument.tsx` - WebGPU support
   - `WebGPUFretboard/renderer.ts` - Pixi.js v8 WebGPU

4. **SIMD Optimizations** (Partial)
   - `GrothendieckDelta.cs` - L2 norm computation (ready for TensorPrimitives)
   - Documentation references TensorPrimitives usage

---

## 🎯 **GPU Acceleration Opportunities**

### **1. Vector Operations (HIGHEST IMPACT)**

#### **Current State**: CPU-based loops
#### **Target**: GPU-accelerated with CUDA/TensorPrimitives
#### **Expected Speedup**: **10-100x**

**Operations to Accelerate**:
- ✅ Cosine similarity (already has CUDA kernel)
- ⚠️ ICV computations (Grothendieck Service)
- ⚠️ Delta operations (Grothendieck Monoid)
- ⚠️ Heat map generation (Markov Walker)
- ⚠️ Shape graph building (batch operations)

---

### **2. Batch Processing (HIGH IMPACT)**

#### **Current State**: Sequential processing
#### **Target**: Parallel GPU batch processing
#### **Expected Speedup**: **50-200x**

**Operations to Batch**:
- Pitch class set indexing (400,000+ sets)
- Fretboard shape generation (millions of voicings)
- BSP tree traversal (massive hierarchies)
- Embedding generation (semantic search)

---

### **3. Matrix Operations (MEDIUM IMPACT)**

#### **Current State**: CPU-based
#### **Target**: cuBLAS/TensorPrimitives
#### **Expected Speedup**: **5-20x**

**Operations**:
- Heat map probability matrices
- Transition probability matrices (Markov chains)
- Graph adjacency matrices

---

## 🛠️ **Implementation Plan**

### **Phase 1: Enable CUDA Vector Search (IMMEDIATE)**

The CUDA infrastructure exists but needs activation:

```csharp
// In Startup.cs / Program.cs
services.AddSingleton<IVectorSearchStrategy, CudaVectorSearchStrategy>();
```

**Requirements**:
- NVIDIA GPU with CUDA Compute Capability 3.5+
- CUDA Toolkit 12.0+ installed
- Compile CUDA kernels: `nvcc -ptx cosine_similarity.cu`

**Expected Performance**:
- Search time: **1-2ms** (vs 100ms CPU)
- Throughput: **500-1000 searches/sec**
- Memory: GPU VRAM (efficient)

---

### **Phase 2: TensorPrimitives for SIMD (.NET 9 Compatible)**

Replace manual loops with hardware-accelerated operations:

```csharp
// BEFORE (Manual loop - SLOW)
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

// AFTER (SIMD-accelerated - 10-20x FASTER)
using System.Numerics.Tensors;

public double CosineSimilarity(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
{
    return TensorPrimitives.CosineSimilarity(a, b);
}
```

**Apply to**:
- `GrothendieckService.cs` - ICV operations
- `GrothendieckDelta.cs` - L2 norm (already prepared!)
- `MarkovWalker.cs` - Probability calculations
- `ShapeGraphBuilder.cs` - Distance calculations

**Package**: `System.Numerics.Tensors` (already referenced in CUDA project)

---

### **Phase 3: ILGPU for Cross-Platform GPU Compute**

For non-NVIDIA GPUs (AMD, Intel, Apple Silicon):

```csharp
using ILGPU;
using ILGPU.Runtime;

public class IlgpuVectorSearchStrategy : IVectorSearchStrategy
{
    private Context _context;
    private Accelerator _accelerator;
    
    public void Initialize()
    {
        _context = Context.CreateDefault();
        _accelerator = _context.GetPreferredDevice(preferCPU: false)
            .CreateAccelerator(_context);
    }
    
    // GPU kernel for cosine similarity
    static void CosineSimilarityKernel(
        Index1D index,
        ArrayView<double> query,
        ArrayView2D<double, Stride2D.DenseX> embeddings,
        ArrayView<double> results)
    {
        var chordIdx = index.X;
        double dot = 0, queryNorm = 0, embeddingNorm = 0;
        
        for (int i = 0; i < query.Length; i++)
        {
            var q = query[i];
            var e = embeddings[chordIdx, i];
            dot += q * e;
            queryNorm += q * q;
            embeddingNorm += e * e;
        }
        
        results[chordIdx] = dot / (Math.Sqrt(queryNorm) * Math.Sqrt(embeddingNorm));
    }
}
```

**Benefits**:
- Works on AMD, Intel, Apple GPUs
- Cross-platform (Windows, Linux, macOS)
- JIT compilation for optimal performance

**Package**: `ILGPU` (NuGet)

---

### **Phase 4: GPU-Accelerated Shape Graph Building**

Parallelize the most expensive operation:

```csharp
public class GpuShapeGraphBuilder : IShapeGraphBuilder
{
    private readonly Accelerator _gpu;
    
    public async Task<ShapeGraph> BuildGraphAsync(
        Tuning tuning,
        IEnumerable<PitchClassSet> pitchClassSets,
        ShapeGraphOptions options)
    {
        var sets = pitchClassSets.ToArray();
        var numSets = sets.Length;
        
        // Allocate GPU memory
        using var gpuIcvs = _gpu.Allocate2DDenseX<double>(new Index2D(numSets, 6));
        using var gpuDistances = _gpu.Allocate2DDenseX<double>(new Index2D(numSets, numSets));
        
        // Copy ICVs to GPU
        var icvs = sets.Select(s => s.IntervalClassVector.ToArray()).ToArray();
        gpuIcvs.CopyFromCPU(icvs);
        
        // Launch kernel to compute all pairwise distances
        var kernel = _gpu.LoadAutoGroupedStreamKernel<
            Index2D,
            ArrayView2D<double, Stride2D.DenseX>,
            ArrayView2D<double, Stride2D.DenseX>>(
            ComputePairwiseDistancesKernel);
        
        kernel(new Index2D(numSets, numSets), gpuIcvs.View, gpuDistances.View);
        _gpu.Synchronize();
        
        // Copy results back
        var distances = gpuDistances.GetAsArray2D();
        
        // Build graph from distances (CPU)
        return BuildGraphFromDistances(sets, distances, options);
    }
    
    static void ComputePairwiseDistancesKernel(
        Index2D index,
        ArrayView2D<double, Stride2D.DenseX> icvs,
        ArrayView2D<double, Stride2D.DenseX> distances)
    {
        var i = index.X;
        var j = index.Y;
        
        if (i >= j) return; // Only compute upper triangle
        
        // Compute L2 distance between ICV[i] and ICV[j]
        double sumSq = 0;
        for (int k = 0; k < 6; k++)
        {
            var diff = icvs[i, k] - icvs[j, k];
            sumSq += diff * diff;
        }
        
        var distance = Math.Sqrt(sumSq);
        distances[i, j] = distance;
        distances[j, i] = distance; // Symmetric
    }
}
```

**Expected Performance**:
- Current: ~10-30 seconds for 10,000 sets
- GPU: **~100-500ms** for 10,000 sets
- **Speedup: 20-300x**

---

## 📦 **Required Packages**

### **Backend (.NET 9)**

```xml
<ItemGroup>
  <!-- CUDA Support (NVIDIA GPUs) -->
  <PackageReference Include="ManagedCuda" Version="10.2.89" />
  <PackageReference Include="CuBLAS.NET" Version="12.0.0" Condition="'$(OS)' == 'Windows_NT'" />
  
  <!-- ILGPU (Cross-platform GPU) -->
  <PackageReference Include="ILGPU" Version="1.5.1" />
  <PackageReference Include="ILGPU.Algorithms" Version="1.5.1" />
  
  <!-- SIMD/Tensor Operations -->
  <PackageReference Include="System.Numerics.Tensors" Version="9.0.0" />
  
  <!-- Optional: ML.NET for GPU-accelerated ML -->
  <PackageReference Include="Microsoft.ML" Version="3.0.1" />
  <PackageReference Include="Microsoft.ML.OnnxRuntime.Gpu" Version="1.17.0" />
</ItemGroup>
```

### **Frontend (React/TypeScript)**

Already using WebGPU! Just ensure it's enabled:

```typescript
// Check WebGPU availability
const hasWebGPU = 'gpu' in navigator;

if (hasWebGPU) {
  const renderer = new WebGPURenderer({
    antialias: true,
    samples: 8, // 8x MSAA for maximum quality
  });
  await renderer.init();
}
```

---

## 🎮 **GPU Hardware Requirements**

### **Minimum (Entry-Level Performance)**
- **NVIDIA**: GTX 1050 Ti (4GB VRAM, Compute 6.1)
- **AMD**: RX 570 (4GB VRAM)
- **Intel**: Arc A380 (6GB VRAM)
- **Apple**: M1 (8GB unified memory)

### **Recommended (High Performance)**
- **NVIDIA**: RTX 3060 (12GB VRAM, Compute 8.6)
- **AMD**: RX 6700 XT (12GB VRAM)
- **Intel**: Arc A770 (16GB VRAM)
- **Apple**: M2 Pro/Max (16-32GB unified)

### **Optimal (Maximum Performance)**
- **NVIDIA**: RTX 4090 (24GB VRAM, Compute 8.9)
- **AMD**: RX 7900 XTX (24GB VRAM)
- **Apple**: M3 Max (48-128GB unified)

---

## 📈 **Expected Performance Gains**

| Operation | CPU Time | GPU Time | Speedup |
|-----------|----------|----------|---------|
| **Vector Search (10K chords)** | 100ms | 1-2ms | **50-100x** |
| **ICV Computation (1M sets)** | 5s | 50-100ms | **50-100x** |
| **Shape Graph Build (10K sets)** | 30s | 100-500ms | **60-300x** |
| **Heat Map Generation** | 2s | 20-50ms | **40-100x** |
| **BSP Tree Traversal** | 500ms | 5-10ms | **50-100x** |
| **Batch Embedding (1K items)** | 10s | 100-200ms | **50-100x** |

**Overall System Throughput**: **10-100x improvement** for compute-intensive operations

---

## 🚀 **Quick Start: Enable GPU Acceleration**

### **Step 1: Install CUDA Toolkit** (NVIDIA only)
```bash
# Download from: https://developer.nvidia.com/cuda-downloads
# Install CUDA 12.0 or later
```

### **Step 2: Compile CUDA Kernels**
```bash
cd Apps/ga-server/GaApi/CUDA/kernels
nvcc -ptx -o cosine_similarity.ptx cosine_similarity.cu
```

### **Step 3: Enable CUDA Strategy**
```csharp
// In Program.cs
builder.Services.AddSingleton<IVectorSearchStrategy>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<CudaVectorSearchStrategy>>();
    var cudaStrategy = new CudaVectorSearchStrategy(logger);
    
    if (cudaStrategy.IsAvailable)
    {
        logger.LogInformation("✅ CUDA GPU acceleration enabled!");
        return cudaStrategy;
    }
    
    logger.LogWarning("⚠️ CUDA not available, falling back to CPU");
    return new InMemoryVectorSearchStrategy(
        sp.GetRequiredService<ILogger<InMemoryVectorSearchStrategy>>());
});
```

### **Step 4: Verify GPU Usage**
```bash
# Monitor GPU usage
nvidia-smi -l 1

# Run benchmark
dotnet run --project Apps/VectorSearchBenchmark
```

---

## 🔧 **Troubleshooting**

### **CUDA Not Found**
```
Error: CUDA driver not found
```
**Solution**: Install NVIDIA drivers + CUDA Toolkit

### **Out of GPU Memory**
```
Error: Out of memory (CUDA_ERROR_OUT_OF_MEMORY)
```
**Solution**: Reduce batch size or use streaming

### **Slow Performance**
```
GPU slower than CPU?
```
**Solution**: Check data transfer overhead, use pinned memory

---

## 📚 **References**

- [CUDA Programming Guide](https://docs.nvidia.com/cuda/)
- [ILGPU Documentation](https://github.com/m4rs-mt/ILGPU)
- [TensorPrimitives API](https://learn.microsoft.com/en-us/dotnet/api/system.numerics.tensors.tensorprimitives)
- [WebGPU Specification](https://www.w3.org/TR/webgpu/)

---

**Next Steps**: See `GPU_IMPLEMENTATION_TASKS.md` for detailed implementation checklist.

