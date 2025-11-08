namespace GA.Business.Core.Examples;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GA.Business.Core.Spatial;
using GA.Business.Core.Atonal;
using GA.Business.Core.Notes;

/// <summary>
/// Simple, working demo of BSP core functionality
/// </summary>
public class SimpleBspDemo
{
    /// <summary>
    /// Run a basic demonstration of BSP capabilities
    /// </summary>
    public static async Task RunDemo()
    {
        Console.WriteLine("ðŸŽµ Guitar Alchemist - Simple BSP Demo ðŸŽµ");
        Console.WriteLine("==========================================");
        
        try
        {
            await DemoBasicBSPTree();
            await DemoSpatialQueries();
            await DemoTonalRegions();
            
            Console.WriteLine("\nâœ… Simple BSP Demo Complete!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nâŒ Demo failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Demo basic BSP tree functionality
    /// </summary>
    private static async Task DemoBasicBSPTree()
    {
        Console.WriteLine("\n=== ðŸŒ³ BSP Tree Demo ===");

        // Create a BSP tree
        var tree = new TonalBspTree();
        Console.WriteLine($"âœ“ Created BSP tree with root region: {tree.Root.Region.Name}");

        // Test basic tree operations
        var cMajorTriad = new PitchClassSet([PitchClass.C, PitchClass.E, PitchClass.G]);
        var region = tree.FindTonalRegion(cMajorTriad);
        
        Console.WriteLine($"âœ“ Found tonal region for C major triad: {region.Name}");
        Console.WriteLine($"  - Tonality: {region.TonalityType}");
        Console.WriteLine($"  - Tonal Center: {region.TonalCenter}");
        Console.WriteLine($"  - Contains C major triad: {region.Contains(cMajorTriad)}");

        await Task.Delay(100); // Simulate async work
    }

    /// <summary>
    /// Demo spatial queries
    /// </summary>
    private static async Task DemoSpatialQueries()
    {
        Console.WriteLine("\n=== ðŸŒŒ Spatial Query Demo ===");

        // Create BSP service
        var bspService = new TonalBspService(null); // No logger for simple demo

        // Query for elements near C major triad
        var queryCenter = new PitchClassSet([PitchClass.C, PitchClass.E, PitchClass.G]);
        var radius = 0.5;
        var strategy = TonalPartitionStrategy.CircleOfFifths;

        Console.WriteLine($"ðŸ” Searching for elements within {radius} units of C major triad...");
        Console.WriteLine($"   Strategy: {strategy}");

        var result = bspService.SpatialQuery(queryCenter, radius, strategy);
        
        Console.WriteLine($"âœ“ Query completed in {result.QueryTime.TotalMilliseconds:F2}ms");
        Console.WriteLine($"âœ“ Found {result.Elements.Count} elements");
        
        // Show first few elements
        foreach (var element in result.Elements.Take(3))
        {
            Console.WriteLine($"  â€¢ {element.Name} ({element.TonalityType})");
        }

        await Task.Delay(100); // Simulate async work
    }

    /// <summary>
    /// Demo tonal regions and relationships
    /// </summary>
    private static async Task DemoTonalRegions()
    {
        Console.WriteLine("\n=== ðŸ—ºï¸  Tonal Regions Demo ===");

        // Create some tonal regions
        var cMajor = new TonalRegion(
            "C Major",
            TonalityType.Major,
            new PitchClassSet([PitchClass.C, PitchClass.D, PitchClass.E, PitchClass.F, 
                              PitchClass.G, PitchClass.A, PitchClass.B]),
            (int)PitchClass.C
        );

        var aMinor = new TonalRegion(
            "A Minor",
            TonalityType.Minor,
            new PitchClassSet([PitchClass.A, PitchClass.B, PitchClass.C, PitchClass.D, 
                              PitchClass.E, PitchClass.F, PitchClass.G]),
            (int)PitchClass.A
        );

        Console.WriteLine($"âœ“ Created tonal regions:");
        Console.WriteLine($"  â€¢ {cMajor.Name} - {cMajor.TonalityType}");
        Console.WriteLine($"  â€¢ {aMinor.Name} - {aMinor.TonalityType}");

        // Test chord containment
        var cMajorTriad = new PitchClassSet([PitchClass.C, PitchClass.E, PitchClass.G]);
        var aMinorTriad = new PitchClassSet([PitchClass.A, PitchClass.C, PitchClass.E]);

        Console.WriteLine($"\nðŸŽ¸ Chord containment tests:");
        Console.WriteLine($"  â€¢ C major triad in C Major region: {cMajor.Contains(cMajorTriad)}");
        Console.WriteLine($"  â€¢ C major triad in A Minor region: {aMinor.Contains(cMajorTriad)}");
        Console.WriteLine($"  â€¢ A minor triad in C Major region: {cMajor.Contains(aMinorTriad)}");
        Console.WriteLine($"  â€¢ A minor triad in A Minor region: {aMinor.Contains(aMinorTriad)}");

        // Demo tonal elements
        var cMajorChord = new TonalChord(
            "C Major",
            cMajorTriad,
            TonalityType.Major,
            (int)PitchClass.C
        );

        var aMinorChord = new TonalChord(
            "A Minor",
            aMinorTriad,
            TonalityType.Minor,
            (int)PitchClass.A
        );

        Console.WriteLine($"\nðŸŽµ Tonal elements:");
        Console.WriteLine($"  â€¢ {cMajorChord.Name}: {cMajorChord.TonalityType} at {cMajorChord.TonalCenter}");
        Console.WriteLine($"  â€¢ {aMinorChord.Name}: {aMinorChord.TonalityType} at {aMinorChord.TonalCenter}");

        await Task.Delay(100); // Simulate async work
    }

    /// <summary>
    /// Demo progression analysis using basic BSP concepts
    /// </summary>
    public static async Task DemoBasicProgression()
    {
        Console.WriteLine("\n=== ðŸŽ¼ Basic Progression Analysis ===");

        // Create a simple progression: C - Am - F - G
        var progression = new List<(string name, PitchClassSet pitchClassSet)>
        {
            ("C Major", new PitchClassSet([PitchClass.C, PitchClass.E, PitchClass.G])),
            ("A Minor", new PitchClassSet([PitchClass.A, PitchClass.C, PitchClass.E])),
            ("F Major", new PitchClassSet([PitchClass.F, PitchClass.A, PitchClass.C])),
            ("G Major", new PitchClassSet([PitchClass.G, PitchClass.B, PitchClass.D]))
        };

        Console.WriteLine("ðŸŽµ Analyzing progression: C - Am - F - G");

        var bspService = new TonalBspService(null);
        
        // Analyze each chord's tonal context
        foreach (var (name, pitchClassSet) in progression)
        {
            var context = bspService.FindTonalContextForChord(pitchClassSet);
            Console.WriteLine($"  â€¢ {name}: Region = {context.Region.Name}, Confidence = {context.Confidence:F2}");
        }

        // Calculate spatial distances between adjacent chords
        Console.WriteLine("\nðŸ“ Spatial distances:");
        for (var i = 0; i < progression.Count - 1; i++)
        {
            var current = progression[i];
            var next = progression[i + 1];
            
            var distance = CalculateSimpleSpatialDistance(current.pitchClassSet, next.pitchClassSet);
            Console.WriteLine($"  {current.name} â†’ {next.name}: {distance:F3} units");
        }

        await Task.Delay(100); // Simulate async work
    }

    /// <summary>
    /// Simple spatial distance calculation
    /// </summary>
    private static double CalculateSimpleSpatialDistance(PitchClassSet set1, PitchClassSet set2)
    {
        var intersection = set1.Intersect(set2).Count();
        var union = set1.Union(set2).Count();
        
        return union > 0 ? 1.0 - (double)intersection / union : 1.0;
    }

    /// <summary>
    /// Entry point for console application
    /// </summary>
    public static async Task Main(string[] args)
    {
        await RunDemo();
        await DemoBasicProgression();
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
