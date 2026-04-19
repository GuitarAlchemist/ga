namespace GA.Business.ML.Search;

using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using Embeddings;

/// <summary>
///     Reads the OPTK v4 memory-mapped index produced by
///     <c>OptickIndexWriter</c>. Vectors are 112-dim, pre-scaled by sqrt(partition weight),
///     and L2-normalized, so cosine similarity reduces to a dot product.
///     <para>
///         <b>Concurrency contract:</b> reads (<see cref="GetVector"/>, <see cref="GetMetadata"/>)
///         are safe from any thread. <b>Dispose must not run while searches are in flight</b> —
///         the pointer gets nulled out and concurrent readers will dereference freed memory.
///         In DI this is enforced by singleton lifetime + shutdown-after-drain; direct callers
///         are responsible for ordering.
///     </para>
/// </summary>
public sealed unsafe class OptickIndexReader : IDisposable
{
    /// <summary>
    ///     Compact vector dimension. Derives from <see cref="EmbeddingSchema.CompactDimension"/>
    ///     — sum of similarity-partition dims. v4-pp: 112. v4-pp-r (v1.8, with ROOT partition): 124.
    /// </summary>
    public static int Dimension => EmbeddingSchema.CompactDimension;

    private const uint SupportedVersion = 4;
    private const ushort ExpectedEndian = 0xFEFF;
    private const int InstrumentCount = 3;
    private static readonly byte[] MagicBytes = "OPTK"u8.ToArray();

    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private byte* _basePtr;
    private readonly float* _vectors;
    private readonly long _count;
    private readonly long _metadataOffsetsStart;
    private readonly long _metadataStart;
    private readonly long _metadataLength;
    private readonly (long FirstIndex, long Count)[] _instrumentRanges;
    private bool _disposed;

    public long Count => _count;

    public OptickIndexReader(string path)
    {
        _mmf = MemoryMappedFile.CreateFromFile(
            path, FileMode.Open, mapName: null, capacity: 0, MemoryMappedFileAccess.Read);
        _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

        byte* ptr = null;
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
        _basePtr = ptr + _accessor.PointerOffset;

        // ── Parse header ────────────────────────────────────────────
        // layout: magic(4) version(4) header_size(4) schema_hash(4) endian(2) _r(2)
        //         dim(4) count(8) instruments(1) _pad(7) instr_offsets(3*16)
        //         meta_offsets_off(8) vectors_off(8) meta_off(8) meta_len(8)
        //         weights(112*4)
        var magic = new ReadOnlySpan<byte>(_basePtr, 4);
        if (!magic.SequenceEqual(MagicBytes))
            throw new InvalidDataException("OPTK magic bytes missing — wrong file or corrupt.");

        var version = ReadU32(4);
        if (version != SupportedVersion)
            throw new InvalidDataException($"Unsupported OPTK version {version} (expected {SupportedVersion}).");

        var schemaHash = ReadU32(12);
        if (schemaHash != EmbeddingSchema.SchemaHashV4)
            throw new InvalidDataException(
                $"OPTK schema hash mismatch: file=0x{schemaHash:X8} code=0x{EmbeddingSchema.SchemaHashV4:X8}. " +
                "Rebuild index after schema change.");

        var endian = ReadU16(16);
        if (endian != ExpectedEndian)
            throw new InvalidDataException("OPTK endian marker mismatch.");

        var dim = ReadU32(20);
        if (dim != Dimension)
            throw new InvalidDataException($"OPTK dimension mismatch: file={dim} expected={Dimension}.");

        _count = (long)ReadU64(24);

        _instrumentRanges = new (long, long)[InstrumentCount];
        long vectorBytesPerEntry = Dimension * sizeof(float);
        long cursor = 40;
        long vectorsOffsetFromHeader = 0;
        for (var i = 0; i < InstrumentCount; i++)
        {
            var byteOff = (long)ReadU64(cursor);
            var cnt = (long)ReadU64(cursor + 8);
            _instrumentRanges[i] = (byteOff, cnt);
            cursor += 16;
            if (i == 0) vectorsOffsetFromHeader = byteOff; // first group starts at vectors_offset
        }

        _metadataOffsetsStart = (long)ReadU64(cursor); cursor += 8;
        var vectorsOffset = (long)ReadU64(cursor); cursor += 8;
        _metadataStart = (long)ReadU64(cursor); cursor += 8;
        _metadataLength = (long)ReadU64(cursor);

        _vectors = (float*)(_basePtr + vectorsOffset);

        // Convert byte-offset instrument ranges into voicing-index ranges.
        for (var i = 0; i < InstrumentCount; i++)
        {
            var (byteOff, cnt) = _instrumentRanges[i];
            var firstIndex = (byteOff - vectorsOffset) / vectorBytesPerEntry;
            _instrumentRanges[i] = (firstIndex, cnt);
        }

        // Warm the OS page cache so the first search doesn't pay 30–400 ms of soft page
        // faults across the 161 MB vector region. Windows-only (Win8+); silent no-op on
        // other platforms or older Windows. Opt-out via GA_OPTICK_NO_PREFETCH=1 for tests
        // that want to measure cold-cache behavior.
        if (Environment.GetEnvironmentVariable("GA_OPTICK_NO_PREFETCH") != "1"
            && OperatingSystem.IsWindowsVersionAtLeast(6, 2))
        {
            var vectorsByteLength = (long)_count * vectorBytesPerEntry;
            TryPrefetch((IntPtr)_vectors, (ulong)vectorsByteLength);
        }
    }

    private static void TryPrefetch(IntPtr address, ulong byteLength)
    {
        try
        {
            var entry = new Win32MemoryRangeEntry { VirtualAddress = address, NumberOfBytes = (UIntPtr)byteLength };
            var ok = PrefetchVirtualMemory(GetCurrentProcess(), (UIntPtr)1, [entry], 0);
            if (!ok)
            {
                Trace.TraceWarning(
                    "OptickIndexReader: PrefetchVirtualMemory returned false (err={0}); first query will page in on demand.",
                    Marshal.GetLastWin32Error());
            }
        }
        catch (Exception ex)
        {
            // EntryPointNotFoundException on older Windows, or DllNotFoundException on non-Windows.
            Trace.TraceWarning("OptickIndexReader: prefetch unavailable ({0}); continuing without it.", ex.GetType().Name);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Win32MemoryRangeEntry
    {
        public IntPtr VirtualAddress;
        public UIntPtr NumberOfBytes;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool PrefetchVirtualMemory(
        IntPtr hProcess,
        UIntPtr NumberOfEntries,
        [In] Win32MemoryRangeEntry[] VirtualAddresses,
        uint Flags);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    /// <summary>Returns (firstIndex, count) for an instrument filter, or the full range if null.</summary>
    public (long FirstIndex, long Count) GetInstrumentRange(string? instrument)
    {
        if (string.IsNullOrEmpty(instrument)) return (0, _count);
        var idx = instrument.ToLowerInvariant() switch
        {
            "guitar" => 0,
            "bass" => 1,
            "ukulele" => 2,
            _ => -1
        };
        return idx < 0 ? (0, _count) : _instrumentRanges[idx];
    }

    /// <summary>Zero-copy view of vector i (length = 112). Lives as long as the reader is alive.</summary>
    public ReadOnlySpan<float> GetVector(long i)
    {
        if ((ulong)i >= (ulong)_count) throw new ArgumentOutOfRangeException(nameof(i));
        return new ReadOnlySpan<float>(_vectors + i * Dimension, Dimension);
    }

    /// <summary>Parses metadata for voicing i. msgpack fixmap{diagram, instrument, midiNotes, quality_inferred}.</summary>
    public OptickMetadata GetMetadata(long i)
    {
        if ((ulong)i >= (ulong)_count) throw new ArgumentOutOfRangeException(nameof(i));
        var recordOff = (long)ReadU64(_metadataOffsetsStart + i * sizeof(ulong));
        var absolute = _metadataStart + recordOff;
        var end = _metadataStart + _metadataLength;
        var cursor = absolute;
        return OptickMetadataParser.ParseRecord(_basePtr, ref cursor, end);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_basePtr != null)
        {
            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            _basePtr = null;
        }
        _accessor.Dispose();
        _mmf.Dispose();
    }

    private uint ReadU32(long off) => MemoryMarshal.Read<uint>(new ReadOnlySpan<byte>(_basePtr + off, 4));
    private ushort ReadU16(long off) => MemoryMarshal.Read<ushort>(new ReadOnlySpan<byte>(_basePtr + off, 2));
    private ulong ReadU64(long off) => MemoryMarshal.Read<ulong>(new ReadOnlySpan<byte>(_basePtr + off, 8));
}

/// <summary>Parsed OPTK metadata record.</summary>
public sealed record OptickMetadata(
    string Diagram,
    string Instrument,
    int[] MidiNotes,
    string? QualityInferred);

/// <summary>
///     Minimal msgpack decoder for OPTK metadata records. Only supports the subset
///     produced by <c>OptickIndexWriter.MsgPack</c>: fixmap, fixstr/str8/str16/str32,
///     fixint/int8/int16/int32, fixarray/array16/array32, nil.
/// </summary>
internal static unsafe class OptickMetadataParser
{
    public static OptickMetadata ParseRecord(byte* basePtr, ref long cursor, long end)
    {
        var mapLen = ReadMapHeader(basePtr, ref cursor);
        string diagram = "", instrument = "";
        int[] midi = [];
        string? quality = null;

        for (var i = 0; i < mapLen; i++)
        {
            var key = ReadString(basePtr, ref cursor) ?? "";
            switch (key)
            {
                case "diagram":          diagram    = ReadString(basePtr, ref cursor) ?? ""; break;
                case "instrument":       instrument = ReadString(basePtr, ref cursor) ?? ""; break;
                case "midiNotes":        midi       = ReadIntArray(basePtr, ref cursor); break;
                case "quality_inferred": quality    = ReadString(basePtr, ref cursor); break;
                default:                 SkipValue(basePtr, ref cursor); break;
            }
            if (cursor > end) throw new InvalidDataException("OPTK metadata overrun.");
        }

        return new OptickMetadata(diagram, instrument, midi, quality);
    }

    private static int ReadMapHeader(byte* p, ref long c)
    {
        var t = p[c++];
        if ((t & 0xF0) == 0x80) return t & 0x0F;                              // fixmap
        if (t == 0xDE) { var n = ReadBE16(p, ref c); return n; }                // map16
        if (t == 0xDF) { var n = ReadBE32(p, ref c); return CheckedLength(n); } // map32
        throw new InvalidDataException($"Expected msgpack map, got 0x{t:X2}.");
    }

    private static string? ReadString(byte* p, ref long c)
    {
        var t = p[c++];
        if (t == 0xC0) return null;                                           // nil
        int len;
        if ((t & 0xE0) == 0xA0) len = t & 0x1F;                               // fixstr
        else if (t == 0xD9) len = p[c++];                                      // str8
        else if (t == 0xDA) len = ReadBE16(p, ref c);                          // str16
        else if (t == 0xDB) len = CheckedLength(ReadBE32(p, ref c));           // str32
        else throw new InvalidDataException($"Expected msgpack string, got 0x{t:X2}.");
        var s = Encoding.UTF8.GetString(p + c, len);
        c += len;
        return s;
    }

    private static int[] ReadIntArray(byte* p, ref long c)
    {
        var t = p[c++];
        int len;
        if ((t & 0xF0) == 0x90) len = t & 0x0F;                               // fixarray
        else if (t == 0xDC) len = ReadBE16(p, ref c);                          // array16
        else if (t == 0xDD) len = CheckedLength(ReadBE32(p, ref c));           // array32
        else throw new InvalidDataException($"Expected msgpack array, got 0x{t:X2}.");
        var arr = new int[len];
        for (var i = 0; i < len; i++) arr[i] = ReadInt(p, ref c);
        return arr;
    }

    /// <summary>Rejects msgpack lengths that can't fit in Int32 without going negative.</summary>
    private static int CheckedLength(uint raw)
    {
        if (raw > int.MaxValue)
            throw new InvalidDataException($"msgpack length {raw} exceeds Int32.MaxValue — corrupt or hostile input.");
        return (int)raw;
    }

    private static int ReadInt(byte* p, ref long c)
    {
        var t = p[c++];
        if ((t & 0x80) == 0) return t;                     // positive fixint 0-127
        if ((t & 0xE0) == 0xE0) return (sbyte)t;           // negative fixint -32..-1
        if (t == 0xD0) return (sbyte)p[c++];               // int8
        if (t == 0xD1) return (short)ReadBE16(p, ref c);   // int16
        if (t == 0xD2) return (int)ReadBE32(p, ref c);     // int32
        if (t == 0xCC) return p[c++];                      // uint8
        if (t == 0xCD) return ReadBE16(p, ref c);          // uint16
        if (t == 0xCE) return (int)ReadBE32(p, ref c);     // uint32
        throw new InvalidDataException($"Unsupported msgpack int type 0x{t:X2}.");
    }

    private static void SkipValue(byte* p, ref long c)
    {
        // Enough to skip unknown values; handles nil/bool/fixint/fixstr and common sized forms.
        var t = p[c++];
        if (t == 0xC0 || t == 0xC2 || t == 0xC3) return;
        if ((t & 0x80) == 0 || (t & 0xE0) == 0xE0) return;
        if ((t & 0xE0) == 0xA0) { c += t & 0x1F; return; }
        if (t == 0xD9) { c += p[c] + 1; return; }
        if (t == 0xDA) { var n = ReadBE16(p, ref c); c += n; return; }
        throw new InvalidDataException($"Cannot skip msgpack type 0x{t:X2}.");
    }

    private static ushort ReadBE16(byte* p, ref long c)
    {
        ushort v = (ushort)((p[c] << 8) | p[c + 1]);
        c += 2;
        return v;
    }

    private static uint ReadBE32(byte* p, ref long c)
    {
        uint v = ((uint)p[c] << 24) | ((uint)p[c + 1] << 16) | ((uint)p[c + 2] << 8) | p[c + 3];
        c += 4;
        return v;
    }
}
