namespace AIIntegrationDemo;

using Spectre.Console;

internal class Program
{
    private static async Task Main(string[] args)
    {
        AnsiConsole.Write(
            new FigletText("AI Integration")
                .LeftJustified()
                .Color(Color.Magenta1));

        AnsiConsole.MarkupLine("[bold]Guitar Alchemist AI-Powered Music Analysis[/]\n");

        await DemonstrateSemanticSearch();
        await DemonstrateNaturalLanguageQueries();
        await DemonstrateIntelligentRecommendations();
        await DemonstrateProgressionGeneration();
        await DemonstrateStyleAnalysis();

        AnsiConsole.MarkupLine("\n[green]AI Demo completed![/]");

        try
        {
            if (!Console.IsInputRedirected && Environment.UserInteractive)
            {
                AnsiConsole.MarkupLine("[dim]Press any key to exit...[/]");
                Console.ReadKey();
            }
        }
        catch (InvalidOperationException)
        {
            // Console input not available
        }
    }

    private static async Task DemonstrateSemanticSearch()
    {
        AnsiConsole.MarkupLine("[bold blue]🔍 Semantic Chord Search[/]\n");

        var queries = new[]
        {
            "Find jazzy chords with a sophisticated sound",
            "Show me dark and mysterious chord voicings",
            "I want bright, happy chords for a pop song",
            "Give me complex chords for progressive rock",
            "Find easy chords for a beginner"
        };

        var table = new Table();
        table.AddColumn("Query");
        table.AddColumn("Top Results");
        table.AddColumn("Similarity Score");
        table.AddColumn("Musical Context");

        foreach (var query in queries)
        {
            AnsiConsole.Status()
                .Start($"Searching for: {query}", ctx =>
                {
                    // Simulate semantic search
                    Thread.Sleep(500);
                });

            var results = await SimulateSemanticSearch(query);

            table.AddRow(
                query,
                string.Join(", ", results.Take(3).Select(r => r.ChordName)),
                string.Join(", ", results.Take(3).Select(r => $"{r.Score:F2}")),
                GetMusicalContext(query)
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static async Task DemonstrateNaturalLanguageQueries()
    {
        AnsiConsole.MarkupLine("[bold blue]💬 Natural Language Music Queries[/]\n");

        var conversations = new[]
        {
            ("What chord comes after Dm7 in a jazz progression?",
                "G7 is the most common choice, creating a ii-V movement. You could also try G7alt for more tension."),
            ("How do I make my chord progressions more interesting?",
                "Try using chord substitutions like tritone subs, or add extensions like 9ths and 11ths. Voice leading is also crucial."),
            ("What's the difference between Dorian and natural minor?",
                "Dorian has a raised 6th degree compared to natural minor, giving it a brighter, more sophisticated sound."),
            ("How can I play jazz chords on guitar more easily?",
                "Focus on rootless voicings using the middle 4 strings. Practice drop-2 and drop-3 voicings for smooth voice leading."),
            ("What makes a chord sound 'bluesy'?",
                "The dominant 7th chord with added blue notes (b3, b5, b7) creates that bluesy sound. Try adding the b5 to any dominant chord.")
        };

        foreach (var (question, answer) in conversations)
        {
            AnsiConsole.MarkupLine($"[bold yellow]Q:[/] {question}");

            AnsiConsole.Status()
                .Start("AI is thinking...", ctx => { Thread.Sleep(800); });

            AnsiConsole.MarkupLine($"[bold green]A:[/] {answer}\n");
        }
    }

    private static async Task DemonstrateIntelligentRecommendations()
    {
        AnsiConsole.MarkupLine("[bold blue]🎯 Intelligent Practice Recommendations[/]\n");

        var playerProfiles = new[]
        {
            ("Beginner", "Just started learning guitar",
                new[] { "Open chords", "Basic strumming", "Simple progressions" }),
            ("Intermediate", "Knows basic chords, wants to improve",
                new[] { "Barre chords", "Scale patterns", "Chord transitions" }),
            ("Advanced", "Experienced player seeking new challenges",
                new[] { "Jazz voicings", "Advanced techniques", "Composition" }),
            ("Jazz Student", "Focused on jazz guitar", new[] { "Chord-melody", "Improvisation", "Standards" }),
            ("Rock Player", "Loves rock and metal", new[] { "Power chords", "Lead techniques", "Rhythm patterns" })
        };

        var table = new Table();
        table.AddColumn("Player Level");
        table.AddColumn("Current Focus");
        table.AddColumn("AI Recommendations");
        table.AddColumn("Practice Schedule");
        table.AddColumn("Next Milestone");

        foreach (var (level, description, currentSkills) in playerProfiles)
        {
            var recommendations = await GenerateRecommendations(level, currentSkills);
            var schedule = GeneratePracticeSchedule(level);
            var milestone = GetNextMilestone(level);

            table.AddRow(
                level,
                description,
                string.Join(", ", recommendations),
                schedule,
                milestone
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static async Task DemonstrateProgressionGeneration()
    {
        AnsiConsole.MarkupLine("[bold blue]🎼 AI-Generated Chord Progressions[/]\n");

        var styles = new[] { "Jazz", "Pop", "Rock", "Blues", "Classical" };

        var table = new Table();
        table.AddColumn("Style");
        table.AddColumn("Generated Progression");
        table.AddColumn("Key");
        table.AddColumn("Mood");
        table.AddColumn("Suggested Tempo");

        foreach (var style in styles)
        {
            AnsiConsole.Status()
                .Start($"Generating {style} progression...", ctx => { Thread.Sleep(600); });

            var progression = await GenerateProgression(style);

            table.AddRow(
                style,
                progression.Chords,
                progression.Key,
                progression.Mood,
                progression.Tempo
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static async Task DemonstrateStyleAnalysis()
    {
        AnsiConsole.MarkupLine("[bold blue]🎨 Musical Style Analysis[/]\n");

        var songExamples = new[]
        {
            ("Giant Steps", "John Coltrane", "Bmaj7 D7 Gmaj7 Bb7 Ebmaj7"),
            ("Wonderwall", "Oasis", "Em7 G D C"),
            ("Stairway to Heaven", "Led Zeppelin", "Am C D F G"),
            ("All of Me", "John Legend", "Em C G D"),
            ("Autumn Leaves", "Jazz Standard", "Cm7 F7 BbMaj7 EbMaj7")
        };

        var table = new Table();
        table.AddColumn("Song");
        table.AddColumn("Artist");
        table.AddColumn("Chord Progression");
        table.AddColumn("Detected Style");
        table.AddColumn("Key Characteristics");
        table.AddColumn("Difficulty");

        foreach (var (song, artist, chords) in songExamples)
        {
            var analysis = await AnalyzeStyle(chords);

            table.AddRow(
                song,
                artist,
                chords,
                analysis.Style,
                analysis.Characteristics,
                analysis.Difficulty
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static async Task<List<SearchResult>> SimulateSemanticSearch(string query)
    {
        await Task.Delay(100); // Simulate API call

        return query.ToLower() switch
        {
            var q when q.Contains("jazzy") || q.Contains("sophisticated") => new()
            {
                new("Cmaj7#11", 0.95, "Lydian sound, very sophisticated"),
                new("Am7b5", 0.92, "Half-diminished, complex harmony"),
                new("Dm7/G", 0.89, "Slash chord, smooth voice leading")
            },
            var q when q.Contains("dark") || q.Contains("mysterious") => new()
            {
                new("Cm7b5", 0.94, "Half-diminished, ominous"),
                new("F#dim7", 0.91, "Fully diminished, unstable"),
                new("Bbm(maj7)", 0.88, "Minor-major 7th, haunting")
            },
            var q when q.Contains("bright") || q.Contains("happy") => new()
            {
                new("Cmaj7", 0.96, "Major 7th, uplifting"),
                new("G6/9", 0.93, "Add9 with 6th, cheerful"),
                new("Fmaj7#11", 0.90, "Lydian brightness")
            },
            var q when q.Contains("complex") || q.Contains("progressive") => new()
            {
                new("C7alt", 0.97, "Altered dominant, very complex"),
                new("Fm7b5/Bb", 0.94, "Slash chord with extensions"),
                new("Ebmaj7#5", 0.91, "Augmented major 7th")
            },
            var q when q.Contains("easy") || q.Contains("beginner") => new()
            {
                new("C", 0.98, "Simple major triad"),
                new("Am", 0.96, "Natural minor, easy fingering"),
                new("F", 0.85, "Barre chord, slightly harder")
            },
            _ => new()
            {
                new("Cmaj7", 0.80, "General purpose chord"),
                new("Am7", 0.75, "Versatile minor 7th"),
                new("G7", 0.70, "Classic dominant")
            }
        };
    }

    private static string GetMusicalContext(string query)
    {
        return query.ToLower() switch
        {
            var q when q.Contains("jazz") => "Jazz, sophisticated harmony",
            var q when q.Contains("pop") => "Pop music, accessible",
            var q when q.Contains("rock") => "Rock, powerful sound",
            var q when q.Contains("beginner") => "Learning, foundation",
            _ => "General musical context"
        };
    }

    private static async Task<string[]> GenerateRecommendations(string level, string[] currentSkills)
    {
        await Task.Delay(50);

        return level switch
        {
            "Beginner" => new[]
                { "Practice G-C-D progression", "Work on chord transitions", "Learn basic strumming patterns" },
            "Intermediate" => new[]
                { "Master F barre chord", "Learn pentatonic scales", "Practice chord embellishments" },
            "Advanced" => new[]
                { "Study jazz chord substitutions", "Work on fingerstyle arrangements", "Compose original pieces" },
            "Jazz Student" => new[]
                { "Learn ii-V-I progressions", "Practice chord-melody style", "Study jazz standards" },
            "Rock Player" => new[]
                { "Master power chord progressions", "Learn lead guitar techniques", "Practice rhythm patterns" },
            _ => new[] { "Continue regular practice", "Explore new styles", "Record your playing" }
        };
    }

    private static string GeneratePracticeSchedule(string level)
    {
        return level switch
        {
            "Beginner" => "30 min/day: 15min chords, 15min songs",
            "Intermediate" => "45 min/day: 20min technique, 25min repertoire",
            "Advanced" => "60 min/day: 30min technique, 30min creativity",
            "Jazz Student" => "60 min/day: 20min theory, 40min playing",
            "Rock Player" => "45 min/day: 25min riffs, 20min solos",
            _ => "Flexible based on goals"
        };
    }

    private static string GetNextMilestone(string level)
    {
        return level switch
        {
            "Beginner" => "Play first complete song",
            "Intermediate" => "Master barre chords",
            "Advanced" => "Perform complex piece",
            "Jazz Student" => "Improvise over standards",
            "Rock Player" => "Write original riff",
            _ => "Set personal goal"
        };
    }

    private static async Task<ProgressionResult> GenerateProgression(string style)
    {
        await Task.Delay(100);

        return style switch
        {
            "Jazz" => new("Cmaj7 - Am7 - Dm7 - G7", "C Major", "Sophisticated", "120 BPM"),
            "Pop" => new("C - G - Am - F", "C Major", "Uplifting", "128 BPM"),
            "Rock" => new("E5 - A5 - D5 - G5", "E Minor", "Powerful", "140 BPM"),
            "Blues" => new("E7 - A7 - E7 - B7", "E Blues", "Soulful", "90 BPM"),
            "Classical" => new("C - F - G - C", "C Major", "Elegant", "Andante"),
            _ => new("C - F - G - C", "C Major", "Neutral", "Variable")
        };
    }

    private static async Task<StyleAnalysis> AnalyzeStyle(string chords)
    {
        await Task.Delay(50);

        return chords switch
        {
            var c when c.Contains("maj7") && c.Contains("7") =>
                new("Jazz", "Extended harmony, sophisticated", "Advanced"),
            var c when c.Contains("Em7") && c.Contains("G") && c.Contains("D") && c.Contains("C") =>
                new("Pop/Rock", "vi-IV-I-V progression, very popular", "Intermediate"),
            var c when c.Contains("Am") && c.Contains("F") && c.Contains("G") =>
                new("Rock Ballad", "Minor key, emotional", "Intermediate"),
            var c when c.Contains("7") && !c.Contains("maj7") =>
                new("Blues/Jazz", "Dominant 7th chords, bluesy", "Intermediate"),
            _ => new("Popular", "Common chord progression", "Beginner")
        };
    }

    // Helper classes and methods
    private record SearchResult(string ChordName, double Score, string Description);

    private record ProgressionResult(string Chords, string Key, string Mood, string Tempo);

    private record StyleAnalysis(string Style, string Characteristics, string Difficulty);
}
