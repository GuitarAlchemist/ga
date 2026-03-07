namespace GenerateNatData.Output;

using System.Runtime.InteropServices;
using GA.Business.ML.Embeddings;

// VoicingMetaRecord is 14 bytes: 6×sbyte + 4×byte + float (Pack=1, no padding)
// The plan spec said 12 — that was a math error. Real layout: 6+1+1+1+1+4 = 14.

/// <summary>
///     Writes voicings.bin (N × float[228] embeddings) and voicings-meta.bin (N × VoicingMetaRecord).
/// </summary>
/// <remarks>
///     voicings.bin format:
///     [16-byte header] [N × TotalDimension float32 records, row-major]
///
///     NumPy load: np.fromfile("voicings.bin", dtype=np.float32, offset=16).reshape(-1, 228)
///
///     voicings-meta.bin format:
///     [12-byte header] [N × VoicingMetaRecord (14 bytes each)]
/// </remarks>
public static class BinaryVoicingWriter
{
    private const int EmbeddingsMagic = 0x47415645; // "GAVE"
    private const int MetaMagic = 0x47415646;       // "GAVF" (frets/features)
    private const byte Version = 1;

    /// <summary>
    ///     Writes the flat float32 embedding array to <paramref name="outputPath"/> with a 16-byte header.
    /// </summary>
    /// <param name="embeddings">Flat float[N × embeddingDim] row-major array.</param>
    /// <param name="n">Number of voicings.</param>
    /// <param name="embeddingDim">Embedding dimension (228).</param>
    /// <param name="config">Constraint config (written to header for provenance).</param>
    /// <param name="outputPath">Output file path.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task WriteEmbeddingsAsync(
        float[] embeddings,
        int n,
        int embeddingDim,
        ConstraintConfig config,
        string outputPath,
        CancellationToken ct = default)
    {
        await using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 1 << 20, useAsync: true);
        await using var bw = new BinaryWriter(fs);

        // 16-byte header
        bw.Write(EmbeddingsMagic);          // 4 bytes
        bw.Write(Version);                  // 1 byte
        bw.Write((byte)embeddingDim);       // 1 byte (228 fits in byte)
        bw.Write(n);                        // 4 bytes
        bw.Write(config.GetStableHash());   // 4 bytes
        bw.Write((short)0);                 // 2 bytes reserved
        // Total: 16 bytes

        // Flush header before writing bulk float data
        bw.Flush();

        // Write float32 payload as raw bytes for maximum throughput
        var bytes = MemoryMarshal.AsBytes(embeddings.AsSpan());
        await fs.WriteAsync(bytes.ToArray(), ct);

        Console.WriteLine($"  voicings.bin: {fs.Length:N0} bytes ({n:N0} × {embeddingDim} × 4)");
    }

    /// <summary>
    ///     Writes VoicingMetaRecord structs to <paramref name="outputPath"/>.
    /// </summary>
    public static async Task WriteMetasAsync(
        VoicingMetaRecord[] metas,
        string outputPath,
        CancellationToken ct = default)
    {
        await using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 1 << 16, useAsync: true);
        await using var bw = new BinaryWriter(fs);

        var recordSize = Marshal.SizeOf<VoicingMetaRecord>(); // 14

        // 12-byte header
        bw.Write(MetaMagic);             // 4 bytes
        bw.Write(Version);               // 1 byte
        bw.Write((byte)recordSize);      // 1 byte: record size (14)
        bw.Write(metas.Length);          // 4 bytes
        bw.Write((short)0);              // 2 bytes padding → 12 bytes total

        bw.Flush();

        // Write as raw bytes for speed
        var bytes = MemoryMarshal.AsBytes(metas.AsSpan());
        await fs.WriteAsync(bytes.ToArray(), ct);

        Console.WriteLine($"  voicings-meta.bin: {fs.Length:N0} bytes ({metas.Length:N0} × {Marshal.SizeOf<VoicingMetaRecord>()})");
    }
}

/// <summary>
///     Per-voicing sidecar metadata for ML label derivation.
///     Packed 12-byte struct for memory-mapped access.
/// </summary>
/// <remarks>
///     Fields enable deriving train_naturalness.py inputs:
///     DeltaAvgFret, MaxFingerDisp, StringCrossingCount, HandStretchDelta, CommonStrings.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VoicingMetaRecord
{
    /// <summary>Raw fret value per string: -1=muted, 0=open, 1-24=fretted.</summary>
    public sbyte Fret0;
    public sbyte Fret1;
    public sbyte Fret2;
    public sbyte Fret3;
    public sbyte Fret4;
    public sbyte Fret5;

    /// <summary>Minimum active (non-zero, non-muted) fret; 0 if all open/muted.</summary>
    public byte StartingFret;

    /// <summary>Count of non-muted strings.</summary>
    public byte NoteCount;

    /// <summary>Fret span of non-open fretted notes (max - min active fret); 0 if all open.</summary>
    public byte FretSpan;

    /// <summary>Reserved for alignment.</summary>
    public byte Reserved;

    /// <summary>Average fret of fretted (non-open) notes; 0 if all open.</summary>
    public float AverageFret;

    // Total: 6×sbyte + 4×byte + float = 14 bytes (Pack=1, verified via Marshal.SizeOf)

    public static VoicingMetaRecord FromFrets(ReadOnlySpan<sbyte> frets, byte startingFret, int stringCount)
    {
        var noteCount = 0;
        var fretSum = 0;
        var fretedCount = 0;
        var minFret = int.MaxValue;
        var maxFret = int.MinValue;

        for (var s = 0; s < Math.Min(stringCount, 6); s++)
        {
            var f = s < frets.Length ? frets[s] : (sbyte)(-1);
            if (f < 0) continue;
            noteCount++;
            if (f > 0)
            {
                fretSum += f;
                fretedCount++;
                if (f < minFret) minFret = f;
                if (f > maxFret) maxFret = f;
            }
        }

        var fretSpan = (fretedCount >= 2 && maxFret > minFret) ? maxFret - minFret : 0;
        var avgFret = fretedCount > 0 ? fretSum / (float)fretedCount : 0.0f;

        return new VoicingMetaRecord
        {
            Fret0 = frets.Length > 0 ? frets[0] : (sbyte)0,
            Fret1 = frets.Length > 1 ? frets[1] : (sbyte)0,
            Fret2 = frets.Length > 2 ? frets[2] : (sbyte)0,
            Fret3 = frets.Length > 3 ? frets[3] : (sbyte)0,
            Fret4 = frets.Length > 4 ? frets[4] : (sbyte)0,
            Fret5 = frets.Length > 5 ? frets[5] : (sbyte)0,
            StartingFret = startingFret,
            NoteCount = (byte)noteCount,
            FretSpan = (byte)Math.Min(fretSpan, 24),
            Reserved = 0,
            AverageFret = avgFret
        };
    }
}
