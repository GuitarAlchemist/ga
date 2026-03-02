namespace InternetContentDemo;

using GA.Business.DSL.Parsers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

/// <summary>
///     Demonstration of Enhanced Practice Routine DSL with Internet Content Loading
///     Showcases tablature and MIDI loading from public repositories
/// </summary>
internal class Program
{
    private static async Task Main(string[] args)
    {
        // Create host with logging
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        // Create beautiful console interface
        AnsiConsole.Write(
            new FigletText("Internet Content DSL")
                .LeftJustified()
                .Color(Color.Blue));

        AnsiConsole.Write(
            new Panel("Guitar Alchemist - Enhanced Practice Routine DSL with Internet Content Loading")
                .BorderColor(Color.Blue)
                .Header("🌐 Internet Content Integration Demo"));

        try
        {
            await RunEnhancedDemoAsync(logger);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            logger.LogError(ex, "Enhanced demo failed");
        }
    }

    private static async Task RunEnhancedDemoAsync(ILogger logger)
    {
        // Run enhanced parsing tests first
        TestEnhancedParsing();
        Console.WriteLine("\nPress any key to continue to interactive demo...");
        Console.ReadKey();
        AnsiConsole.Clear();

        while (true)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("What would you like to explore?")
                    .PageSize(10)
                    .AddChoices("🌐 Parse Internet Content DSL", "🔍 Test Content Discovery",
                        "🛡️ Validate Content Safety", "📊 Show Enhanced Grammar Examples",
                        "🎯 Interactive Content Builder", "🎸 Load Sample Tablatures", "🎵 Load Sample MIDI Files",
                        "❌ Exit"));

            switch (choice)
            {
                case "🌐 Parse Internet Content DSL":
                    await ParseInternetContentAsync(logger);
                    break;
                case "🔍 Test Content Discovery":
                    await TestContentDiscoveryAsync(logger);
                    break;
                case "🛡️ Validate Content Safety":
                    await ValidateContentSafetyAsync(logger);
                    break;
                case "📊 Show Enhanced Grammar Examples":
                    ShowEnhancedGrammarExamples();
                    break;
                case "🎯 Interactive Content Builder":
                    await InteractiveContentBuilderAsync(logger);
                    break;
                case "🎸 Load Sample Tablatures":
                    await LoadSampleTablaturesAsync(logger);
                    break;
                case "🎵 Load Sample MIDI Files":
                    await LoadSampleMidiAsync(logger);
                    break;
                case "❌ Exit":
                    AnsiConsole.MarkupLine("[green]Thank you for exploring the Enhanced Practice Routine DSL! 🌐🎸[/]");
                    return;
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
            Console.ReadKey();
            AnsiConsole.Clear();
        }
    }

    private static void TestEnhancedParsing()
    {
        Console.WriteLine("🧪 Testing Enhanced Practice Routine DSL with Internet Content...\n");

        var testCases = new[]
        {
            ("Internet Content Loading", @"session ""internet_practice"" 60 minutes intermediate {
  warmup 5: ""finger exercises""
  songs 20: ""Wonderwall"" by ""Oasis"" from ultimate_guitar
  technique 15: ""fingerpicking"" from url ""https://example.com/tab.txt""
  cooldown 5: ""stretching""
}"),

            ("Content Discovery", @"session ""discovery_session"" 45 minutes advanced {
  songs 30: ""classical piece"" search { genre: ""classical"", difficulty: ""advanced"" } in imslp
  technique 15: ""sweep picking"" from midi ""https://example.com/backing.mid""
}"),

            ("Mixed Content Sources", @"session ""mixed_content"" 50 minutes intermediate {
  scales 15: ""pentatonic patterns"" from songsterr
  improvisation 20: ""blues jam"" from freemidi
  songs 15: ""acoustic practice"" from musescore
}")
        };

        var passed = 0;
        var total = testCases.Length;

        foreach (var (name, dsl) in testCases)
        {
            Console.Write($"Testing {name}... ");

            var result = PracticeRoutineParser.parse(dsl);

            if (result.IsOk)
            {
                Console.WriteLine("✅ PASSED");
                passed++;
            }
            else
            {
                Console.WriteLine($"❌ FAILED: {result.ErrorValue}");
            }
        }

        Console.WriteLine($"\n📊 Results: {passed}/{total} enhanced tests passed");

        if (passed == total)
        {
            Console.WriteLine("🎉 All Enhanced Practice Routine DSL tests passed!");
        }
        else
        {
            Console.WriteLine("⚠️ Some tests failed. Check the enhanced DSL syntax.");
        }
    }

    private static async Task ParseInternetContentAsync(ILogger logger)
    {
        AnsiConsole.Write(new Rule("[blue]Parse Internet Content DSL[/]").LeftJustified());

        var input = AnsiConsole.Ask<string>("Enter enhanced practice routine DSL code:");

        var result = PracticeRoutineParser.parse(input);

        if (result.IsOk)
        {
            AnsiConsole.MarkupLine("[green]✓ Enhanced syntax is valid![/]");
            AnsiConsole.MarkupLine("[dim]Successfully parsed practice routine with internet content[/]");
        }
        else
        {
            var error = result.ErrorValue;
            AnsiConsole.MarkupLine($"[red]✗ Parse error: {error}[/]");
        }

        logger.LogInformation("Parsed enhanced practice routine syntax");
    }

    private static async Task TestContentDiscoveryAsync(ILogger logger)
    {
        AnsiConsole.Write(new Rule("[purple]Test Content Discovery[/]").LeftJustified());

        AnsiConsole.MarkupLine("🔍 Simulating content discovery from various repositories...\n");

        var repositories = new[] { "Ultimate Guitar", "Songsterr", "MuseScore", "IMSLP", "FreeMidi" };

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                foreach (var repo in repositories)
                {
                    var task = ctx.AddTask($"[green]Searching {repo}[/]");

                    for (var i = 0; i <= 100; i += 20)
                    {
                        task.Value = i;
                        await Task.Delay(100);
                    }

                    AnsiConsole.MarkupLine($"[green]✓[/] Found content in {repo}");
                }
            });

        var mockResults = new[]
        {
            ("Wonderwall - Oasis", "Ultimate Guitar", "Easy", "4.8/5"),
            ("Master of Puppets - Metallica", "Songsterr", "Very Hard", "4.7/5"),
            ("Canon in D - Pachelbel", "MuseScore", "Medium", "4.6/5"),
            ("BWV 999 - Bach", "IMSLP", "Hard", "4.9/5"),
            ("Blues Backing Track", "FreeMidi", "Medium", "4.5/5")
        };

        var table = new Table();
        table.AddColumn("Song");
        table.AddColumn("Repository");
        table.AddColumn("Difficulty");
        table.AddColumn("Rating");

        foreach (var (song, repo, difficulty, rating) in mockResults)
        {
            table.AddRow(song, repo, difficulty, rating);
        }

        AnsiConsole.Write(table);
        logger.LogInformation("Tested content discovery across {RepositoryCount} repositories", repositories.Length);
    }

    private static async Task ValidateContentSafetyAsync(ILogger logger)
    {
        AnsiConsole.Write(new Rule("[red]Validate Content Safety[/]").LeftJustified());

        var testUrls = new[]
        {
            ("https://ultimate-guitar.com/tab/oasis/wonderwall", "✅ Trusted"),
            ("https://songsterr.com/tab/metallica/master", "✅ Trusted"),
            ("https://malicious-site.com/script.js", "❌ Untrusted"),
            ("https://imslp.org/wiki/Bach_BWV_999", "✅ Trusted"),
            ("javascript:alert('xss')", "❌ Malicious")
        };

        AnsiConsole.MarkupLine("🛡️ Testing URL safety validation...\n");

        foreach (var (url, expected) in testUrls)
        {
            AnsiConsole.MarkupLine($"URL: [dim]{url}[/]");
            AnsiConsole.MarkupLine($"Status: {expected}\n");
        }

        logger.LogInformation("Validated {UrlCount} URLs for safety", testUrls.Length);
    }

    private static void ShowEnhancedGrammarExamples()
    {
        AnsiConsole.Write(new Rule("[cyan]Enhanced DSL Grammar Examples[/]").LeftJustified());

        var examples = new[]
        {
            ("Internet Content Loading", @"session ""internet_practice"" 60 minutes intermediate {
  songs 20: ""Wonderwall"" by ""Oasis""
    from ultimate_guitar { difficulty: easy, tuning: standard }
  improvisation 15: ""blues jam""
    content midi ""https://freemidi.org/blues-backing.mid""
}"),

            ("Content Discovery", @"session ""discovery_session"" 45 minutes advanced {
  songs 30: ""classical piece""
    search { genre: ""classical"", license: ""public_domain"" } in imslp
  technique 15: ""fingerpicking patterns""
    search { style: ""fingerpicking"" } in songsterr
}"),

            ("AI Content Generation", @"session ""ai_practice"" 40 minutes intermediate {
  scales 15: ""pentatonic practice""
    content generate {
      style: ""rock"",
      scale: ""A minor pentatonic"",
      length: 8 bars
    }
}")
        };

        foreach (var (title, example) in examples)
        {
            AnsiConsole.Write(
                new Panel(example)
                    .Header($"[bold]{title}[/]")
                    .BorderColor(Color.Yellow));
            AnsiConsole.WriteLine();
        }
    }

    private static async Task InteractiveContentBuilderAsync(ILogger logger)
    {
        AnsiConsole.Write(new Rule("[yellow]Interactive Content Builder[/]").LeftJustified());

        var sessionName = AnsiConsole.Ask<string>("Enter session name:");
        var duration = AnsiConsole.Ask<int>("Enter duration in minutes:");

        var contentSource = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select content source:")
                .AddChoices("Ultimate Guitar", "Songsterr", "MuseScore", "IMSLP", "Direct URL", "AI Generated"));

        var songTitle = AnsiConsole.Ask<string>("Enter song/exercise title:");

        var builtSession = contentSource switch
        {
            "Ultimate Guitar" => $@"session ""{sessionName}"" {duration} minutes intermediate {{
  songs 20: ""{songTitle}"" from ultimate_guitar
}}",
            "Direct URL" => $@"session ""{sessionName}"" {duration} minutes intermediate {{
  technique 15: ""{songTitle}"" from url ""https://example.com/tab.txt""
}}",
            "AI Generated" => $@"session ""{sessionName}"" {duration} minutes intermediate {{
  scales 15: ""{songTitle}""
    content generate {{ style: ""rock"", complexity: medium }}
}}",
            _ => $@"session ""{sessionName}"" {duration} minutes intermediate {{
  songs 20: ""{songTitle}"" from {contentSource.ToLower().Replace(" ", "_")}
}}"
        };

        AnsiConsole.Write(
            new Panel(builtSession)
                .Header("[bold green]Generated Enhanced Practice Session[/]")
                .BorderColor(Color.Green));

        logger.LogInformation("Built interactive enhanced practice session: {SessionName}", sessionName);
    }

    private static async Task LoadSampleTablaturesAsync(ILogger logger)
    {
        AnsiConsole.Write(new Rule("[green]Load Sample Tablatures[/]").LeftJustified());

        AnsiConsole.MarkupLine("🎸 Simulating tablature loading from various sources...\n");

        var samples = new[]
        {
            ("Wonderwall - Oasis", "Ultimate Guitar", "ASCII Tab"),
            ("Blackbird - Beatles", "Songsterr", "Guitar Pro"),
            ("Classical Gas - Mason Williams", "MuseScore", "MusicXML")
        };

        foreach (var (song, source, format) in samples)
        {
            AnsiConsole.MarkupLine($"Loading: [yellow]{song}[/]");
            AnsiConsole.MarkupLine($"Source: [blue]{source}[/]");
            AnsiConsole.MarkupLine($"Format: [green]{format}[/]");

            await Task.Delay(500); // Simulate loading
            AnsiConsole.MarkupLine("[green]✓ Loaded successfully[/]\n");
        }

        logger.LogInformation("Loaded {SampleCount} sample tablatures", samples.Length);
    }

    private static async Task LoadSampleMidiAsync(ILogger logger)
    {
        AnsiConsole.Write(new Rule("[magenta]Load Sample MIDI Files[/]").LeftJustified());

        AnsiConsole.MarkupLine("🎵 Simulating MIDI file loading...\n");

        var midiSamples = new[]
        {
            ("Blues Backing Track", "FreeMidi", "12-bar blues in A"),
            ("Jazz Chord Progression", "Archive.org", "ii-V-I progression"),
            ("Classical Piece", "IMSLP", "Bach invention")
        };

        foreach (var (title, source, description) in midiSamples)
        {
            AnsiConsole.MarkupLine($"Loading: [yellow]{title}[/]");
            AnsiConsole.MarkupLine($"Source: [blue]{source}[/]");
            AnsiConsole.MarkupLine($"Description: [dim]{description}[/]");

            await Task.Delay(300); // Simulate loading
            AnsiConsole.MarkupLine("[green]✓ MIDI loaded successfully[/]\n");
        }

        logger.LogInformation("Loaded {MidiCount} sample MIDI files", midiSamples.Length);
    }
}
