using GA.Business.Core.Chords;
using GA.Business.Core.Tonal.Modes.Diatonic;
using GA.Business.Core.Tonal.Primitives.Diatonic;

namespace GA.Business.Core.Demo;

/// <summary>
/// Demonstrates the new fully programmatic chord generation system
/// that preserves complete intervallic and harmonic context from parent scales.
/// </summary>
public static class ProgrammaticChordDemo
{
    public static void RunDemo()
    {
        Console.WriteLine("🎵 PROGRAMMATIC CHORD GENERATION DEMO 🎵");
        Console.WriteLine("========================================");
        Console.WriteLine();

        // 1. Generate chords from any scale mode
        DemonstrateModalChordGeneration();
        
        // 2. Generate chords by name (traditional approach)
        DemonstrateChordByName();
        
        // 3. Generate chords from interval patterns
        DemonstrateIntervalPatterns();
        
        // 4. Generate comprehensive chord libraries
        DemonstrateChordLibraries();
        
        // 5. Show preserved intervallic information
        DemonstrateIntervalPreservation();
    }

    private static void DemonstrateModalChordGeneration()
    {
        Console.WriteLine("1. 🎼 MODAL CHORD GENERATION");
        Console.WriteLine("   Generating chords from C Dorian mode...");
        
        var dorianMode = MajorScaleMode.Get(MajorScaleDegree.Dorian);
        var diatonicChords = ChordTemplateFactory.CreateDiatonicChords(dorianMode);
        
        foreach (var chord in diatonicChords)
        {
            if (chord is ChordTemplate.TonalModal tonalChord)
            {
                Console.WriteLine($"   • Degree {tonalChord.ScaleDegree}: {chord.Name} ({chord.GetSymbolSuffix()}) - {tonalChord.HarmonicFunction}");
            }
        }
        Console.WriteLine();
    }

    private static void DemonstrateChordByName()
    {
        Console.WriteLine("2. 🎯 CHORD BY NAME LOOKUP");
        Console.WriteLine("   Getting traditional chord types...");
        
        var chordNames = new[] { "Major", "Minor", "Dominant7", "Major9", "Sus4" };
        
        foreach (var name in chordNames)
        {
            var chord = ChordTemplateFactory.GetChordByName(name);
            if (chord != null)
            {
                Console.WriteLine($"   • {name}: {chord.Quality} quality, {chord.Extension} extension, {chord.NoteCount} notes");
            }
        }
        Console.WriteLine();
    }

    private static void DemonstrateIntervalPatterns()
    {
        Console.WriteLine("3. 🔢 INTERVAL PATTERN CREATION");
        Console.WriteLine("   Creating chords from interval patterns...");
        
        // Create a custom chord from semitones
        var customChord = ChordTemplateFactory.FromSemitones("MyCustomChord", 4, 7, 10, 14);
        Console.WriteLine($"   • Custom chord: {customChord.Name} with intervals: {string.Join(", ", customChord.Intervals.Select(i => i.Interval.Semitones.Value))}");
        
        // Create from interval names
        var namedIntervalChord = ChordTemplateFactory.FromIntervalNames("NamedChord", "M3", "P5", "m7");
        Console.WriteLine($"   • Named interval chord: {namedIntervalChord.Name} ({namedIntervalChord.GetSymbolSuffix()})");
        Console.WriteLine();
    }

    private static void DemonstrateChordLibraries()
    {
        Console.WriteLine("4. 📚 COMPREHENSIVE CHORD LIBRARIES");
        Console.WriteLine("   Generating traditional chord library...");
        
        var traditionalChords = ChordTemplateFactory.CreateTraditionalChordLibrary().Take(10);
        
        foreach (var chord in traditionalChords)
        {
            Console.WriteLine($"   • {chord.Name} ({chord.GetConstructionType()}) - {chord.GetDescription()}");
        }
        
        var totalCount = ChordTemplateFactory.CreateTraditionalChordLibrary().Count();
        Console.WriteLine($"   Total chords generated: {totalCount}");
        Console.WriteLine();
    }

    private static void DemonstrateIntervalPreservation()
    {
        Console.WriteLine("5. 🧬 INTERVALLIC INFORMATION PRESERVATION");
        Console.WriteLine("   Showing preserved harmonic context...");
        
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);
        var dominantChord = ChordTemplateFactory.CreateModalChords(ionianMode, ChordExtension.Seventh)
            .OfType<ChordTemplate.TonalModal>()
            .First(c => c.ScaleDegree == 5); // V7 chord
        
        Console.WriteLine($"   • Chord: {dominantChord.Name}");
        Console.WriteLine($"   • Parent Scale: {dominantChord.ParentScale.Name}");
        Console.WriteLine($"   • Scale Degree: {dominantChord.ScaleDegree}");
        Console.WriteLine($"   • Harmonic Function: {dominantChord.HarmonicFunction}");
        Console.WriteLine($"   • Stacking Type: {dominantChord.StackingType}");
        Console.WriteLine($"   • Intervals from root:");
        
        foreach (var interval in dominantChord.Intervals)
        {
            Console.WriteLine($"     - {interval.Function}: {interval.Interval.Semitones.Value} semitones");
        }
        Console.WriteLine();
    }
}
