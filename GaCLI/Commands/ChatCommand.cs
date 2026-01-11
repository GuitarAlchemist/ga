namespace GaCLI.Commands;

using Spectre.Console;
using System.Text.RegularExpressions;

public class ChatCommand(
    IdentifyCommand identifyCommand,
    SearchVoicingsCommand searchCommand)
{
    public async Task ExecuteAsync()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("Guitar Alchemist").Color(Color.Cyan1));
        AnsiConsole.MarkupLine("[dim]Semantic Chatbot CLI v1.0[/]");
        AnsiConsole.MarkupLine("ask me anything like [green]\"show me a sad jazz chord\"[/] or [green]\"identify x32010\"[/]");
        AnsiConsole.MarkupLine("[dim]Type 'exit' or 'quit' to leave.[/]");
        AnsiConsole.WriteLine();

        while (true)
        {
            var input = AnsiConsole.Ask<string>("[bold cyan]You >[/]");
            if (string.IsNullOrWhiteSpace(input)) continue;
            
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) || 
                input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                break;

            await ProcessInput(input);
            AnsiConsole.WriteLine();
        }
    }

    private async Task ProcessInput(string input)
    {
        try 
        {
            // 1. Check for Diagram (Identification)
            // Regex for diagrams: x or digit, dashes optional, approx 6 chars
            var diagramRegex = new Regex(@"[xX\d]{6}|([xX\d]-){5}[xX\d]");
            if (diagramRegex.IsMatch(input) && input.Length < 20)
            {
                var match = diagramRegex.Match(input).Value;
                AnsiConsole.MarkupLine($"[dim]Detected diagram: {match}[/]");
                await identifyCommand.ExecuteAsync(match, verbose: false);
                return;
            }

            // 2. Intent Recognition: Search with Tags
            var tags = ExtractTags(input);
            var difficulty = ExtractDifficulty(input);
            var isShell = input.Contains("shell", StringComparison.OrdinalIgnoreCase);
            
            // If we found specific intent
            if (tags.Count > 0 || difficulty != null || isShell)
            {
                var options = new SearchVoicingsCommand.ValidatedOptions
                {
                    Limit = 5,
                    Detailed = true
                };

                // Apply filters
                if (tags.Count > 0) options.Tag = tags[0]; // Simple single tag for now
                if (difficulty != null) options.Difficulty = difficulty;
                
                // Construct a friendly message
                var searchDesc = new List<string>();
                if (difficulty != null) searchDesc.Add(difficulty);
                if (tags.Count > 0) searchDesc.Add(string.Join("/", tags));
                if (isShell) 
                {
                    searchDesc.Add("shell voicing");
                    options.Tag = "shell-voicing"; // Override tag if shell
                }

                AnsiConsole.MarkupLine($"[dim]Searching for [green]{string.Join(" ", searchDesc)}[/] lines...[/]");
                await searchCommand.ExecuteAsync(options);
                return;
            }

            // 3. Fallback: Generic text search (treat input as chord name if short)
            if (input.Length < 10)
            {
                 AnsiConsole.MarkupLine($"[dim]Searching for chord name: {input}[/]");
                 await searchCommand.ExecuteAsync(new SearchVoicingsCommand.ValidatedOptions 
                 { 
                     ChordName = input,
                     Limit = 3,
                     Detailed = true
                 });
                 return;
            }

            AnsiConsole.MarkupLine("[yellow]I didn't understand that request.[/]");
            AnsiConsole.MarkupLine("Try: [cyan]\"sad chord\"[/], [cyan]\"neo-soul\"[/], [cyan]\"beginner G\"[/], or a diagram like [cyan]x32010[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error processing request: {ex.Message}[/]");
        }
    }

    private List<string> ExtractTags(string input)
    {
        var inputLower = input.ToLowerInvariant();
        var foundTags = new List<string>();

        // Map keywords to semantic tags
        var tagMap = new Dictionary<string, string>
        {
            { "happy", "happy" },
            { "sad", "sad" },
            { "scary", "scary" },
            { "tense", "tense" },
            { "dreamy", "dreamy" },
            { "lydian", "dreamy" }, // Map Lydian to dreamy
            { "neo", "neo-soul" },
            { "soul", "neo-soul" },
            { "jazz", "jazz-chord" },
            { "funk", "funk" },
            { "spanish", "spanish" },
            { "campfire", "campfire-chord" },
            { "hendrix", "hendrix-chord" },
            { "bond", "james-bond-chord" }
        };

        foreach (var kvp in tagMap)
        {
            if (inputLower.Contains(kvp.Key))
            {
                foundTags.Add(kvp.Value);
            }
        }

        return foundTags;
    }

    private string? ExtractDifficulty(string input)
    {
        var l = input.ToLowerInvariant();
        if (l.Contains("easy") || l.Contains("beginner")) return "Beginner";
        if (l.Contains("hard") || l.Contains("advanced") || l.Contains("shred")) return "Advanced";
        if (l.Contains("intermediate")) return "Intermediate";
        return null;
    }
}
