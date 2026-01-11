namespace GaCLI.Commands;

using GA.Data.MongoDB.Models;
using GA.Data.MongoDB.Services;
using MongoDB.Driver;
using Spectre.Console;

/// <summary>
/// Identifies what chord a given fret diagram represents.
/// Example: ga identify x32010 → "C Major (92% confidence)"
/// </summary>
public class IdentifyCommand(MongoDbService mongoDbService)
{
    public async Task ExecuteAsync(string diagram, bool verbose = false)
    {
        AnsiConsole.Write(
            new Rule("[bold blue]Chord Identification[/]")
                .RuleStyle("blue"));

        // Normalize the diagram input
        var normalizedDiagram = NormalizeDiagram(diagram);
        
        if (string.IsNullOrEmpty(normalizedDiagram))
        {
            AnsiConsole.MarkupLine("[red]Invalid diagram format. Use format like: x32010 or x-3-2-0-1-0[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[dim]Looking up:[/] [bold]{normalizedDiagram}[/]");
        AnsiConsole.WriteLine();

        // Query MongoDB for this voicing
        // Query MongoDB for this voicing (Standard tuning by default)
        var filter = Builders<VoicingEntity>.Filter.And(
            Builders<VoicingEntity>.Filter.Eq(v => v.Diagram, normalizedDiagram),
            Builders<VoicingEntity>.Filter.Eq(v => v.Tuning, "Standard")
        );
        var voicing = await mongoDbService.Voicings.Find(filter).FirstOrDefaultAsync();

        if (voicing == null)
        {
            AnsiConsole.MarkupLine("[yellow]Voicing not found in database.[/]");
            AnsiConsole.MarkupLine("[dim]This shape may not be indexed, or the format may be incorrect.[/]");
            return;
        }

        // Display the identification results
        DisplayIdentification(voicing, verbose);
    }

    private static string? NormalizeDiagram(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        // Remove spaces and normalize
        var cleaned = input.Trim().ToLower().Replace(" ", "-");
        
        // Handle dash-separated format (or space-separated converted to dashes)
        if (cleaned.Contains('-'))
        {
            var parts = cleaned.Split('-');
            if (parts.Length != 6) return null;
            // Basic validation: x or number
            if (parts.All(p => p == "x" || int.TryParse(p, out _)))
                // Keep order: Low->High (standard reading)
                return string.Join("-", parts);
            return null;
        }

        // Handle compact format (e.g. x32010), strict 6 characters
        if (cleaned.Length == 6 && cleaned.All(c => c == 'x' || char.IsDigit(c)))
        {
             // Keep order: Low->High
             return string.Join("-", cleaned.Select(c => c.ToString()));
        }

        return null;
    }

    private static void DisplayIdentification(VoicingEntity voicing, bool verbose)
    {
        // Main chord name panel
        var chordPanel = new Panel(
            new Markup($"[bold green]{voicing.ChordName ?? "Unknown"}[/]"))
        {
            Header = new PanelHeader("[bold]Primary Identification[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 0)
        };
        AnsiConsole.Write(chordPanel);
        AnsiConsole.WriteLine();

        // Confidence and function
        var table = new Table()
            .Border(TableBorder.Simple)
            .AddColumn("Property")
            .AddColumn("Value");

        table.AddRow("Root Confidence", $"{voicing.RootConfidence:P0}");
        table.AddRow("Harmonic Function", ColorFunction(voicing.HarmonicFunction));
        table.AddRow("Root Pitch Class", voicing.RootPitchClass?.ToString() ?? "Unknown");
        
        if (!string.IsNullOrEmpty(voicing.RomanNumeral))
            table.AddRow("Roman Numeral", voicing.RomanNumeral);

        if (voicing.AlternateChordNames?.Length > 0)
            table.AddRow("Alternate Names", string.Join(", ", voicing.AlternateChordNames));

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Voicing characteristics
        var charTable = new Table()
            .Title("[bold]Voicing Characteristics[/]")
            .Border(TableBorder.Rounded)
            .AddColumn("Category")
            .AddColumn("Details");

        // Sound
        charTable.AddRow("[blue]Register[/]", voicing.Register ?? "Unknown");
        charTable.AddRow("[blue]Brightness[/]", $"{voicing.Brightness:F2} {BrightnessIndicator(voicing.Brightness)}");
        charTable.AddRow("[blue]Consonance[/]", $"{voicing.ConsonanceScore:F2} {ConsonanceIndicator(voicing.ConsonanceScore)}");
        charTable.AddRow("[blue]Spacing[/]", voicing.Spacing ?? "Unknown");
        
        if (voicing.MayBeMuddy)
            charTable.AddRow("[yellow]⚠ Warning[/]", "[yellow]May sound muddy in low register[/]");

        // Playability
        charTable.AddRow("[green]Difficulty[/]", $"{voicing.Difficulty ?? "Unknown"} ({voicing.DifficultyScore:F1})");
        charTable.AddRow("[green]Fret Range[/]", $"{voicing.MinFret} - {voicing.MaxFret} (span: {voicing.HandStretch})");
        charTable.AddRow("[green]Barre Required[/]", voicing.BarreRequired ? "[yellow]Yes[/]" : "[green]No[/]");

        if (voicing.HasGuideTones)
            charTable.AddRow("[cyan]Guide Tones[/]", "[cyan]✓ 3rd and 7th present[/]");

        AnsiConsole.Write(charTable);
        AnsiConsole.WriteLine();

        // Tags
        if (voicing.SemanticTags?.Length > 0)
        {
            AnsiConsole.MarkupLine("[bold]Tags:[/]");
            var tagLine = string.Join(" ", voicing.SemanticTags.Select(t => $"[dim]#{t}[/]"));
            AnsiConsole.MarkupLine(tagLine);
            AnsiConsole.WriteLine();
        }

        // Verbose mode: show more details
        if (verbose)
        {
            AnsiConsole.Write(new Rule("[dim]Extended Analysis[/]").RuleStyle("dim"));
            
            var extTable = new Table()
                .Border(TableBorder.Simple)
                .AddColumn("Field")
                .AddColumn("Value");

            extTable.AddRow("ID", voicing.Id);
            extTable.AddRow("Tuning", voicing.Tuning);
            extTable.AddRow("Diagram (DB)", voicing.Diagram);
            extTable.AddRow("Pitch Classes", string.Join(", ", voicing.PitchClasses ?? []));
            extTable.AddRow("MIDI Notes", string.Join(", ", voicing.MidiNotes ?? []));
            extTable.AddRow("Prime Form", voicing.PrimeFormId ?? "Unknown");
            extTable.AddRow("Forte Code", voicing.ForteCode ?? "Unknown");
            extTable.AddRow("Roughness", $"{voicing.Roughness:F3}");
            extTable.AddRow("Voicing Type", voicing.VoicingType ?? "Unknown");
            extTable.AddRow("Is Rootless", voicing.IsRootless ? "Yes" : "No");
            
            if (voicing.TonesPresent?.Length > 0)
                extTable.AddRow("Tones Present", string.Join(", ", voicing.TonesPresent));
            
            if (voicing.OmittedTones?.Length > 0)
                extTable.AddRow("Omitted Tones", string.Join(", ", voicing.OmittedTones));

            extTable.AddRow("Analysis Engine", voicing.AnalysisEngine ?? "Unknown");
            extTable.AddRow("Analysis Version", voicing.AnalysisVersion ?? "Unknown");
            
            AnsiConsole.Write(extTable);
        }
    }

    private static string ColorFunction(string? function) => function switch
    {
        "Tonic" => "[green]Tonic[/]",
        "Dominant" => "[red]Dominant[/]",
        "Predominant" => "[yellow]Predominant[/]",
        _ => $"[dim]{function ?? "Unknown"}[/]"
    };

    private static string BrightnessIndicator(double brightness) => brightness switch
    {
        > 0.7 => "☀️ bright",
        < 0.3 => "🌙 dark",
        _ => "◐ balanced"
    };

    private static string ConsonanceIndicator(double consonance) => consonance switch
    {
        > 0.7 => "😊 consonant",
        < 0.4 => "😬 tense",
        _ => "😐 neutral"
    };
}
