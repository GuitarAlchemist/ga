namespace GenerateNatData.Phase2;

/// <summary>
///     Abstraction over the OPTIC-K GPU kernel for STRUCTURE + MORPHOLOGY dims (6–53).
/// </summary>
/// <remarks>
///     Default implementation: <see cref="OpticKGpuKernel"/> (ILGPU → PTX on NVIDIA, OpenCL on AMD, CPU fallback).
///     Future implementation: <c>CudaNativeOpticKKernel</c> via P/Invoke to a compiled <c>.cu</c> kernel
///     (~5–15% faster, requires <c>nvcc</c> in the build chain) — drop-in replacement via this interface.
/// </remarks>
public interface IOpticKKernel : IDisposable
{
    /// <summary>
    ///     Computes STRUCTURE (dims 6–29) and MORPHOLOGY (dims 30–53) for N voicings in one batch.
    /// </summary>
    /// <param name="fretPositions">
    ///     Flat array of N×6 fret values (row-major). Values: -1=muted, 0=open, 1–24=fretted.
    /// </param>
    /// <param name="tuningMidi">Open-string MIDI notes per string [6].</param>
    /// <returns>
    ///     float[N × <see cref="OutputDimsPerVoicing"/>] — dims 6–53 for each voicing.
    ///     Caller copies into the full 228-dim embedding at <c>EmbeddingSchema.StructureOffset</c>.
    /// </returns>
    float[] ComputeBatch(ReadOnlySpan<sbyte> fretPositions, int[] tuningMidi);

    /// <summary>Number of output floats per voicing (48 = 24 STRUCTURE + 24 MORPHOLOGY).</summary>
    int OutputDimsPerVoicing { get; }
}
