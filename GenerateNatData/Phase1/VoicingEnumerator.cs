namespace GenerateNatData.Phase1;

using System.Diagnostics;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Services.Fretboard.Voicings.Generation;

/// <summary>
///     Enumerates all ergonomically-bounded voicings and writes a compact scratch binary.
/// </summary>
/// <remarks>
///     Scratch binary format (7 bytes/voicing):
///     bytes[0..5]: int8 fret per string (0=open, 1-24=fretted, -1=muted), string 1..6
///     byte[6]:     uint8 minimum active fret (0 if all open/muted)
///
///     File header (16 bytes):
///     int32:  magic = 0x4741564F ("GAVO")
///     byte:   version = 1
///     byte:   stringCount
///     int32:  voicing count N
///     int32:  constraint hash
///     int16:  reserved = 0
/// </remarks>
public static class VoicingEnumerator
{
    private const int Magic = 0x4741564F; // "GAVO"
    private const byte Version = 1;

    /// <summary>
    ///     Enumerates voicings, sorts them for reproducibility, and writes the scratch binary.
    /// </summary>
    /// <param name="config">Ergonomic constraints.</param>
    /// <param name="scratchPath">Path to write the scratch binary.</param>
    /// <param name="dryRun">If true, enumerate and count without writing files.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Total number of unique voicings enumerated.</returns>
    public static async Task<int> EnumerateAsync(
        ConstraintConfig config,
        string scratchPath,
        bool dryRun = false,
        CancellationToken ct = default)
    {
        var fretboard = Fretboard.CreateStandardGuitar();
        var sw = Stopwatch.StartNew();

        Console.WriteLine($"[Phase 1] Enumerating voicings ({config}) ...");

        // Collect all voicings using the sliding window (windowSize = MaxFretSpan)
        // Each window covers MaxFretSpan frets; deduplication via ConcurrentDictionary
        var voicings = await CollectAsync(fretboard, config, ct);

        Console.WriteLine($"[Phase 1] Collected {voicings.Count:N0} unique voicings in {sw.Elapsed.TotalSeconds:F1}s");

        if (dryRun)
        {
            Console.WriteLine("[Phase 1] Dry-run: skipping file write.");
            return voicings.Count;
        }

        // Sort for reproducibility: same constraints → same byte-identical file
        voicings.Sort((a, b) => string.Compare(
            VoicingExtensions.GetPositionDiagram(a.Positions),
            VoicingExtensions.GetPositionDiagram(b.Positions),
            StringComparison.Ordinal));

        Directory.CreateDirectory(Path.GetDirectoryName(scratchPath)!);
        await WriteScratchBinaryAsync(voicings, config, fretboard.StringCount, scratchPath, ct);

        Console.WriteLine($"[Phase 1] Written to: {scratchPath} ({sw.Elapsed.TotalSeconds:F1}s total)");
        return voicings.Count;
    }

    private static async Task<List<Voicing>> CollectAsync(
        Fretboard fretboard,
        ConstraintConfig config,
        CancellationToken ct)
    {
        var voicings = new List<Voicing>(capacity: 300_000);
        await foreach (var voicing in VoicingGenerator.GenerateAllVoicingsAsync(
                           fretboard,
                           windowSize: config.MaxFretSpan,
                           minPlayedNotes: config.MinNotesPlayed,
                           parallel: true,
                           cancellationToken: ct))
        {
            voicings.Add(voicing);
        }

        return voicings;
    }

    private static async Task WriteScratchBinaryAsync(
        List<Voicing> voicings,
        ConstraintConfig config,
        int stringCount,
        string path,
        CancellationToken ct)
    {
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 1 << 20, useAsync: true);
        await using var bw = new BinaryWriter(fs);

        // Write 16-byte header
        bw.Write(Magic);                        // 4 bytes
        bw.Write(Version);                      // 1 byte
        bw.Write((byte)stringCount);            // 1 byte
        bw.Write(voicings.Count);               // 4 bytes
        bw.Write(config.GetStableHash());       // 4 bytes
        bw.Write((short)0);                     // 2 bytes reserved
        // Total: 16 bytes

        // Write records (7 bytes each)
        var record = new byte[7];
        foreach (var voicing in voicings)
        {
            ct.ThrowIfCancellationRequested();
            FillRecord(voicing, stringCount, record);
            bw.Write(record);
        }
    }

    /// <summary>
    ///     Fills a 7-byte record from a voicing's positions.
    /// </summary>
    public static void FillRecord(Voicing voicing, int stringCount, byte[] record)
    {
        var minActiveFret = int.MaxValue;
        for (var s = 0; s < stringCount; s++)
        {
            if (s < voicing.Positions.Length && voicing.Positions[s] is Position.Played played)
            {
                var fret = played.Location.Fret.Value;
                record[s] = (byte)(sbyte)fret; // 0=open, 1-24=fretted → stored as unsigned (safe for 0-24)
                if (fret > 0 && fret < minActiveFret)
                    minActiveFret = fret;
            }
            else
            {
                record[s] = unchecked((byte)(sbyte)(-1)); // 0xFF = muted
            }
        }

        record[6] = minActiveFret == int.MaxValue ? (byte)0 : (byte)minActiveFret;
    }

    /// <summary>
    ///     Reads the voicing count from a scratch file header without loading all records.
    /// </summary>
    public static (int Count, int StringCount, int ConstraintHash) ReadHeader(string scratchPath)
    {
        using var fs = File.OpenRead(scratchPath);
        using var br = new BinaryReader(fs);

        var magic = br.ReadInt32();
        if (magic != Magic)
            throw new InvalidDataException($"Invalid scratch file: expected magic 0x{Magic:X8}, got 0x{magic:X8}");

        br.ReadByte(); // version
        var stringCount = br.ReadByte();
        var count = br.ReadInt32();
        var hash = br.ReadInt32();
        return (count, stringCount, hash);
    }

    /// <summary>
    ///     Streams raw 7-byte records from the scratch file, skipping the header.
    /// </summary>
    public static IEnumerable<(sbyte[] Frets, byte StartingFret)> ReadRecords(
        string scratchPath, int stringCount = 6)
    {
        using var fs = File.OpenRead(scratchPath);
        using var br = new BinaryReader(fs);

        // Skip 16-byte header
        br.ReadBytes(16);

        var record = new byte[7];
        while (br.Read(record, 0, 7) == 7)
        {
            var frets = new sbyte[stringCount];
            for (var s = 0; s < stringCount; s++)
                frets[s] = (sbyte)record[s];
            yield return (frets, record[6]);
        }
    }
}
