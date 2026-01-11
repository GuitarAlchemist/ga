namespace GaCLI.Commands;

using GA.Data.MongoDB.Models;
using GA.Data.MongoDB.Services;
using MongoDB.Driver;
using Spectre.Console;
using System.Linq;

/// <summary>
/// Finds alternative voicings for the same chord (same pitch classes).
/// Usage: ga similar x32010
/// </summary>
public class SimilarVoicingsCommand(MongoDbService mongoDbService)
{
    public async Task ExecuteAsync(string diagram, bool sameBass = false, int limit = 5)
    {
        AnsiConsole.Write(
            new Rule("[bold blue]Find Similar Voicings[/]")
                .RuleStyle("blue"));

        // 1. Normalize Input
        var normalizedDiagram = NormalizeDiagram(diagram);
        if (string.IsNullOrEmpty(normalizedDiagram))
        {
            AnsiConsole.MarkupLine("[red]Invalid diagram format. Use format like: x32010 or x-3-2-0-1-0[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[dim]Source:[/] [bold]{normalizedDiagram}[/]");

        // 2. Find Source Voicing
        var sourceFilter = Builders<VoicingEntity>.Filter.And(
            Builders<VoicingEntity>.Filter.Eq(v => v.Diagram, normalizedDiagram),
            Builders<VoicingEntity>.Filter.Eq(v => v.Tuning, "Standard")
        );
        var source = await mongoDbService.Voicings.Find(sourceFilter).FirstOrDefaultAsync();

        if (source == null)
        {
            AnsiConsole.MarkupLine("[yellow]Source voicing not found in database.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[dim]Identified as:[/] [green]{source.ChordName}[/]");
        AnsiConsole.MarkupLine($"[dim]Pitch Classes:[/] {string.Join(",", source.PitchClasses ?? [])}");
        AnsiConsole.WriteLine();

        // 3. Find Alternatives (Same Pitch Classes)
        var builder = Builders<VoicingEntity>.Filter;
        var filter = builder.Eq(v => v.Tuning, "Standard") &
                     builder.Ne(v => v.Id, source.Id) & // Exclude self
                     builder.Eq(v => v.PitchClasses, source.PitchClasses);

        if (sameBass && source.MidiNotes?.Length > 0)
        {
             // ... same bass logic ignored ...
             AnsiConsole.MarkupLine("[yellow]--same-bass not fully implemented, ignoring.[/]");
        }

        // execute query
        var alternatives = await mongoDbService.Voicings.Find(filter)
            .Limit(limit)
            .ToListAsync();

        AnsiConsole.MarkupLine($"[bold]Found {alternatives.Count} alternatives.[/]");

        if (alternatives.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No alternative voicings found.[/]");
            return;
        }

        // 4. Display Results
        foreach (var alt in alternatives)
        {
            var displayDiagram = ReverseDiagram(alt.Diagram);
            AnsiConsole.MarkupLine($"[bold]{displayDiagram}[/] - {alt.ChordName} ({alt.Difficulty}, {alt.Register})");
            AnsiConsole.MarkupLine($"  Tags: {string.Join(" ", (alt.SemanticTags ?? []).Take(3))}");
            AnsiConsole.WriteLine();
        }
    }
    
    // Logic from IdentifyCommand (Fixed)
    private static string? NormalizeDiagram(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var cleaned = input.Trim().ToLower().Replace(" ", "-");
        
        if (cleaned.Contains('-'))
        {
            var parts = cleaned.Split('-');
            if (parts.Length != 6) return null;
            if (parts.All(p => p == "x" || int.TryParse(p, out _)))
                return string.Join("-", parts.Reverse());
            return null;
        }

        if (cleaned.Length == 6 && cleaned.All(c => c == 'x' || char.IsDigit(c)))
        {
             return string.Join("-", cleaned.Select(c => c.ToString()).Reverse());
        }

        return null;
    }

    private static string ReverseDiagram(string dbDiagram)
    {
        if (string.IsNullOrEmpty(dbDiagram)) return dbDiagram;
        return string.Join("-", dbDiagram.Split('-').Reverse()); 
    }
}
