namespace GenerateNatData.Phase2;

using System.Runtime.InteropServices;
using GA.Business.ML.Embeddings;
using ILGPU;
using ILGPU.Algorithms;
using ILGPU.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
///     ILGPU kernel that computes STRUCTURE (dims 6-29) and MORPHOLOGY (dims 30-53) for all N voicings
///     in a single GPU dispatch. Falls back transparently to the CPU accelerator if no GPU is detected.
/// </summary>
/// <remarks>
///     ILGPU compiles to PTX (real CUDA) on NVIDIA hardware and to OpenCL on AMD/Intel.
///     The same kernel code runs identically on all backends.
/// </remarks>
public sealed class OpticKGpuKernel : IDisposable
{
    // ── Kernel constants ──────────────────────────────────────────────────────
    /// <summary>Output floats per voicing (24 STRUCTURE + 24 MORPHOLOGY).</summary>
    public const int OutputDimsPerVoicing = EmbeddingSchema.StructureDim + EmbeddingSchema.MorphologyDim; // 48
    private const int StringCount = 6;

    // ── ILGPU objects ─────────────────────────────────────────────────────────
    private readonly Context _context;
    private readonly Accelerator _accelerator;
    private readonly Action<Index1D, ArrayView<sbyte>, ArrayView<int>, ArrayView<float>, int> _kernel;
    private readonly ILogger<OpticKGpuKernel> _logger;
    private bool _disposed;

    public OpticKGpuKernel(ILogger<OpticKGpuKernel>? logger = null)
    {
        _logger = logger ?? NullLogger<OpticKGpuKernel>.Instance;

        _context = Context.CreateDefault();

        // Prefer CUDA → CPU fallback (same pattern as SetClassGpuAnalyzer)
        var cudaDevice = _context.Devices.FirstOrDefault(d => d.AcceleratorType == AcceleratorType.Cuda);
        if (cudaDevice != null)
        {
            _accelerator = cudaDevice.CreateAccelerator(_context);
            _logger.LogInformation("GPU: CUDA device '{Name}'", _accelerator.Name);
        }
        else
        {
            var cpuDevice = _context.Devices.First(d => d.AcceleratorType == AcceleratorType.CPU);
            _accelerator = cpuDevice.CreateAccelerator(_context);
            _logger.LogWarning(
                "No CUDA/OpenCL GPU found — OpticKGpuKernel running on CPU accelerator. " +
                "Performance will be limited (consider using EmbeddingComputer directly for small batches).");
        }

        _kernel = _accelerator.LoadAutoGroupedStreamKernel<
            Index1D,
            ArrayView<sbyte>,
            ArrayView<int>,
            ArrayView<float>,
            int>(ComputeStructureAndMorphologyKernel);
    }

    /// <summary>
    ///     Computes STRUCTURE + MORPHOLOGY for N voicings in one GPU dispatch.
    /// </summary>
    /// <param name="fretPositions">
    ///     Flat array of N*6 fret values: index [i*6+s] = fret for voicing i, string s.
    ///     Values: -1=muted, 0=open, 1-24=fretted.
    /// </param>
    /// <param name="tuningMidi">Open-string MIDI notes per string [StringCount].</param>
    /// <returns>
    ///     Flat float[N * OutputDimsPerVoicing]:
    ///     index [i * OutputDimsPerVoicing + k] = dim (StructureOffset+k) for voicing i.
    ///     Caller copies slices into dims 6-53 of the full 228-dim vector.
    /// </returns>
    public float[] ComputeBatch(ReadOnlySpan<sbyte> fretPositions, int[] tuningMidi)
    {
        var n = fretPositions.Length / StringCount;
        if (n == 0) return [];

        // Upload inputs
        var hostFrets = fretPositions.ToArray();
        using var deviceFrets = _accelerator.Allocate1D(hostFrets);
        using var deviceTuning = _accelerator.Allocate1D(tuningMidi);
        using var deviceOutput = _accelerator.Allocate1D<float>(n * OutputDimsPerVoicing);

        // Launch kernel
        _kernel((Index1D)n, deviceFrets.View, deviceTuning.View, deviceOutput.View, n);
        _accelerator.Synchronize();

        return deviceOutput.GetAsArray1D();
    }

    /// <summary>
    ///     ILGPU kernel: computes STRUCTURE + MORPHOLOGY for one voicing identified by <paramref name="voicingIdx"/>.
    ///     All math uses ILGPU.Algorithms.XMath — no System.Math allowed in kernel code.
    /// </summary>
    private static void ComputeStructureAndMorphologyKernel(
        Index1D voicingIdx,
        ArrayView<sbyte> fretPositions, // [voicingIdx * 6 .. +6]
        ArrayView<int> tuningMidi,      // [6] standard EADGBE open MIDI notes
        ArrayView<float> output,        // [voicingIdx * 48 .. +48]
        int totalVoicings)
    {
        if (voicingIdx >= totalVoicings) return;

        var baseIn = voicingIdx * StringCount;
        var baseOut = voicingIdx * OutputDimsPerVoicing;

        // ── Step 1: Compute MIDI notes and pitch classes ──────────────────────
        // Note: stack arrays not available in ILGPU kernels — use indexed locals
        // Unrolled for 6 strings (compile-time constant)

        var midi0 = fretPositions[baseIn + 0] >= 0 ? tuningMidi[0] + fretPositions[baseIn + 0] : -1;
        var midi1 = fretPositions[baseIn + 1] >= 0 ? tuningMidi[1] + fretPositions[baseIn + 1] : -1;
        var midi2 = fretPositions[baseIn + 2] >= 0 ? tuningMidi[2] + fretPositions[baseIn + 2] : -1;
        var midi3 = fretPositions[baseIn + 3] >= 0 ? tuningMidi[3] + fretPositions[baseIn + 3] : -1;
        var midi4 = fretPositions[baseIn + 4] >= 0 ? tuningMidi[4] + fretPositions[baseIn + 4] : -1;
        var midi5 = fretPositions[baseIn + 5] >= 0 ? tuningMidi[5] + fretPositions[baseIn + 5] : -1;

        // PCS presence bits (dims 0-11 of output)
        for (var pc = 0; pc < 12; pc++) output[baseOut + pc] = 0.0f;
        if (midi0 >= 0) output[baseOut + ((midi0 % 12 + 12) % 12)] = 1.0f;
        if (midi1 >= 0) output[baseOut + ((midi1 % 12 + 12) % 12)] = 1.0f;
        if (midi2 >= 0) output[baseOut + ((midi2 % 12 + 12) % 12)] = 1.0f;
        if (midi3 >= 0) output[baseOut + ((midi3 % 12 + 12) % 12)] = 1.0f;
        if (midi4 >= 0) output[baseOut + ((midi4 % 12 + 12) % 12)] = 1.0f;
        if (midi5 >= 0) output[baseOut + ((midi5 % 12 + 12) % 12)] = 1.0f;

        // Unique pitch class count (for cardinality and ICV normalization)
        var uniquePcs = 0;
        for (var pc = 0; pc < 12; pc++)
            uniquePcs += output[baseOut + pc] > 0 ? 1 : 0;

        // Cardinality (dim 12)
        output[baseOut + 12] = uniquePcs / 12.0f;

        // ── Step 2: ICV (dims 13-18) ──────────────────────────────────────────
        // Count interval class occurrences among distinct pitch class pairs
        float ic1 = 0, ic2 = 0, ic3 = 0, ic4 = 0, ic5 = 0, ic6 = 0;
        for (var pc1 = 0; pc1 < 12; pc1++)
        {
            if (output[baseOut + pc1] == 0) continue;
            for (var pc2 = pc1 + 1; pc2 < 12; pc2++)
            {
                if (output[baseOut + pc2] == 0) continue;
                var interval = pc2 - pc1; // 1-11
                var ic = interval > 6 ? 12 - interval : interval; // 1-6
                if (ic == 1) ic1++;
                else if (ic == 2) ic2++;
                else if (ic == 3) ic3++;
                else if (ic == 4) ic4++;
                else if (ic == 5) ic5++;
                else if (ic == 6) ic6++;
            }
        }

        var maxPairs = XMath.Max(1.0f, uniquePcs * (uniquePcs - 1) / 2.0f);
        output[baseOut + 13] = ic1 / maxPairs;
        output[baseOut + 14] = ic2 / maxPairs;
        output[baseOut + 15] = ic3 / maxPairs;
        output[baseOut + 16] = ic4 / maxPairs;
        output[baseOut + 17] = ic5 / maxPairs;
        output[baseOut + 18] = ic6 / maxPairs;

        // Complementarity (dim 19)
        output[baseOut + 19] = 1.0f - uniquePcs / 12.0f;

        // Dims 20-23: tonal properties (zeros — require root analysis)

        // ── Step 3: MORPHOLOGY (dims 24-47) ──────────────────────────────────
        var noteCount = 0;
        var minActiveFret = 99;
        var maxActiveFret = -1;
        var fretSum = 0;
        var openCount = 0;

        var f0 = (int)fretPositions[baseIn + 0];
        var f1 = (int)fretPositions[baseIn + 1];
        var f2 = (int)fretPositions[baseIn + 2];
        var f3 = (int)fretPositions[baseIn + 3];
        var f4 = (int)fretPositions[baseIn + 4];
        var f5 = (int)fretPositions[baseIn + 5];

        if (f0 >= 0) { noteCount++; if (f0 == 0) openCount++; else { fretSum += f0; if (f0 < minActiveFret) minActiveFret = f0; if (f0 > maxActiveFret) maxActiveFret = f0; } }
        if (f1 >= 0) { noteCount++; if (f1 == 0) openCount++; else { fretSum += f1; if (f1 < minActiveFret) minActiveFret = f1; if (f1 > maxActiveFret) maxActiveFret = f1; } }
        if (f2 >= 0) { noteCount++; if (f2 == 0) openCount++; else { fretSum += f2; if (f2 < minActiveFret) minActiveFret = f2; if (f2 > maxActiveFret) maxActiveFret = f2; } }
        if (f3 >= 0) { noteCount++; if (f3 == 0) openCount++; else { fretSum += f3; if (f3 < minActiveFret) minActiveFret = f3; if (f3 > maxActiveFret) maxActiveFret = f3; } }
        if (f4 >= 0) { noteCount++; if (f4 == 0) openCount++; else { fretSum += f4; if (f4 < minActiveFret) minActiveFret = f4; if (f4 > maxActiveFret) maxActiveFret = f4; } }
        if (f5 >= 0) { noteCount++; if (f5 == 0) openCount++; else { fretSum += f5; if (f5 < minActiveFret) minActiveFret = f5; if (f5 > maxActiveFret) maxActiveFret = f5; } }

        var fretedNotes = noteCount - openCount;
        var fretSpan = (maxActiveFret < 0 || minActiveFret > 24) ? 0 : maxActiveFret - minActiveFret;
        var avgFret = fretedNotes > 0 ? fretSum / (float)fretedNotes : 0.0f;

        output[baseOut + 24] = fretSpan / 24.0f;                               // normalized fret span
        output[baseOut + 25] = noteCount / (float)StringCount;                  // note density
        output[baseOut + 26] = avgFret / 24.0f;                                 // avg fret normalized
        output[baseOut + 27] = (minActiveFret > 0 && minActiveFret <= 24 && CountAtFret(fretPositions, baseIn, (sbyte)minActiveFret) >= 3) ? 1.0f : 0.0f; // barre
        output[baseOut + 28] = openCount / (float)StringCount;                  // open string ratio
        output[baseOut + 29] = (minActiveFret <= 24) ? minActiveFret / 24.0f : 0.0f; // min fret

        // Per-string normalized fret positions (dims 30-35)
        output[baseOut + 30] = f0 >= 0 ? f0 / 24.0f : 0.0f;
        output[baseOut + 31] = f1 >= 0 ? f1 / 24.0f : 0.0f;
        output[baseOut + 32] = f2 >= 0 ? f2 / 24.0f : 0.0f;
        output[baseOut + 33] = f3 >= 0 ? f3 / 24.0f : 0.0f;
        output[baseOut + 34] = f4 >= 0 ? f4 / 24.0f : 0.0f;
        output[baseOut + 35] = f5 >= 0 ? f5 / 24.0f : 0.0f;

        // String activity bits (dims 36-41)
        output[baseOut + 36] = f0 >= 0 ? 1.0f : 0.0f;
        output[baseOut + 37] = f1 >= 0 ? 1.0f : 0.0f;
        output[baseOut + 38] = f2 >= 0 ? 1.0f : 0.0f;
        output[baseOut + 39] = f3 >= 0 ? 1.0f : 0.0f;
        output[baseOut + 40] = f4 >= 0 ? 1.0f : 0.0f;
        output[baseOut + 41] = f5 >= 0 ? 1.0f : 0.0f;

        // Dims 42-47: reserved zeros
    }

    private static int CountAtFret(ArrayView<sbyte> frets, int baseIn, sbyte targetFret)
    {
        var count = 0;
        for (var s = 0; s < StringCount; s++)
            if (frets[baseIn + s] == targetFret) count++;
        return count;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _accelerator.Dispose();
        _context.Dispose();
    }
}
