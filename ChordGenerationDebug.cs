using System;
using System.Linq;
using GA.Business.Core.Chords;
using GA.Business.Core.Scales;

class ChordGenerationDebug
{
    static void Main()
    {
        Console.WriteLine("=== CHORD GENERATION DEBUG ===");
        
        // Test with Ionian mode
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);
        Console.WriteLine($"Scale: {ionianMode.Name}");
        Console.WriteLine($"Notes: {string.Join(", ", ionianMode.Notes)}");
        Console.WriteLine($"Scale Length: {ionianMode.Notes.Count}");
        
        // Generate quartal triads
        Console.WriteLine("\n=== QUARTAL TRIADS ===");
        var quartalTriads = ChordTemplateFactory.GenerateFromScaleMode(ionianMode)
            .Where(c => c.StackingType == ChordStackingType.Quartal && c.Extension == ChordExtension.Triad)
            .ToList();
            
        Console.WriteLine($"Count: {quartalTriads.Count}");
        foreach (var chord in quartalTriads.Take(3))
        {
            Console.WriteLine($"- {chord.Name}: {chord.NoteCount} notes, Intervals: {string.Join(", ", chord.Intervals)}");
        }
        
        // Generate quartal sevenths
        Console.WriteLine("\n=== QUARTAL SEVENTHS ===");
        var quartalSevenths = ChordTemplateFactory.GenerateFromScaleMode(ionianMode)
            .Where(c => c.StackingType == ChordStackingType.Quartal && c.Extension == ChordExtension.Seventh)
            .ToList();
            
        Console.WriteLine($"Count: {quartalSevenths.Count}");
        foreach (var chord in quartalSevenths.Take(3))
        {
            Console.WriteLine($"- {chord.Name}: {chord.NoteCount} notes, Intervals: {string.Join(", ", chord.Intervals)}");
        }
        
        // Test all chords from this mode
        Console.WriteLine("\n=== ALL CHORDS FROM IONIAN ===");
        var allChords = ChordTemplateFactory.GenerateFromScaleMode(ionianMode).ToList();
        Console.WriteLine($"Total chords generated: {allChords.Count}");
        
        var byStacking = allChords.GroupBy(c => c.StackingType).ToList();
        foreach (var group in byStacking)
        {
            Console.WriteLine($"- {group.Key}: {group.Count()} chords");
        }
    }
}
