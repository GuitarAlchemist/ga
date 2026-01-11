namespace GaCLI.Commands;

using GA.Data.MongoDB.Models;
using GA.Data.MongoDB.Services;
using MongoDB.Driver;
using Spectre.Console;

public class ValidateVoicingIndexCommand(MongoDbService mongoDbService)
{
    public async Task ExecuteAsync()
    {
        AnsiConsole.Write(
            new FigletText("Index Validator")
                .LeftJustified()
                .Color(Color.Purple));

        var collection = mongoDbService.Voicings;

        // 1. Basic Counts
        AnsiConsole.MarkupLine("[bold yellow]1. Checking Global Counts...[/]");
        var count = await collection.CountDocumentsAsync(FilterDefinition<VoicingEntity>.Empty);
        AnsiConsole.MarkupLine($"Total Voicings: [cyan]{count:N0}[/]");

        if (count == 0)
        {
            AnsiConsole.MarkupLine("[red]Index is empty! Aborting verification.[/]");
            return;
        }

        // 2. Integrity Checks (Missing Fields)
        AnsiConsole.MarkupLine("\n[bold yellow]2. Checking Data Integrity...[/]");
        
        var missingDiagram = await collection.CountDocumentsAsync(Builders<VoicingEntity>.Filter.Eq(v => v.Diagram, null));
        if (missingDiagram > 0) AnsiConsole.MarkupLine($"[red]Found {missingDiagram} voicings with missing Diagram![/]");
        else AnsiConsole.MarkupLine("[green]All voicings have diagrams.[/]");

        var missingPitchClasses = await collection.CountDocumentsAsync(Builders<VoicingEntity>.Filter.Eq(v => v.PitchClasses, null));
        if (missingPitchClasses > 0) AnsiConsole.MarkupLine($"[red]Found {missingPitchClasses} voicings with missing PitchClasses![/]");
        else AnsiConsole.MarkupLine("[green]All voicings have pitch classes.[/]");

        var missingTags = await collection.CountDocumentsAsync(Builders<VoicingEntity>.Filter.Eq(v => v.SemanticTags, null));
        if (missingTags > 0) AnsiConsole.MarkupLine($"[red]Found {missingTags} voicings with missing SemanticTags![/]");
        else AnsiConsole.MarkupLine("[green]All voicings have semantic tags.[/]");

        // 3. Logic & Analysis Verification
        AnsiConsole.MarkupLine("\n[bold yellow]3. Playability & Analysis Spot Checks...[/]");

        // Check for impossible stretches marked as "Beginner" (contradiction check)
        var impossibleBeginner = await collection.CountDocumentsAsync(
            Builders<VoicingEntity>.Filter.And(
                Builders<VoicingEntity>.Filter.Eq(v => v.Difficulty, "Beginner"),
                Builders<VoicingEntity>.Filter.Gt(v => v.HandStretch, 5) // >5 fret stretch is definitely not beginner
            ));
        
        if (impossibleBeginner > 0) AnsiConsole.MarkupLine($"[red]Found {impossibleBeginner} 'Beginner' voicings with >5 fret stretch (Likely Analysis Error)[/]");
        else AnsiConsole.MarkupLine("[green]Difficulty ratings seem consistent with hand stretch.[/]");

        // Check for Drop Voicings
        var drop2Count = await collection.CountDocumentsAsync(Builders<VoicingEntity>.Filter.Eq(v => v.VoicingType, "Drop-2"));
        AnsiConsole.MarkupLine($"Drop-2 Voicings: [cyan]{drop2Count:N0}[/]");
        
        var drop3Count = await collection.CountDocumentsAsync(Builders<VoicingEntity>.Filter.Eq(v => v.VoicingType, "Drop-3"));
        AnsiConsole.MarkupLine($"Drop-3 Voicings: [cyan]{drop3Count:N0}[/]");

        // 4. Specific Chord Verification
        AnsiConsole.MarkupLine("\n[bold yellow]4. Sample Chord Verification...[/]");
        await CheckChord("C Major", "C", 1); // "C"
        await CheckChord("C Major 7", "Cmaj7", 1); // "Cmaj7"
        await CheckChord("F Dominant 7", "F7", 1); // "F7"
        await CheckChord("Petrushka Chord", "Petrushka", 0); 
    }

    private async Task CheckChord(string descriptiveName, string dbName, int expectedMin)
    {
        var collection = mongoDbService.Voicings;
        // Search by exact ChordName OR Tag
        var filter = Builders<VoicingEntity>.Filter.Eq(v => v.ChordName, dbName) |
                     Builders<VoicingEntity>.Filter.AnyEq(v => v.SemanticTags, dbName);
        
        var count = await collection.CountDocumentsAsync(filter);
        if (count >= expectedMin)
            AnsiConsole.MarkupLine($"Chord '{descriptiveName}' (key: {dbName}): [green]Found {count:N0} variations[/]");
        else if (expectedMin > 0)
            AnsiConsole.MarkupLine($"Chord '{descriptiveName}' (key: {dbName}): [red]Found only {count:N0} variations (Expected > {expectedMin})[/]");
        else 
             AnsiConsole.MarkupLine($"Chord '{descriptiveName}' (key: {dbName}): [dim]Found {count:N0} variations (Rare/Exotic)[/]");
    }
}
