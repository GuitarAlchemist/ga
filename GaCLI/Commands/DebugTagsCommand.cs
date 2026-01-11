namespace GaCLI.Commands;
using System.Linq;
using GA.Business.Config.Configuration;
using Spectre.Console;

public class DebugTagsCommand
{
    public void Execute()
    {
        var registry = SymbolicTagRegistry.Instance;
        var tags = registry.GetAllKnownTags().OrderBy(t => t);
        
        var table = new Table().Border(TableBorder.Rounded).Title("Symbolic Tag Registry");
        table.AddColumn("Tag");
        table.AddColumn("Bit Index");

        foreach (var tag in tags)
        {
            table.AddRow(tag, registry.GetBitIndex(tag)?.ToString() ?? "N/A");
        }

        AnsiConsole.Write(table);
    }
}
