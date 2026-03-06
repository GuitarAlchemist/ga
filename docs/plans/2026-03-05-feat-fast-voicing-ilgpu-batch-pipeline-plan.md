---
title: "Fast Chord Voicing ILGPU Batch Pipeline"
type: feat
status: in-progress
date: 2026-03-05
origin: docs/brainstorms/2026-03-05-fast-voicing-indexing-for-ml-brainstorm.md
---

# Fast Chord Voicing ILGPU Batch Pipeline

## Overview

Build a two-phase offline batch console app in `GenerateNatData/` that exhaustively enumerates all ergonomically-bounded chord voicings for standard EADGBE tuning and produces binary OPTIC-K embedding files for ML training. The pipeline runs fully offline (no MongoDB, no live API), produces a `voicings.bin` (N × float[228]) NumPy-loadable file, and a `voicings-meta.bin` sidecar for downstream label derivation.

Key decisions carried forward from brainstorm:
- Two-phase GPU pipeline (C2): CPU combinatorics → GPU OPTIC-K embedding
- ILGPU primary (IS real CUDA/PTX on NVIDIA) — no raw `.cu` dependency in first version
- STRUCTURE + MORPHOLOGY computed in GPU kernel (dims 6–53); CONTEXT/SYMBOLIC on CPU
- Flat binary output (not JSON, not Parquet) — directly NumPy-loadable
- Reproducibility: sorted output, constraint hash in header

## Problem Statement

There is no offline batch path to produce OPTIC-K embedding vectors for all ergonomically valid voicings. The existing generator (`VoicingGenerator.GenerateAllVoicingsAsync`) is a streaming IAsyncEnumerable designed for live API use, and `MusicalEmbeddingGenerator.GenerateEmbeddingAsync` requires a fully assembled `ChordVoicingRagDocument` rather than raw fret positions. The analytical cost (~6ms/voicing CPU × 400K voicings ≈ 40 minutes) makes naive CPU enumeration impractical for ML data generation.

## Proposed Solution

Two phases, each independently re-runnable:

**Phase 1 — CPU Enumeration:** Use `VoicingGenerator` (sliding window mode) to enumerate all voicings within ergonomic constraints and write a compact raw scratch binary (7 bytes/voicing: 6 × int8 fret values + 1 byte starting fret). This is trivially parallelisable pure combinatorics — expected under 30 seconds for 400K voicings.

**Phase 2 — GPU Embedding:** Load the scratch binary, run an ILGPU kernel to compute STRUCTURE (dims 6–29) and MORPHOLOGY (dims 30–53) in parallel for every voicing, then run the existing `MusicalEmbeddingGenerator` CPU path for dims 54–215 (CONTEXT/SYMBOLIC/EXTENSIONS/SPECTRAL/MODAL/HIERARCHY/ATONAL_MODAL), then write the final `voicings.bin` + `voicings-meta.bin` sidecar.

## Technical Approach

### Architecture

```
GenerateNatData/
├── Program.cs               -- CLI entry: "generate-vectors" | "embed-vectors"
├── ConstraintConfig.cs      -- MinNotesPlayed, MaxFretSpan, Tuning, FretCount
│                               Serialized as header in all output files
├── Phase1/
│   └── VoicingEnumerator.cs -- Wraps VoicingGenerator, writes scratch binary
│                               Output: scratch-{hash}.bin
├── Phase2/
│   ├── LeanDocumentBuilder.cs  -- Converts raw fret positions → minimal ChordVoicingRagDocument
│   │                              (for CPU dims 54-215 via MusicalEmbeddingGenerator)
│   ├── OpticKGpuKernel.cs   -- ILGPU kernel: fret positions → STRUCTURE+MORPHOLOGY dims
│   └── BatchEmbedder.cs     -- Orchestrates GPU + CPU passes, writes output files
└── Output/
    ├── voicings.bin         -- N × float[228], header + packed records
    └── voicings-meta.bin    -- N × VoicingMetaRecord, for label derivation
```

### Phase 1: CPU Enumeration

**Entry point:** `VoicingEnumerator.EnumerateAsync(ConstraintConfig config, string scratchPath)`

**Approach:**
- Call `VoicingGenerator.GenerateAllVoicingsAsync(fretboard, windowSize: config.FretCount, minPlayedNotes: config.MinNotesPlayed)`
- Note: `GenerateAllVoicingsAsync` is at `Common/GA.Domain.Services/Fretboard/Voicings/Generation/VoicingGenerator.cs:156`, uses `Channel.CreateUnbounded` with `Parallel.ForEachAsync` (line 196: `MaxDegreeOfParallelism = Environment.ProcessorCount`)
- For full-range enumeration, set `windowSize` = fretboard fret count (24)
- Filter post-generation: `handStretch <= config.MaxFretSpan`
- Deduplicate by position diagram hash (generator already does this via `ConcurrentDictionary`)

**Scratch binary format** (7 bytes/voicing):
```
byte[0..5]: int8 fret per string (0=open, 1-24=fret, -1=muted)
byte[6]:    uint8 starting fret (base of window)
```

**Sort for reproducibility:** Collect all into a `List<RawVoicing>`, sort by `PositionDiagram.ToString()` lexicographically, then write sequentially. Same constraints → same order regardless of thread scheduling.

**File header:**
```csharp
// 16-byte header
BinaryWriter.Write(magic: 0x4741564F);    // "GAVO" (Guitar Alchemist Voicings)
BinaryWriter.Write(version: (byte)1);
BinaryWriter.Write(count: (int)N);
BinaryWriter.Write(constraintHash: (int)config.GetStableHash());
BinaryWriter.Write(reserved: (int)0);
```

### Phase 2: GPU Embedding

#### 2a. ILGPU Kernel — STRUCTURE + MORPHOLOGY (dims 6–53)

**Pattern:** Follow `SetClassGpuAnalyzer.cs` (the cleanest batch pattern in the codebase): build flat host array → allocate GPU buffers → dispatch kernel → `GetAsArray1D()` → slice back.

```csharp
// OpticKGpuKernel.cs
// Input: flattened int8 fret positions (N * 6 bytes)
// Input: standard tuning MIDI offsets [40, 45, 50, 55, 59, 64] for EADGBE
// Output: flattened float (N * 48 floats) for dims 6-53
static void ComputeStructureAndMorphologyKernel(
    Index1D voicingIdx,
    ArrayView<sbyte> fretPositions,   // [voicingIdx * 6 .. +6]
    ArrayView<int>   tuningMidi,      // [6] static, E2=40,A2=45,D3=50,G3=55,B3=59,E4=64
    ArrayView<float> output,          // [voicingIdx * 48 .. +48] (24 STRUCTURE + 24 MORPHOLOGY)
    int totalVoicings)
```

**STRUCTURE dims (6–29) — 24 dims, weight 0.45:**
- PCS presence bits (12 dims): for each pitch class 0-11, 1.0 if present else 0.0
  - `midiNote = tuningMidi[string] + fret` for non-muted strings; `pitchClass = midiNote % 12`
- ICV (6 dims): count of each interval class 1-6 among all pairs of sounding pitch classes
- Prime form flag (1 dim): `XMath.Sqrt` of cardinality as a proxy (full Forte prime form is too complex for kernel; leave as 0.0 to be filled by CPU LeanDocumentBuilder)
- Tonal center proximity (5 dims): pre-embed in a constant buffer passed to kernel

**GPU-safe math:** Use `ILGPU.Algorithms.XMath` — `XMath.Abs`, `XMath.Min`, `XMath.Max`, `XMath.Sqrt`. No `System.Math` in kernels.

**MORPHOLOGY dims (30–53) — 24 dims, weight 0.25:**
- Fret span = `max_active_fret - min_active_fret` (excluding muted and open strings)
- Normalized span: `span / 24.0f`
- Note count: count of non-muted strings
- Normalized note count: `noteCount / 6.0f`
- Average fret: `sum_active_frets / noteCount`
- String density: `noteCount / 6.0f`
- Bass string index: lowest non-muted string (0-5)
- Treble string index: highest non-muted string (0-5)
- Open string count: count of fret==0
- Barre indicator: `minActiveFret > 0 && activeStringCount > 3 ? 1.0f : 0.0f`
- Remaining dims: normalized per-string fret positions ([0..24] → [0..1])

**GPU initialization pattern** (from `GpuVoicingSearchStrategy.cs:312–328`):
```csharp
_context = Context.CreateDefault();
// Probe CUDA first, then OpenCL, then CPU fallback
var cudaDevices = _context.GetCudaDevices();
_accelerator = cudaDevices.Any()
    ? cudaDevices[0].CreateCudaAccelerator(_context)
    : _context.CreateCPUAccelerator(0);  // CPU fallback: same kernel, slower
```

**Data upload/download pattern** (from `GpuVoicingSearchStrategy.cs:509–540`):
```csharp
var hostInput = new sbyte[N * 6];  // flatten scratch data
// ... fill from scratch binary ...
using var deviceInput = _accelerator.Allocate1D(hostInput);
using var deviceOutput = _accelerator.Allocate1D<float>(N * 48);
kernel(N, deviceInput.View, tuningMidi.View, deviceOutput.View, N);
_accelerator.Synchronize();
var hostOutput = deviceOutput.GetAsArray1D();
```

**CPU fallback guard** (from `GpuVoicingSearchStrategy.cs:549–555`):
```csharp
if (_accelerator.AcceleratorType == AcceleratorType.CPU)
    _logger.LogWarning("No GPU found — running STRUCTURE/MORPHOLOGY kernel on CPU accelerator. Performance will be limited.");
```

#### 2b. CPU Pass — Dims 54–215

The existing `MusicalEmbeddingGenerator.GenerateEmbeddingAsync` (`Common/GA.Business.ML/Embeddings/MusicalEmbeddingGenerator.cs:63`) requires a `ChordVoicingRagDocument`. A new `LeanDocumentBuilder` converts raw fret positions + GPU-computed pitch classes into a minimal document:

```csharp
// LeanDocumentBuilder.cs
public static ChordVoicingRagDocument BuildMinimal(
    ReadOnlySpan<sbyte> frets,
    ReadOnlySpan<float> structureDims,    // GPU output dims 0-23 (= OPTIC-K dims 6-29)
    ReadOnlySpan<float> morphologyDims,   // GPU output dims 24-47 (= OPTIC-K dims 30-53)
    int[] standardTuningMidi)
```

Extracts from GPU output:
- `MidiNotes`: active string MIDI pitches
- `PitchClasses`: from PCS presence bits (dims 0-11 of GPU output)
- `HandStretch`: from normalized span × 24 → back to fret count
- `BassPitchClass`, `MelodyPitchClass`: from MIDI notes
- `IntervalClassVector`: from ICV dims (dims 12-17 of GPU output)

For dims the CPU generator can't fill without full analysis (e.g., Consonance, modal flavor): use 0.0f defaults. These will be filled once a full indexing pass is run via the normal live pipeline.

#### 2c. Output Files

**`voicings.bin`** — main output:
```
[16-byte header: magic, version, N, constraintHash, embeddingDim=228]
[N × float[228] packed records, row-major]
```
NumPy: `np.fromfile("voicings.bin", dtype=np.float32, offset=16).reshape(-1, 228)`

**`voicings-meta.bin`** — sidecar for label derivation:
```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct VoicingMetaRecord
{
    fixed sbyte Frets[6];     // raw fret positions (same as scratch file)
    byte StartingFret;
    byte NoteCount;
    byte FretSpan;
    byte Reserved;
    float AverageFret;
    // Total: 12 bytes per record
}
```

This carries exactly what `train_naturalness.py` needs to derive transition features (`DeltaAvgFret`, `MaxFingerDisp`, `StringCrossingCount`, `HandStretchDelta`, `CommonStrings`) between pairs at training time.

### CLI Commands

```
GenerateNatData.exe generate-vectors [--min-notes 2] [--max-span 5] [--tuning EADGBE] [--frets 24] [--output ./out]
GenerateNatData.exe embed-vectors <scratch-file> [--output ./out]
```

`generate-vectors` runs both phases end-to-end.
`embed-vectors` runs Phase 2 only from a pre-existing scratch file.

### CUDA/Rust Extension Point

The ILGPU kernel compiles to PTX on NVIDIA hardware — it IS CUDA. No separate toolchain required. A `INativeCudaKernel` interface is reserved for a future optional `.cu` kernel drop-in (~5-15% faster, requires `nvcc` in build chain):

```csharp
// Future: replace OpticKGpuKernel with a raw CUDA kernel via P/Invoke
// Interface reserved so the pipeline doesn't need restructuring
public interface IOpticKKernel : IDisposable
{
    void ComputeStructureMorphology(ReadOnlySpan<sbyte> frets, Span<float> output, int N);
}
```

Default implementation: `IlgpuOpticKKernel`. Future native: `CudaNativeOpticKKernel`.

### Build Isolation

GPU kernel code that requires the CUDA runtime is isolated per project convention (see `docs/solutions/refactoring/dotnet-solution-structure-cleanup.md`):
```xml
<!-- GenerateNatData/GenerateNatData.csproj -->
<!-- GPU/CUDA code requires CUDA runtime — excluded from standard builds -->
<Compile Remove="Phase2/NativeCuda/**/*.cs" />
```

`OpticKGpuKernel.cs` (ILGPU, no native runtime required) stays in standard compilation.

## Alternative Approaches Considered

**A — CPU-parallel only:** `Parallel.ForEachAsync` with existing `MusicalEmbeddingGenerator`. At 6ms/voicing × 400K voicings = 40 minutes. Rejected: too slow for iterative ML experimentation.

**B — Precomputed relative-form space:** Enumerate only relative fret shapes (root-agnostic), then shift. Rejected: open strings have fixed pitch (open-E ≠ fretted-E on B string in embedding space). Loses musically important distinctions.

**C1 — CPU-parallel with manual SIMD:** Write intrinsics manually for PCS/ICV computation. Rejected: ILGPU gives equivalent GPU parallelism with less code. SIMD is a micro-optimization on top; add only if GPU approach hits unexpected bottlenecks.

**Rust for Phase 1:** Rust + Rayon for combinatorics. Marginal gain — existing C# `stackalloc` + `Parallel.ForEachAsync` is already near Rust speed for this workload. Adds Rust toolchain with no meaningful benefit.

## Pre-Implementation Checks

Two open questions from brainstorm require validation before GPU kernel dims are hardcoded:

1. **OPTIC-K schema stability at 228 dims** — Current `EmbeddingSchema.cs` shows `TotalDimension = 228` with `"OPTIC-K-v1.7"` (line 52). Constant `OpticKv17Dim = 228` confirmed in `GpuVoicingSearchStrategy.cs`. If a v1.8 dimension change is planned, the kernel must account for it. Check `EmbeddingSchema.cs` before hardcoding.

2. **Phase 4 ML pipeline input format** — The AI Architecture notebook describes semantic basins for AI testing. The sidecar format `(frets[6], noteCount, fretSpan, averageFret)` is designed to derive the `train_naturalness.py` 5-feature input on demand. Validate this is sufficient before implementing the sidecar writer.

Both are verify-before-coding checks, not blockers for starting Phase 1.

## System-Wide Impact

**Interaction graph:** `GenerateNatData` is a standalone console app. It calls `VoicingGenerator` (Layer 3) and `MusicalEmbeddingGenerator` (Layer 4) but does not expose any API, push to MongoDB, or interact with the live stack. Zero interaction with running services.

**Error propagation:** The only failure modes are: (a) no GPU detected → CPU fallback (logged, not thrown), (b) disk full during write (IOException surfaces to CLI), (c) VoicingGenerator cancellation (propagated via CancellationToken). No retry logic needed — this is a one-shot offline tool.

**State lifecycle:** Two files on disk per run (scratch + output). Named by constraint hash — same constraints overwrite same files. No orphaned state risk.

**API surface parity:** No API surface. This is a CLI tool.

**Integration test scenarios:**
1. Full pipeline with default constraints on CPU accelerator (no GPU required) — produces deterministic output
2. Same constraint config run twice — produces byte-identical `voicings.bin`
3. Phase 2 alone from a pre-existing scratch file — output matches Phase 1+2 combined run
4. `np.fromfile("voicings.bin", dtype=np.float32, offset=16).reshape(-1, 228)` loads without error
5. Sidecar note count + fret span fields match the voicing's actual position

## Implementation Phases

### Phase 1: Console App + CPU Enumeration (~2 days)

- [x] Expand `GenerateNatData/Program.cs` from stub to CLI with two commands (System.CommandLine 2.0-beta5)
- [x] Implement `ConstraintConfig` with `GetStableHash()` (deterministic constraint fingerprint)
- [x] Implement `VoicingEnumerator`: calls `VoicingGenerator`, filters, collects, sorts, writes scratch binary
- [x] Add `--dry-run` flag: report count without writing files
- [x] Unit test: same config → same count (reproducibility)

**Files to create/modify:**
- `GenerateNatData/Program.cs` — expand stub
- `GenerateNatData/ConstraintConfig.cs` — new
- `GenerateNatData/Phase1/VoicingEnumerator.cs` — new

**Files to reference:**
- `Common/GA.Domain.Services/Fretboard/Voicings/Generation/VoicingGenerator.cs:156` — streaming source

### Phase 2: ILGPU Kernel — STRUCTURE + MORPHOLOGY (~3 days)

- [x] Add ILGPU NuGet reference to `GenerateNatData/GenerateNatData.csproj`
- [x] Implement `OpticKGpuKernel.cs` with `ComputeStructureAndMorphologyKernel` (Index1D)
  - All math via `ILGPU.Algorithms.XMath` (no `System.Math` in kernel)
  - PCS presence bits (12 dims), ICV (6 dims), span/density morphology (24 dims)
- [x] Implement GPU init following `SetClassGpuAnalyzer.cs` pattern (`ctx.Devices.FirstOrDefault`)
- [x] Implement data upload/download following `GpuVoicingSearchStrategy.cs` pattern
- [x] CPU fallback guard (log warning, same kernel runs on CPU accelerator)
- [x] Unit test: known voicing (e.g., G chord) → expected PCS bits {D,G,B} and ICV {IC3,IC4,IC5=1/3}

**Files to create:**
- `GenerateNatData/Phase2/OpticKGpuKernel.cs` — new
- `GenerateNatData/Phase2/BatchEmbedder.cs` — new (orchestrates GPU + CPU)

**Files to reference:**
- `Common/GA.Business.ML/Search/GpuVoicingSearchStrategy.cs:312,353,509,549` — ILGPU patterns
- `Common/GA.Business.Core.Analysis.Gpu/SetClassGpuAnalyzer.cs` — cleanest batch pattern
- `Apps/ga-server/GaApi/Services/ILGPUKernels.cs` — static kernel patterns
- `Common/GA.Business.ML/Embeddings/EmbeddingSchema.cs:52,55,85,100` — dim offsets

### Phase 3: CPU Pass + Output Writing (~2 days)

- [x] Implement `EmbeddingComputer.cs`: standalone 228-dim OPTIC-K computer from raw fret positions (replaces LeanDocumentBuilder + MusicalEmbeddingGenerator DI dependency)
- [x] Implement binary writer for `voicings.bin` (header + float[228] records) → `BinaryVoicingWriter.WriteEmbeddingsAsync`
- [x] Implement binary writer for `voicings-meta.bin` (`VoicingMetaRecord` struct, 12 bytes, Pack=1) → `BinaryVoicingWriter.WriteMetasAsync`
- [ ] Verify with NumPy: `np.fromfile("voicings.bin", dtype=np.float32, offset=16).reshape(-1, 228)` loads cleanly
- [x] Wire `embed-vectors` CLI command to Phase 2 + 3 only (given scratch file)
- [x] Integration test: full pipeline end-to-end on CPU accelerator (14 tests, all passing)

**Files to create:**
- `GenerateNatData/Phase2/LeanDocumentBuilder.cs` — new
- `GenerateNatData/Output/BinaryVoicingWriter.cs` — new

**Files to reference:**
- `Common/GA.Business.ML/Embeddings/MusicalEmbeddingGenerator.cs:63,73,86` — CPU embedding pass
- `Common/GA.Business.ML/Search/VoicingCacheSerialization.cs` — existing cache (JSON, do NOT use for output)
- `train_naturalness.py` — sidecar field requirements (DeltaAvgFret etc.)

### Phase 4: Performance Validation (~1 day)

- [ ] Run on GPU machine with CUDA: confirm Phase 2 completes in <60 seconds for 400K voicings
- [ ] Profile Phase 1: confirm <30 seconds
- [ ] Confirm output file size ≈ 365 MB (400K × 228 × 4 bytes)
- [ ] Add `IOpticKKernel` interface + `IlgpuOpticKKernel` implementation wrapper for future `NativeCudaKernel` extensibility
- [x] Add build isolation: `<Compile Remove="Phase2/NativeCuda/**/*.cs" />` placeholder

## Acceptance Criteria

### Functional

- [ ] `dotnet run --project GenerateNatData -- generate-vectors` completes under 5 minutes on a CUDA/AMD GPU
- [ ] `np.fromfile("voicings.bin", dtype=np.float32, offset=16).reshape(-1, 228)` works without error
- [ ] Same constraint config always produces the same vector count (same sorted scratch file, same output)
- [ ] Zero dependency on MongoDB or the live API stack
- [ ] `embed-vectors` command re-runs Phase 2+3 from an existing scratch file without re-enumerating

### Non-Functional

- [ ] No warnings in `dotnet build` for new files (zero-warnings policy from CLAUDE.md)
- [ ] `Nullable>enable</Nullable>` in `GenerateNatData.csproj`
- [ ] Collection expressions `[...]` throughout (no `new List<T>()`, no `ImmutableList.Create()`)
- [ ] ILGPU kernel compiles to PTX (not CPU-only) — verify via `AcceleratorType != CPU` log message on GPU machine

### Scale

- [ ] Standard tuning, default constraints (minNotes=2, maxSpan=5, 24 frets): 200K–400K voicings
- [ ] Output: `voicings.bin` (~365 MB), `voicings-meta.bin` (~5 MB for 400K × 12 bytes)

## Sources & References

### Origin

- **Brainstorm document:** [docs/brainstorms/2026-03-05-fast-voicing-indexing-for-ml-brainstorm.md](../brainstorms/2026-03-05-fast-voicing-indexing-for-ml-brainstorm.md)
  - Key decisions carried forward: two-phase C2 approach, ILGPU as primary (IS CUDA on NVIDIA), binary flat output, scratch file int8 format, sort-for-reproducibility, voicings-meta.bin sidecar

### Internal References

- **Voicing source:** `Common/GA.Domain.Services/Fretboard/Voicings/Generation/VoicingGenerator.cs:156`
- **OPTIC-K schema:** `Common/GA.Business.ML/Embeddings/EmbeddingSchema.cs:52,55,85,100`
- **Embedding generator:** `Common/GA.Business.ML/Embeddings/MusicalEmbeddingGenerator.cs:63,73,86`
- **ILGPU patterns (primary):** `Common/GA.Business.ML/Search/GpuVoicingSearchStrategy.cs:312,353,509,549`
- **ILGPU batch pattern (cleanest):** `Common/GA.Business.Core.Analysis.Gpu/SetClassGpuAnalyzer.cs`
- **ILGPU float + XMath:** `Common/GA.Business.ML/Text/Gpu/GpuAcceleratedEmbeddingService.cs`
- **ILGPU static kernels + Index2D:** `Apps/ga-server/GaApi/Services/ILGPUKernels.cs`
- **Cache serialization (JSON, do not use):** `Common/GA.Business.ML/Search/VoicingCacheSerialization.cs`
- **Train naturalness (5-float ONNX input):** `train_naturalness.py`
- **Build isolation precedent:** `docs/solutions/refactoring/dotnet-solution-structure-cleanup.md`
- **Stub to implement:** `GenerateNatData/Program.cs`
