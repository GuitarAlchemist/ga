// GaDuckLens — proof that GA core-domain entities can be queried as live SQL by
// embedding DuckDB in-process (DuckDB.NET) and registering the domain model as
// table/scalar functions. This is the C# mirror of ix's `ix-duck` crate.
//
//   (no args) / --demo     register ga_scale_notes + run sample queries
//   --sql "<query>"        register the functions, run an arbitrary query
//   --appender [db]        snapshot the full Key corpus into a real `ga_key_notes`
//                          table (default db: ga-domain.duckdb) — which the
//                          standalone `duckdb.exe` CLI can then read, no extension.
//
// Why in-process rather than a C#-authored .duckdb_extension: DuckDB hosts native
// extensions over a C ABI, so a C# extension would need Native AOT + hand-written
// C glue. Embedding DuckDB in the CLR (here) needs none of that and is supported
// out of the box. Table/scalar functions live only on THIS connection; to reach
// the standalone CLI, materialize via the appender (see --appender).

using DuckDB.NET.Data;
using GA.Domain.Core.Theory.Tonal;

var command = args.Length > 0 ? args[0] : "--demo";

using var conn = new DuckDBConnection("DataSource=:memory:");
conn.Open();
RegisterGaFunctions(conn);

switch (command)
{
    case "--appender":
        SnapshotKeyCorpus(args.Length > 1 ? args[1] : "ga-domain.duckdb");
        break;

    case "--sql":
        if (args.Length < 2) throw new ArgumentException("--sql requires a query string");
        PrintQuery(conn, args[1]);
        break;

    default: // --demo
        Console.WriteLine("GaDuckLens — GA domain entities as live DuckDB SQL\n");
        Console.WriteLine("SELECT * FROM ga_scale_notes('C', 'major'):");
        PrintQuery(conn, "SELECT * FROM ga_scale_notes('C', 'major')");
        Console.WriteLine("\nSELECT * FROM ga_scale_notes('F#', 'minor'):");
        PrintQuery(conn, "SELECT * FROM ga_scale_notes('F#', 'minor')");
        Console.WriteLine("\nJoin across two invocations — common pitch classes of C major and A minor:");
        PrintQuery(conn,
            "SELECT c.note AS c_major, a.note AS a_minor, c.pitch_class " +
            "FROM ga_scale_notes('C','major') c " +
            "JOIN ga_scale_notes('A','minor') a USING (pitch_class) ORDER BY c.pitch_class");
        break;
}

// --- Registration --------------------------------------------------------

void RegisterGaFunctions(DuckDBConnection c)
{
    // ga_scale_notes(root VARCHAR, mode VARCHAR) -> (degree, note, pitch_class)
    // Backed by the Key domain type. The bind callback reads the SQL args and
    // hands back the schema + a lazy row enumerable; the mapper writes one row.
    c.RegisterTableFunction<string, string>("ga_scale_notes",
        parameters =>
        {
            var root = parameters[0].GetValue<string>();
            var mode = parameters[1].GetValue<string>();
            var columns = new List<ColumnInfo>
            {
                new("degree", typeof(int)),
                new("note", typeof(string)),
                new("pitch_class", typeof(int)),
            };
            return new TableFunction(columns, ScaleNotes(root, mode));
        },
        (item, writers, rowIndex) =>
        {
            var row = (ScaleNote)item;
            writers[0].WriteValue(row.Degree, rowIndex);
            writers[1].WriteValue(row.Note, rowIndex);
            writers[2].WriteValue(row.PitchClass, rowIndex);
        });
}

// --- Domain projection ---------------------------------------------------

IEnumerable<ScaleNote> ScaleNotes(string root, string mode)
{
    var key = ResolveKey(root, mode);
    var degree = 1;
    foreach (var note in key.Notes)
        yield return new ScaleNote(degree++, note.ToString(), note.PitchClass.Value);
}

Key ResolveKey(string root, string mode)
{
    var m = mode.Trim().ToLowerInvariant();
    try
    {
        switch (m)
        {
            case "major" or "maj" or "ionian":
                if (Key.Major.TryParse(root, out var major)) return major;
                break;
            case "minor" or "min" or "aeolian":
                if (Key.Minor.TryParse(root, out var minor)) return minor;
                break;
            default:
                // POC vocabulary: major | minor. Church modes are a follow-up via
                // GA.Domain.Core ScaleMode (rooted rotation of the parent scale).
                throw new ArgumentException($"unsupported mode '{mode}' (supported: major | minor)");
        }
    }
    catch (Exception e) when (e is not ArgumentException)
    {
        throw new ArgumentException($"could not parse root '{root}': {e.Message}");
    }
    throw new ArgumentException($"unknown {m} key root '{root}'");
}

// --- Appender snapshot (reaches the standalone CLI) ----------------------

void SnapshotKeyCorpus(string dbPath)
{
    using var c = new DuckDBConnection($"DataSource={dbPath}");
    c.Open();

    using (var cmd = c.CreateCommand())
    {
        cmd.CommandText =
            "CREATE OR REPLACE TABLE ga_key_notes (" +
            "key VARCHAR, key_mode VARCHAR, root VARCHAR, degree INTEGER, note VARCHAR, pitch_class INTEGER)";
        cmd.ExecuteNonQuery();
    }

    var rows = 0;
    using (var appender = c.CreateAppender("ga_key_notes"))
    {
        foreach (var key in Key.Items)
        {
            var keyName = key.ToString();
            var keyMode = key.KeyMode.ToString();
            var root = key.Root.ToString();
            var degree = 1;
            foreach (var note in key.Notes)
            {
                appender.CreateRow()
                    .AppendValue(keyName)
                    .AppendValue(keyMode)
                    .AppendValue(root)
                    .AppendValue((int?)degree++)
                    .AppendValue(note.ToString())
                    .AppendValue((int?)note.PitchClass.Value)
                    .EndRow();
                rows++;
            }
        }
    }

    long total;
    using (var cmd = c.CreateCommand())
    {
        cmd.CommandText = "SELECT count(*) FROM ga_key_notes";
        total = Convert.ToInt64(cmd.ExecuteScalar());
    }

    Console.WriteLine($"Snapshotted {rows} note-rows across {Key.Items.Count} keys into {dbPath} (ga_key_notes = {total} rows).");
    Console.WriteLine($"The standalone CLI can now read it:  duckdb {dbPath} \"SELECT * FROM ga_key_notes LIMIT 5\"");
}

// --- Generic result printer ---------------------------------------------

void PrintQuery(DuckDBConnection c, string sql)
{
    using var cmd = c.CreateCommand();
    cmd.CommandText = sql;
    using var reader = cmd.ExecuteReader();

    var names = new string[reader.FieldCount];
    var widths = new int[reader.FieldCount];
    for (var i = 0; i < reader.FieldCount; i++)
    {
        names[i] = reader.GetName(i);
        widths[i] = names[i].Length;
    }

    var table = new List<string[]>();
    while (reader.Read())
    {
        var row = new string[reader.FieldCount];
        for (var i = 0; i < reader.FieldCount; i++)
        {
            row[i] = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)?.ToString() ?? "";
            widths[i] = Math.Max(widths[i], row[i].Length);
        }
        table.Add(row);
    }

    Console.WriteLine(string.Join(" | ", names.Select((n, i) => n.PadRight(widths[i]))));
    Console.WriteLine(string.Join("-+-", widths.Select(w => new string('-', w))));
    foreach (var row in table)
        Console.WriteLine(string.Join(" | ", row.Select((cell, i) => cell.PadRight(widths[i]))));
    Console.WriteLine($"({table.Count} row{(table.Count == 1 ? "" : "s")})");
}

internal sealed record ScaleNote(int Degree, string Note, int PitchClass);
