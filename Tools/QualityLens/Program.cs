// QualityLens — queries the persisted DuckDB quality analytics layer from .NET.
//
// Reads state/quality/analytics/quality.duckdb (built by analytics/build-views.sql)
// and prints either the quality_latest rollup or a caller-supplied SQL query.
//
//   dotnet run --project Tools/QualityLens                       # latest rollup
//   dotnet run --project Tools/QualityLens -- "SELECT * FROM routing_eval"

using DuckDB.NET.Data;
using Spectre.Console;

var dbPath = FindQualityDb();
if (dbPath is null)
{
    AnsiConsole.MarkupLine("[red]Could not locate state/quality/analytics/quality.duckdb.[/]");
    AnsiConsole.MarkupLine("Build it first: [yellow]duckdb analytics/quality.duckdb < analytics/build-views.sql[/] (from state/quality/).");
    return 1;
}

var sql = args.Length > 0
    ? string.Join(' ', args)
    : "SELECT * FROM quality_latest ORDER BY source";

// READ_ONLY so the tool never contends with a refresh holding the file open.
using var connection = new DuckDBConnection($"Data Source={dbPath};ACCESS_MODE=READ_ONLY");
connection.Open();

using var command = connection.CreateCommand();
command.CommandText = sql;

using var reader = command.ExecuteReader();

var table = new Table().Border(TableBorder.Rounded);
for (var i = 0; i < reader.FieldCount; i++)
    table.AddColumn($"[bold]{Markup.Escape(reader.GetName(i))}[/]");

var rows = 0;
while (reader.Read())
{
    var cells = new string[reader.FieldCount];
    for (var i = 0; i < reader.FieldCount; i++)
        cells[i] = reader.IsDBNull(i) ? "[grey]NULL[/]" : Markup.Escape(reader.GetValue(i)?.ToString() ?? "");
    table.AddRow(cells);
    rows++;
}

AnsiConsole.MarkupLine($"[grey]{Markup.Escape(dbPath)}[/]");
AnsiConsole.Write(table);
AnsiConsole.MarkupLine($"[green]{rows}[/] row(s).");
return 0;

// Walk up from the executable and the current directory to find the repo-relative DB.
static string? FindQualityDb()
{
    const string relative = "state/quality/analytics/quality.duckdb";
    foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
    }
    return null;
}
