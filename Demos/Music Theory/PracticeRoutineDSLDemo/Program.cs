namespace PracticeRoutineDSLDemo;

using GA.MusicTheory.DSL.Parsers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

/// <summary>
///     Demonstration of the Practice Routine DSL for Guitar Alchemist
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
            new FigletText("Practice Routine DSL")
                .LeftJustified()
                .Color(Color.Green));

        AnsiConsole.Write(
            new Panel("Guitar Alchemist - Practice Routine Domain Specific Language")
                .BorderColor(Color.Blue)
                .Header("🎸 Practice Routine DSL Demo"));

        try
        {
            // Run quick parsing tests first
            TestParsing.RunTests();
            Console.WriteLine("\nPress any key to continue to interactive demo...");
            Console.ReadKey();
            AnsiConsole.Clear();

            await RunDemoAsync(logger);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            logger.LogError(ex, "Demo failed");
        }
    }

    private static async Task RunDemoAsync(ILogger logger)
    {
        while (true)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("What would you like to do?")
                    .PageSize(10)
                    .AddChoices("📝 Parse Sample Practice Routines", "✍️ Create Custom Practice Routine",
                        "🔍 Validate Practice Routine Syntax", "📊 Show DSL Grammar Examples",
                        "🎯 Interactive Practice Session Builder", "❌ Exit"));

            switch (choice)
            {
                case "📝 Parse Sample Practice Routines":
                    await ParseSampleRoutinesAsync(logger);
                    break;
                case "✍️ Create Custom Practice Routine":
                    await CreateCustomRoutineAsync(logger);
                    break;
                case "🔍 Validate Practice Routine Syntax":
                    await ValidateSyntaxAsync(logger);
                    break;
                case "📊 Show DSL Grammar Examples":
                    ShowGrammarExamples();
                    break;
                case "🎯 Interactive Practice Session Builder":
                    await InteractiveBuilderAsync(logger);
                    break;
                case "❌ Exit":
                    AnsiConsole.MarkupLine("[green]Thank you for exploring the Practice Routine DSL! 🎸[/]");
                    return;
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
            Console.ReadKey();
            AnsiConsole.Clear();
        }
    }

    private static async Task ParseSampleRoutinesAsync(ILogger logger)
    {
        AnsiConsole.Write(new Rule("[blue]Sample Practice Routines[/]").LeftJustified());

        var samples = new[]
        {
            ("Beginner Daily Practice", """
                                        session "beginner_daily" 30 minutes beginner {
                                          warmup 5: "finger exercises" at 60 bpm
                                          scales 10: "C major scale" at 80 bpm
                                          chords 10: "C-Am-F-G progression" at 70 bpm
                                          cooldown 5: "stretching and relaxation"
                                        }
                                        """),

            ("Intermediate Technique Focus", """
                                             session "technique_focus" 45 minutes intermediate {
                                               warmup 5: "chromatic exercises" at start 60 increase_to 80 bpm
                                               technique 20: "alternate picking" at 120 bpm difficulty medium
                                               scales 15: "pentatonic patterns" at 100 bpm
                                               cooldown 5: "gentle stretching"
                                             }
                                             """),

            ("Advanced Jazz Practice", """
                                       session "jazz_practice" 60 minutes advanced {
                                         warmup 10: "finger independence" at 80 bpm
                                         scales 20: "jazz scales and modes" at 120 bpm
                                         chords 20: "jazz chord progressions" at 90 bpm difficulty hard
                                         improvisation 10: "jazz improvisation" at 110 bpm
                                       }
                                       """)
        };

        foreach (var (name, routine) in samples)
        {
            AnsiConsole.MarkupLine($"\n[yellow]Parsing: {name}[/]");

            var result = PracticeRoutineParser.parse(routine);

            if (result.IsOk)
            {
                AnsiConsole.MarkupLine("[green]✓ Successfully parsed![/]");
                AnsiConsole.MarkupLine("[dim]Parsed practice routine successfully[/]");
            }
            else
            {
                var error = result.ErrorValue;
                AnsiConsole.MarkupLine($"[red]✗ Parse error: {error}[/]");
            }
        }

        logger.LogInformation("Parsed {SampleCount} sample practice routines", samples.Length);
    }

    private static async Task CreateCustomRoutineAsync(ILogger logger)
    {
        AnsiConsole.Write(new Rule("[green]Create Custom Practice Routine[/]").LeftJustified());

        var sessionName = AnsiConsole.Ask<string>("Enter session name:");
        var duration = AnsiConsole.Ask<int>("Enter duration in minutes:");
        var skillLevel = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select skill level:")
                .AddChoices("beginner", "intermediate", "advanced", "expert"));

        var exercises = new List<string>();

        while (true)
        {
            var exerciseType = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Add exercise type (or Done to finish):")
                    .AddChoices("warmup", "technique", "scales", "chords", "songs", "cooldown", "Done"));

            if (exerciseType == "Done")
            {
                break;
            }

            var exerciseDuration = AnsiConsole.Ask<int>($"Duration for {exerciseType} (minutes):");
            var description = AnsiConsole.Ask<string>($"Description for {exerciseType}:");
            var bpm = AnsiConsole.Ask<int>("BPM (optional, 0 to skip):");

            var exercise = bpm > 0
                ? $"  {exerciseType} {exerciseDuration}: \"{description}\" at {bpm} bpm"
                : $"  {exerciseType} {exerciseDuration}: \"{description}\"";

            exercises.Add(exercise);
        }

        var customRoutine = $@"session ""{sessionName}"" {duration} minutes {skillLevel} {{
{string.Join("\n", exercises)}
}}";

        AnsiConsole.MarkupLine("\n[yellow]Generated Practice Routine:[/]");
        AnsiConsole.Write(new Panel(customRoutine).BorderColor(Color.Blue));

        var result = PracticeRoutineParser.parse(customRoutine);

        if (result.IsOk)
        {
            AnsiConsole.MarkupLine("[green]✓ Custom routine is valid![/]");
        }
        else
        {
            var error = result.ErrorValue;
            AnsiConsole.MarkupLine($"[red]✗ Validation error: {error}[/]");
        }

        logger.LogInformation("Created custom practice routine: {SessionName}", sessionName);
    }

    private static async Task ValidateSyntaxAsync(ILogger logger)
    {
        AnsiConsole.Write(new Rule("[purple]Validate Practice Routine Syntax[/]").LeftJustified());

        var input = AnsiConsole.Ask<string>("Enter practice routine DSL code:");

        var result = PracticeRoutineParser.parse(input);

        if (result.IsOk)
        {
            AnsiConsole.MarkupLine("[green]✓ Syntax is valid![/]");
        }
        else
        {
            var error = result.ErrorValue;
            AnsiConsole.MarkupLine($"[red]✗ Parse error: {error}[/]");
        }

        logger.LogInformation("Validated practice routine syntax");
    }

    private static void ShowGrammarExamples()
    {
        AnsiConsole.Write(new Rule("[cyan]DSL Grammar Examples[/]").LeftJustified());

        var examples = new[]
        {
            ("Basic Session", @"session ""daily_practice"" 30 minutes beginner {
  warmup 5: ""finger exercises""
  scales 10: ""C major scale""
  cooldown 5: ""stretching""
}"),

            ("With Timing", @"session ""tempo_practice"" 25 minutes intermediate {
  technique 15: ""alternate picking"" at 120 bpm
  scales 10: ""pentatonic"" at start 80 increase_to 100 bpm
}"),

            ("With Difficulty", @"session ""challenge"" 40 minutes advanced {
  technique 20: ""sweep picking"" at 140 bpm difficulty hard
  improvisation 20: ""jazz improv"" difficulty 85%
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

    private static async Task InteractiveBuilderAsync(ILogger logger)
    {
        AnsiConsole.Write(new Rule("[yellow]Interactive Practice Session Builder[/]").LeftJustified());

        AnsiConsole.MarkupLine("[dim]This feature will guide you through building a complete practice session...[/]");

        // Simulate interactive building
        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Building practice session[/]");

                for (var i = 0; i <= 100; i += 10)
                {
                    task.Value = i;
                    await Task.Delay(100);
                }
            });

        var builtSession = @"session ""guided_practice"" 35 minutes intermediate {
  warmup 5: ""chromatic exercises"" at 70 bpm
  technique 15: ""hammer-ons and pull-offs"" at 90 bpm
  scales 10: ""minor pentatonic"" at 100 bpm
  cooldown 5: ""finger stretches""
}";

        AnsiConsole.Write(
            new Panel(builtSession)
                .Header("[bold green]Generated Practice Session[/]")
                .BorderColor(Color.Green));

        logger.LogInformation("Built interactive practice session");
    }

    // Helper method removed due to F# interop complexity
}
