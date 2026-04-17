namespace FretboardVoicingsCLI;

using System.Buffers.Binary;
using System.Text;

using GA.Business.ML.Embeddings;

/// <summary>
/// A single voicing entry to be written into the OPTIC-K v4 binary index.
/// <paramref name="Embedding"/> is the 228-dim raw vector from MusicalEmbeddingGenerator.
/// The writer extracts the 112 search-relevant dims, applies sqrt-weight scaling,
/// and L2-normalizes before writing.
/// </summary>
public sealed record VoicingEntry(
    float[] Embedding,
    string Diagram,
    string Instrument,
    int[] MidiNotes,
    string? QualityInferred);

/// <summary>
/// Writes a memory-mappable OPTIC-K v4 binary index file.
/// <para>
/// V4 improvements over v3:
/// <list type="bullet">
///   <item>Dimension reduced from 228 → 112 (only search-relevant partitions kept)</item>
///   <item>Info-only partitions (IDENTITY, EXTENSIONS, SPECTRAL, HIERARCHY, ATONAL_MODAL) dropped</item>
///   <item>Per-voicing metadata offset table for O(1) metadata fetch</item>
///   <item>~55% smaller files, no search-quality regression</item>
/// </list>
/// </para>
/// <para>
/// Binary format v4 (little-endian throughout):
/// <list type="bullet">
///   <item>Header: magic, version, schema hash, endian, dim, counts, instrument offsets, metadata_offsets_offset, vectors_offset, metadata_offset, metadata_length, partition weights[112]</item>
///   <item>metadata_offsets: count × u64 (byte offset of each msgpack record, relative to metadata_offset)</item>
///   <item>Vectors: count × 112 × float32 (weighted + L2-normalized), sorted by instrument</item>
///   <item>Metadata: count × msgpack records (unchanged from v3)</item>
/// </list>
/// </para>
/// </summary>
public sealed class OptickIndexWriter : IDisposable
{
    // ---------------------------------------------------------------
    // Constants
    // ---------------------------------------------------------------

    private static readonly byte[] Magic = "OPTK"u8.ToArray();
    private const uint FormatVersion = 4;
    private const byte InstrumentCount = 3;         // guitar, bass, ukulele
    private const ushort EndianMarker = 0xFEFF;

    /// <summary>
    /// Raw embedding size from <see cref="MusicalEmbeddingGenerator"/> (228 for v1.7).
    /// </summary>
    private static readonly int RawDimension = EmbeddingSchema.TotalDimension;

    /// <summary>
    /// Compact v4 dimension — sum of similarity-partition sizes (112 for v1.7).
    /// </summary>
    private static readonly int Dimension = EmbeddingSchema.CompactDimension;

    /// <summary>
    /// Compact partition mapping: (compact_start, raw_start, dim, sqrt_weight).
    /// Derived from <see cref="EmbeddingSchema.SimilarityPartitions"/> — single source of truth.
    /// </summary>
    private static readonly (int CStart, int RStart, int Dim, float SqrtWeight)[] CompactPartitions =
        BuildCompactPartitions();

    /// <summary>
    /// Pre-computed sqrt-scaled weights for all 112 compact dimensions.
    /// </summary>
    private static readonly float[] PartitionWeights = BuildPartitionWeights();

    // ---------------------------------------------------------------
    // Instance state
    // ---------------------------------------------------------------

    private readonly FileStream _stream;
    private bool _disposed;

    /// <summary>
    /// Creates a writer targeting the specified output path.
    /// The file is created (or truncated) immediately.
    /// </summary>
    public OptickIndexWriter(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        _stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
    }

    // ---------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------

    /// <summary>
    /// Writes the complete OPTIC-K v4 index.
    /// <paramref name="entries"/> must be sorted by instrument: guitar first, then bass, then ukulele.
    /// </summary>
    public void WriteIndex(List<VoicingEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        using var writer = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);

        // --- Pre-compute weighted + normalized compact vectors ---
        var vectors = new float[entries.Count][];
        for (var i = 0; i < entries.Count; i++)
        {
            vectors[i] = ExtractAndNormalize(entries[i].Embedding);
        }

        // --- Write metadata to a buffer; record per-record offsets ---
        var metadataOffsets = new ulong[entries.Count];
        byte[] metadataBytes;
        using (var ms = new MemoryStream())
        {
            for (var i = 0; i < entries.Count; i++)
            {
                metadataOffsets[i] = (ulong)ms.Position;
                WriteMetadataRecord(ms, entries[i]);
            }
            metadataBytes = ms.ToArray();
        }

        // --- Compute section offsets ---
        var headerSize = (ulong)ComputeHeaderSize();
        var metadataOffsetsOffset = headerSize;
        var metadataOffsetsByteLength = (ulong)entries.Count * sizeof(ulong);
        var vectorsOffset = metadataOffsetsOffset + metadataOffsetsByteLength;
        var vectorsByteLength = (ulong)entries.Count * (ulong)Dimension * sizeof(float);
        var metadataOffset = vectorsOffset + vectorsByteLength;
        var metadataLength = (ulong)metadataBytes.Length;

        // Instrument byte offsets (absolute, pointing into the vectors section)
        var instrumentGroups = CountByInstrument(entries, vectorsOffset);

        // --- Write header ---
        WriteHeader(writer, (uint)headerSize, (ulong)entries.Count,
            instrumentGroups, metadataOffsetsOffset, vectorsOffset, metadataOffset, metadataLength);

        // --- Write metadata offsets table ---
        foreach (var off in metadataOffsets)
        {
            writer.Write(off);
        }

        // --- Write vectors ---
        WriteVectors(writer, vectors);

        // --- Write metadata ---
        writer.Write(metadataBytes);

        writer.Flush();
    }

    // ---------------------------------------------------------------
    // Header
    // ---------------------------------------------------------------

    /// <summary>
    /// Computes the total header size in bytes.
    /// </summary>
    private static int ComputeHeaderSize()
    {
        var size = 0;
        size += 4;                          // magic
        size += 4;                          // version
        size += 4;                          // header_size
        size += 4;                          // schema_hash
        size += 2;                          // endian_marker
        size += 2;                          // _reserved
        size += 4;                          // dimension
        size += 8;                          // count (uint64)
        size += 1;                          // instruments (byte)
        size += 7;                          // _pad (align to 8)
        size += InstrumentCount * (8 + 8);  // instrument_offsets (3 x (u64, u64))
        size += 8;                          // metadata_offsets_offset (v4)
        size += 8;                          // vectors_offset
        size += 8;                          // metadata_offset
        size += 8;                          // metadata_length
        size += Dimension * 4;              // partition_weights (112 x float)
        return size;
    }

    private void WriteHeader(
        BinaryWriter w,
        uint headerSize,
        ulong count,
        (ulong ByteOffset, ulong Count)[] instrumentOffsets,
        ulong metadataOffsetsOffset,
        ulong vectorsOffset,
        ulong metadataOffset,
        ulong metadataLength)
    {
        w.Write(Magic);                     // magic
        w.Write(FormatVersion);             // version
        w.Write(headerSize);                // header_size
        w.Write(ComputeSchemaHash());       // schema_hash
        w.Write(EndianMarker);              // endian_marker
        w.Write((ushort)0);                 // _reserved
        w.Write((uint)Dimension);           // dimension
        w.Write(count);                     // count (uint64)
        w.Write(InstrumentCount);           // instruments (byte)
        w.Write(new byte[7]);               // _pad

        foreach (var (byteOffset, instrCount) in instrumentOffsets)
        {
            w.Write(byteOffset);
            w.Write(instrCount);
        }

        w.Write(metadataOffsetsOffset);     // v4: points to the per-record offset table
        w.Write(vectorsOffset);
        w.Write(metadataOffset);
        w.Write(metadataLength);

        foreach (var weight in PartitionWeights)
        {
            w.Write(weight);
        }
    }

    // ---------------------------------------------------------------
    // Vectors
    // ---------------------------------------------------------------

    private static void WriteVectors(BinaryWriter w, float[][] vectors)
    {
        foreach (var vec in vectors)
        {
            foreach (var val in vec)
            {
                w.Write(val);
            }
        }
    }

    // ---------------------------------------------------------------
    // Metadata (minimal msgpack encoder)
    // ---------------------------------------------------------------

    /// <summary>
    /// Writes a single metadata record as a msgpack fixmap with 4 keys:
    /// "diagram" (string), "instrument" (string), "midiNotes" (int[]), "quality_inferred" (string/nil).
    /// </summary>
    private static void WriteMetadataRecord(Stream stream, VoicingEntry entry)
    {
        MsgPack.WriteFixMap(stream, 4);

        MsgPack.WriteString(stream, "diagram");
        MsgPack.WriteString(stream, entry.Diagram);

        MsgPack.WriteString(stream, "instrument");
        MsgPack.WriteString(stream, entry.Instrument);

        MsgPack.WriteString(stream, "midiNotes");
        MsgPack.WriteIntArray(stream, entry.MidiNotes);

        MsgPack.WriteString(stream, "quality_inferred");
        if (entry.QualityInferred is not null)
            MsgPack.WriteString(stream, entry.QualityInferred);
        else
            MsgPack.WriteNil(stream);
    }

    // ---------------------------------------------------------------
    // Weight application + L2 normalization
    // ---------------------------------------------------------------

    /// <summary>
    /// Extracts the 112 search-relevant dimensions from the 228-dim raw embedding,
    /// multiplies each by its sqrt-scaled partition weight, and L2-normalizes.
    /// Zero vectors are returned as-is (no division).
    /// </summary>
    internal static float[] ExtractAndNormalize(float[] raw)
    {
        if (raw.Length != RawDimension)
            throw new ArgumentException(
                $"Expected {RawDimension} raw dimensions, got {raw.Length}.", nameof(raw));

        var compact = new float[Dimension];
        var sumSq = 0.0f;

        foreach (var (cStart, rStart, dim, sqrtWeight) in CompactPartitions)
        {
            for (var j = 0; j < dim; j++)
            {
                var v = raw[rStart + j] * sqrtWeight;
                compact[cStart + j] = v;
                sumSq += v * v;
            }
        }

        if (sumSq <= float.Epsilon)
            return compact; // zero vector

        var norm = MathF.Sqrt(sumSq);
        for (var i = 0; i < Dimension; i++)
            compact[i] /= norm;

        return compact;
    }

    // ---------------------------------------------------------------
    // Schema hash
    // ---------------------------------------------------------------

    /// <summary>
    /// Schema hash for the OPTK v4 format — read from the authoritative
    /// <see cref="EmbeddingSchema.SchemaHashV4"/> so writer + reader cannot drift.
    /// </summary>
    private static uint ComputeSchemaHash() => EmbeddingSchema.SchemaHashV4;

    // ---------------------------------------------------------------
    // Instrument grouping
    // ---------------------------------------------------------------

    /// <summary>
    /// Counts entries per instrument and computes byte offsets into the vector section.
    /// Returns exactly 3 entries (guitar, bass, ukulele) with their byte offsets and counts.
    /// Entries must be sorted by instrument in that order.
    /// </summary>
    private static (ulong ByteOffset, ulong Count)[] CountByInstrument(
        List<VoicingEntry> entries,
        ulong vectorsOffset)
    {
        var instrumentNames = new[] { "guitar", "bass", "ukulele" };
        var result = new (ulong ByteOffset, ulong Count)[InstrumentCount];

        var runningOffset = vectorsOffset;
        var idx = 0;

        for (var i = 0; i < InstrumentCount; i++)
        {
            var name = instrumentNames[i];
            ulong instrCount = 0;

            while (idx < entries.Count &&
                   string.Equals(entries[idx].Instrument, name, StringComparison.OrdinalIgnoreCase))
            {
                instrCount++;
                idx++;
            }

            result[i] = (runningOffset, instrCount);
            runningOffset += instrCount * (ulong)Dimension * sizeof(float);
        }

        return result;
    }

    // ---------------------------------------------------------------
    // Partition weight builder
    // ---------------------------------------------------------------

    private static float[] BuildPartitionWeights()
    {
        var weights = new float[Dimension];
        foreach (var (cStart, _, dim, sqrtWeight) in CompactPartitions)
        {
            for (var j = 0; j < dim; j++)
                weights[cStart + j] = sqrtWeight;
        }
        return weights;
    }

    /// <summary>
    /// Builds the compact-space partition mapping from the authoritative
    /// <see cref="EmbeddingSchema.SimilarityPartitions"/>.
    /// </summary>
    private static (int CStart, int RStart, int Dim, float SqrtWeight)[] BuildCompactPartitions()
    {
        var result = new List<(int, int, int, float)>();
        var cStart = 0;
        foreach (var p in EmbeddingSchema.SimilarityPartitions)
        {
            result.Add((cStart, p.Start, p.Dim, p.SqrtWeight));
            cStart += p.Dim;
        }
        return [.. result];
    }

    // ---------------------------------------------------------------
    // IDisposable
    // ---------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stream.Dispose();
    }
}

// ===================================================================
// Minimal msgpack encoder (subset: fixmap, str, int, array, nil)
// ===================================================================

/// <summary>
/// Minimal MessagePack encoder supporting only the types needed for OPTIC-K metadata:
/// fixmap, str 8/16/32, int (fixint/int8/int16/int32), array 16/32, and nil.
/// Encodes in big-endian as per the msgpack specification.
/// </summary>
internal static class MsgPack
{
    /// <summary>Writes a fixmap header (up to 15 entries).</summary>
    public static void WriteFixMap(Stream s, int count)
    {
        if (count is < 0 or > 15)
            throw new ArgumentOutOfRangeException(nameof(count), "Fixmap supports 0-15 entries.");
        s.WriteByte((byte)(0x80 | count));
    }

    /// <summary>Writes nil (0xC0).</summary>
    public static void WriteNil(Stream s) => s.WriteByte(0xC0);

    /// <summary>Writes a string with appropriate msgpack str format.</summary>
    public static void WriteString(Stream s, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var len = bytes.Length;

        if (len <= 31)
        {
            // fixstr: 101XXXXX
            s.WriteByte((byte)(0xA0 | len));
        }
        else if (len <= 0xFF)
        {
            // str 8: 0xD9 + 1-byte length
            s.WriteByte(0xD9);
            s.WriteByte((byte)len);
        }
        else if (len <= 0xFFFF)
        {
            // str 16: 0xDA + 2-byte length (big-endian)
            s.WriteByte(0xDA);
            Span<byte> buf = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(buf, (ushort)len);
            s.Write(buf);
        }
        else
        {
            // str 32: 0xDB + 4-byte length (big-endian)
            s.WriteByte(0xDB);
            Span<byte> buf = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(buf, (uint)len);
            s.Write(buf);
        }

        s.Write(bytes);
    }

    /// <summary>Writes a signed 32-bit integer using the most compact msgpack encoding.</summary>
    public static void WriteInt(Stream s, int value)
    {
        switch (value)
        {
            // positive fixint: 0XXXXXXX (0..127)
            case >= 0 and <= 127:
                s.WriteByte((byte)value);
                break;

            // negative fixint: 111XXXXX (-32..-1)
            case >= -32 and < 0:
                s.WriteByte((byte)(value & 0xFF));
                break;

            // uint 8: 0xCC + 1 byte (128..255)
            case > 127 and <= 255:
                s.WriteByte(0xCC);
                s.WriteByte((byte)value);
                break;

            // int 8: 0xD0 + 1 byte (-128..-33)
            case >= -128 and < -32:
                s.WriteByte(0xD0);
                s.WriteByte((byte)(value & 0xFF));
                break;

            // uint 16: 0xCD + 2 bytes (256..65535)
            case > 255 and <= 65535:
            {
                s.WriteByte(0xCD);
                Span<byte> buf = stackalloc byte[2];
                BinaryPrimitives.WriteUInt16BigEndian(buf, (ushort)value);
                s.Write(buf);
                break;
            }

            // int 16: 0xD1 + 2 bytes (-32768..-129)
            case >= -32768 and < -128:
            {
                s.WriteByte(0xD1);
                Span<byte> buf = stackalloc byte[2];
                BinaryPrimitives.WriteInt16BigEndian(buf, (short)value);
                s.Write(buf);
                break;
            }

            // int 32 / uint 32 fallback
            default:
            {
                if (value >= 0)
                {
                    // uint 32: 0xCE + 4 bytes
                    s.WriteByte(0xCE);
                    Span<byte> buf = stackalloc byte[4];
                    BinaryPrimitives.WriteUInt32BigEndian(buf, (uint)value);
                    s.Write(buf);
                }
                else
                {
                    // int 32: 0xD2 + 4 bytes
                    s.WriteByte(0xD2);
                    Span<byte> buf = stackalloc byte[4];
                    BinaryPrimitives.WriteInt32BigEndian(buf, value);
                    s.Write(buf);
                }
                break;
            }
        }
    }

    /// <summary>Writes an array of integers with an array 16 or array 32 header.</summary>
    public static void WriteIntArray(Stream s, int[] values)
    {
        var len = values.Length;

        if (len <= 15)
        {
            // fixarray: 1001XXXX
            s.WriteByte((byte)(0x90 | len));
        }
        else if (len <= 0xFFFF)
        {
            // array 16: 0xDC + 2-byte length (big-endian)
            s.WriteByte(0xDC);
            Span<byte> buf = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(buf, (ushort)len);
            s.Write(buf);
        }
        else
        {
            // array 32: 0xDD + 4-byte length (big-endian)
            s.WriteByte(0xDD);
            Span<byte> buf = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(buf, (uint)len);
            s.Write(buf);
        }

        foreach (var v in values)
            WriteInt(s, v);
    }
}
