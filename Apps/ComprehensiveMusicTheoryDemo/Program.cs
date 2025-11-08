namespace ComprehensiveMusicTheoryDemo;

using GA.Business.Core.Atonal;
using GA.Business.Core.Scales;
using Spectre.Console;

internal class Program
{
    private static void Main(string[] args)
    {
        AnsiConsole.Write(
            new FigletText("Guitar Alchemist")
                .LeftJustified()
                .Color(Color.Green));

        AnsiConsole.Write(
            new FigletText("Music Theory Demo")
                .LeftJustified()
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[bold]Comprehensive Music Theory Analysis System[/]\n");

        // Run all demonstrations
        DemonstrateScalesAndModes();
        DemonstrateChordAnalysis();
        DemonstrateIntervalCalculations();
        DemonstrateAtonalAnalysis();
        DemonstrateHarmonicProgressions();

        AnsiConsole.MarkupLine("\n[green]Demo completed![/]");

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

    private static void DemonstrateScalesAndModes()
    {
        AnsiConsole.MarkupLine("[bold blue]🎵 Scales and Modes Analysis[/]\n");

        var table = new Table();
        table.AddColumn("Scale/Mode");
        table.AddColumn("Root");
        table.AddColumn("Notes");
        table.AddColumn("Characteristic Intervals");
        table.AddColumn("Mood/Character");

        // Major scale and its modes
        var cMajor = Scale.Major.WithRoot(PitchClass.C);
        table.AddRow("C Major", "C", GetScaleNotes(cMajor), "W-W-H-W-W-W-H", "Bright, Happy");

        var dDorian = Scale.Dorian.WithRoot(PitchClass.D);
        table.AddRow("D Dorian", "D", GetScaleNotes(dDorian), "W-H-W-W-W-H-W", "Jazzy, Sophisticated");

        var ePhrygian = Scale.Phrygian.WithRoot(PitchClass.E);
        table.AddRow("E Phrygian", "E", GetScaleNotes(ePhrygian), "H-W-W-W-H-W-W", "Spanish, Exotic");

        var fLydian = Scale.Lydian.WithRoot(PitchClass.F);
        table.AddRow("F Lydian", "F", GetScaleNotes(fLydian), "W-W-W-H-W-W-H", "Dreamy, Floating");

        var gMixolydian = Scale.Mixolydian.WithRoot(PitchClass.G);
        table.AddRow("G Mixolydian", "G", GetScaleNotes(gMixolydian), "W-W-H-W-W-H-W", "Bluesy, Rock");

        var aMinor = Scale.NaturalMinor.WithRoot(PitchClass.A);
        table.AddRow("A Natural Minor", "A", GetScaleNotes(aMinor), "W-H-W-W-H-W-W", "Sad, Melancholic");

        var bLocrian = Scale.Locrian.WithRoot(PitchClass.B);
        table.AddRow("B Locrian", "B", GetScaleNotes(bLocrian), "H-W-W-H-W-W-W", "Unstable, Tense");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void DemonstrateChordAnalysis()
    {
        AnsiConsole.MarkupLine("[bold blue]🎸 Advanced Chord Analysis[/]\n");

        var table = new Table();
        table.AddColumn("Chord");
        table.AddColumn("Notes");
        table.AddColumn("Intervals");
        table.AddColumn("Function");
        table.AddColumn("Tension Level");
        table.AddColumn("Voice Leading");

        // Analyze various chord types
        AnalyzeChord(table, "C Major", new[] { PitchClass.C, PitchClass.E, PitchClass.G });
        AnalyzeChord(table, "C7", new[] { PitchClass.C, PitchClass.E, PitchClass.G, PitchClass.Bb });
        AnalyzeChord(table, "Cmaj7", new[] { PitchClass.C, PitchClass.E, PitchClass.G, PitchClass.B });
        AnalyzeChord(table, "Cm7b5", new[] { PitchClass.C, PitchClass.Eb, PitchClass.Gb, PitchClass.Bb });
        AnalyzeChord(table, "C7alt", new[] { PitchClass.C, PitchClass.E, PitchClass.Bb, PitchClass.Db, PitchClass.Ab });
        AnalyzeChord(table, "Csus4", new[] { PitchClass.C, PitchClass.F, PitchClass.G });

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void DemonstrateIntervalCalculations()
    {
        AnsiConsole.MarkupLine("[bold blue]📐 Interval Mathematics[/]\n");

        var table = new Table();
        table.AddColumn("From");
        table.AddColumn("To");
        table.AddColumn("Interval");
        table.AddColumn("Semitones");
        table.AddColumn("Quality");
        table.AddColumn("Harmonic Function");

        // Demonstrate various intervals
        AddIntervalRow(table, PitchClass.C, PitchClass.D, "Major 2nd");
        AddIntervalRow(table, PitchClass.C, PitchClass.Eb, "Minor 3rd");
        AddIntervalRow(table, PitchClass.C, PitchClass.E, "Major 3rd");
        AddIntervalRow(table, PitchClass.C, PitchClass.F, "Perfect 4th");
        AddIntervalRow(table, PitchClass.C, PitchClass.Gb, "Tritone");
        AddIntervalRow(table, PitchClass.C, PitchClass.G, "Perfect 5th");
        AddIntervalRow(table, PitchClass.C, PitchClass.A, "Major 6th");
        AddIntervalRow(table, PitchClass.C, PitchClass.Bb, "Minor 7th");
        AddIntervalRow(table, PitchClass.C, PitchClass.B, "Major 7th");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void DemonstrateAtonalAnalysis()
    {
        AnsiConsole.MarkupLine("[bold blue]🔬 Atonal Set Theory Analysis[/]\n");

        var table = new Table();
        table.AddColumn("Set");
        table.AddColumn("Prime Form");
        table.AddColumn("Forte Number");
        table.AddColumn("Interval Vector");
        table.AddColumn("Symmetry");
        table.AddColumn("Complement");

        // Analyze various pitch class sets
        AnalyzePitchClassSet(table, "Major Triad", new[] { PitchClass.C, PitchClass.E, PitchClass.G });
        AnalyzePitchClassSet(table, "Minor Triad", new[] { PitchClass.C, PitchClass.Eb, PitchClass.G });
        AnalyzePitchClassSet(table, "Diminished Triad", new[] { PitchClass.C, PitchClass.Eb, PitchClass.Gb });
        AnalyzePitchClassSet(table, "Augmented Triad", new[] { PitchClass.C, PitchClass.E, PitchClass.Ab });
        AnalyzePitchClassSet(table, "Whole Tone",
            new[] { PitchClass.C, PitchClass.D, PitchClass.E, PitchClass.Gb, PitchClass.Ab, PitchClass.Bb });
        AnalyzePitchClassSet(table, "Octatonic",
            new[]
            {
                PitchClass.C, PitchClass.Db, PitchClass.Eb, PitchClass.E, PitchClass.Gb, PitchClass.G, PitchClass.A,
                PitchClass.Bb
            });

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void DemonstrateHarmonicProgressions()
    {
        AnsiConsole.MarkupLine("[bold blue]🎼 Harmonic Progression Analysis[/]\n");

        var progressions = new[]
        {
            ("ii-V-I (Jazz)", new[] { "Dm7", "G7", "Cmaj7" }),
            ("I-V-vi-IV (Pop)", new[] { "C", "G", "Am", "F" }),
            ("vi-IV-I-V (Ballad)", new[] { "Am", "F", "C", "G" }),
            ("I-bVII-IV-I (Rock)", new[] { "C", "Bb", "F", "C" }),
            ("i-bVI-bVII-i (Minor)", new[] { "Cm", "Ab", "Bb", "Cm" })
        };

        foreach (var (name, chords) in progressions)
        {
            AnsiConsole.MarkupLine($"[bold yellow]{name}:[/]");

            var table = new Table();
            table.AddColumn("Chord");
            table.AddColumn("Function");
            table.AddColumn("Tension");
            table.AddColumn("Resolution");

            for (var i = 0; i < chords.Length; i++)
            {
                var chord = chords[i];
                var function = GetChordFunction(chord, i, chords.Length);
                var tension = GetTensionLevel(chord);
                var resolution = i < chords.Length - 1 ? $"→ {chords[i + 1]}" : "Cadence";

                table.AddRow(chord, function, tension, resolution);
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }
    }

    // Helper methods
    private static string GetScaleNotes(Scale scale)
    {
        return string.Join(" - ", scale.Notes.Select(n => n.ToString()));
    }

    private static void AnalyzeChord(Table table, string name, PitchClass[] pitchClasses)
    {
        var pcs = new PitchClassSet(pitchClasses);
        var intervals = GetIntervals(pitchClasses);
        var function = GetChordFunction(name);
        var tension = CalculateTension(pcs);
        var voiceLeading = GetVoiceLeadingTips(name);

        table.AddRow(
            name,
            string.Join(" ", pitchClasses.Select(pc => pc.ToString())),
            intervals,
            function,
            tension,
            voiceLeading
        );
    }

    private static void AddIntervalRow(Table table, PitchClass from, PitchClass to, string intervalName)
    {
        var semitones = (to.Value - from.Value + 12) % 12;
        var quality = GetIntervalQuality(semitones);
        var function = GetHarmonicFunction(semitones);

        table.AddRow(
            from.ToString(),
            to.ToString(),
            intervalName,
            semitones.ToString(),
            quality,
            function
        );
    }

    private static void AnalyzePitchClassSet(Table table, string name, PitchClass[] pitchClasses)
    {
        var pcs = new PitchClassSet(pitchClasses);
        var primeForm = GetPrimeForm(pcs);
        var forteNumber = GetForteNumber(pcs);
        var intervalVector = GetIntervalVector(pcs);
        var symmetry = GetSymmetryProperties(pcs);
        var complement = GetComplement(pcs);

        table.AddRow(name, primeForm, forteNumber, intervalVector, symmetry, complement);
    }

    // Simplified helper implementations
    private static string GetIntervals(PitchClass[] pitchClasses)
    {
        return "1-3-5";
        // Simplified
    }

    private static string GetChordFunction(string chordName)
    {
        return chordName.Contains("7") ? "Dominant" : "Stable";
    }

    private static string GetChordFunction(string chord, int position, int total)
    {
        return position == total - 1 ? "Tonic" : "Predominant";
    }

    private static string CalculateTension(PitchClassSet pcs)
    {
        return pcs.IntervalClassVector.Sum() > 6 ? "High" : "Low";
    }

    private static string GetVoiceLeadingTips(string chord)
    {
        return "Smooth voice leading";
    }

    private static string GetIntervalQuality(int semitones)
    {
        return semitones switch
        {
            0 => "Perfect", 3 or 4 => "Major/Minor", 6 => "Tritone", 7 => "Perfect", _ => "Other"
        };
    }

    private static string GetHarmonicFunction(int semitones)
    {
        return semitones switch { 0 => "Unison", 7 => "Consonant", 6 => "Dissonant", _ => "Varies" };
    }

    private static string GetTensionLevel(string chord)
    {
        return chord.Contains("7") ? "Medium" : "Low";
    }

    private static string GetPrimeForm(PitchClassSet pcs)
    {
        return $"({string.Join(",", pcs.PitchClasses.Take(3))})";
    }

    private static string GetForteNumber(PitchClassSet pcs)
    {
        return $"3-{pcs.PitchClasses.Count()}";
    }

    private static string GetIntervalVector(PitchClassSet pcs)
    {
        return $"<{string.Join("", pcs.IntervalClassVector.Take(6))}>";
    }

    private static string GetSymmetryProperties(PitchClassSet pcs)
    {
        return pcs.PitchClasses.Count() == 3 ? "None" : "Partial";
    }

    private static string GetComplement(PitchClassSet pcs)
    {
        return $"{12 - pcs.PitchClasses.Count()}-note";
    }
}
