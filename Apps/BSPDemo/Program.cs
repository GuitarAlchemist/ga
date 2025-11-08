namespace BSPDemo;

/// <summary>
///     Simple pitch class for demonstration
/// </summary>
public enum PitchClass
{
    C = 0,
    CSharp = 1,
    D = 2,
    DSharp = 3,
    E = 4,
    F = 5,
    FSharp = 6,
    G = 7,
    GSharp = 8,
    A = 9,
    ASharp = 10,
    B = 11
}

/// <summary>
///     Simple pitch class set for demonstration
/// </summary>
public class PitchClassSet : HashSet<PitchClass>
{
    public PitchClassSet(IEnumerable<PitchClass> pitchClasses) : base(pitchClasses)
    {
    }

    public override string ToString()
    {
        return string.Join(", ", this.OrderBy(pc => (int)pc));
    }
}

/// <summary>
///     Simple BSP node for demonstration
/// </summary>
public class BSPNode
{
    public BSPNode(string name, PitchClassSet region)
    {
        Name = name;
        Region = region;
    }

    public string Name { get; set; }
    public PitchClassSet Region { get; set; }
    public BSPNode? Left { get; set; }
    public BSPNode? Right { get; set; }
}

/// <summary>
///     Simple console application demonstrating BSP concepts
/// </summary>
internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("🎵 Guitar Alchemist - BSP Concept Demo 🎵");
        Console.WriteLine("==========================================");

        try
        {
            await RunBasicBSPDemo();
            Console.WriteLine("\n✅ BSP Demo completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Demo failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        Console.WriteLine("\nDemo completed!");
    }

    private static async Task RunBasicBSPDemo()
    {
        Console.WriteLine("\n=== 🎵 Basic Musical Analysis Demo ===");

        // Create some basic pitch class sets
        var cMajorTriad = new PitchClassSet([PitchClass.C, PitchClass.E, PitchClass.G]);
        var aMinorTriad = new PitchClassSet([PitchClass.A, PitchClass.C, PitchClass.E]);
        var fMajorTriad = new PitchClassSet([PitchClass.F, PitchClass.A, PitchClass.C]);
        var gMajorTriad = new PitchClassSet([PitchClass.G, PitchClass.B, PitchClass.D]);

        Console.WriteLine("✓ Created basic triads:");
        Console.WriteLine($"  • C Major: {cMajorTriad}");
        Console.WriteLine($"  • A Minor: {aMinorTriad}");
        Console.WriteLine($"  • F Major: {fMajorTriad}");
        Console.WriteLine($"  • G Major: {gMajorTriad}");

        Console.WriteLine("\n=== 🔍 Set Theory Analysis ===");

        // Calculate set similarities
        Console.WriteLine("Set Relationships:");
        var cAmIntersection = cMajorTriad.Intersect(aMinorTriad).Count();
        var cAmUnion = cMajorTriad.Union(aMinorTriad).Count();
        var similarity = cAmUnion > 0 ? (double)cAmIntersection / cAmUnion : 0.0;

        Console.WriteLine($"  • C Major ∩ A Minor: {cAmIntersection} common tones");
        Console.WriteLine($"  • Similarity: {similarity:F2}");

        // Show all intersections
        Console.WriteLine("\n📊 All Chord Relationships:");
        var chords = new[]
        {
            ("C Major", cMajorTriad),
            ("A Minor", aMinorTriad),
            ("F Major", fMajorTriad),
            ("G Major", gMajorTriad)
        };

        for (var i = 0; i < chords.Length; i++)
        {
            for (var j = i + 1; j < chords.Length; j++)
            {
                var intersection = chords[i].Item2.Intersect(chords[j].Item2).Count();
                Console.WriteLine($"  • {chords[i].Item1} ∩ {chords[j].Item1}: {intersection} common tones");
            }
        }

        Console.WriteLine("\n=== 🌳 BSP Tree Concept Demo ===");

        // Create a simple BSP tree for tonal regions
        var rootRegion = new PitchClassSet([
            PitchClass.C, PitchClass.D, PitchClass.E, PitchClass.F,
            PitchClass.G, PitchClass.A, PitchClass.B
        ]); // C Major scale
        var root = new BSPNode("C Major Region", rootRegion);

        // Add child regions
        var majorTriadRegion = new PitchClassSet([PitchClass.C, PitchClass.E, PitchClass.G]);
        var minorTriadRegion = new PitchClassSet([PitchClass.A, PitchClass.C, PitchClass.E]);

        root.Left = new BSPNode("Major Triads", majorTriadRegion);
        root.Right = new BSPNode("Minor Triads", minorTriadRegion);

        Console.WriteLine("✓ Created BSP tree:");
        Console.WriteLine($"  Root: {root.Name} - {root.Region}");
        Console.WriteLine($"  Left: {root.Left.Name} - {root.Left.Region}");
        Console.WriteLine($"  Right: {root.Right.Name} - {root.Right.Region}");

        Console.WriteLine("\n=== 🎼 Progression Analysis ===");

        // Create a simple progression: C - Am - F - G
        var progression = new List<(string name, PitchClassSet pitchClassSet)>
        {
            ("C Major", cMajorTriad),
            ("A Minor", aMinorTriad),
            ("F Major", fMajorTriad),
            ("G Major", gMajorTriad)
        };

        Console.WriteLine("🎵 Analyzing progression: C - Am - F - G");

        // Calculate spatial distances between adjacent chords
        Console.WriteLine("\n📏 Chord Relationships:");
        for (var i = 0; i < progression.Count - 1; i++)
        {
            var current = progression[i];
            var next = progression[i + 1];

            var distance = CalculateSimpleSpatialDistance(current.pitchClassSet, next.pitchClassSet);
            var commonTones = current.pitchClassSet.Intersect(next.pitchClassSet).Count();

            Console.WriteLine($"  {current.name} → {next.name}:");
            Console.WriteLine($"    - Distance: {distance:F3} units");
            Console.WriteLine($"    - Common tones: {commonTones}");
        }

        Console.WriteLine("\n=== 🎯 BSP Spatial Queries ===");

        // Demonstrate spatial queries using BSP concepts
        Console.WriteLine("Spatial Query: Find chords similar to C Major");

        var queryChord = cMajorTriad;
        var candidates = new[] { aMinorTriad, fMajorTriad, gMajorTriad };

        var results = candidates
            .Select(chord => new
            {
                Chord = chord,
                Distance = CalculateSimpleSpatialDistance(queryChord, chord),
                CommonTones = queryChord.Intersect(chord).Count()
            })
            .OrderBy(r => r.Distance)
            .ToList();

        Console.WriteLine($"Query chord: {queryChord}");
        Console.WriteLine("Results (ordered by similarity):");

        foreach (var result in results)
        {
            var chordName = GetChordName(result.Chord, chords);
            Console.WriteLine($"  • {chordName}: distance={result.Distance:F3}, common tones={result.CommonTones}");
        }

        Console.WriteLine("\n=== 🔄 BSP Tree Traversal ===");

        // Demonstrate tree traversal
        Console.WriteLine("Tree traversal to find best region for A Minor chord:");
        var testChord = aMinorTriad;

        Console.WriteLine($"Testing chord: {testChord}");
        Console.WriteLine($"Root region ({root.Name}): contains {CountContainedNotes(testChord, root.Region)} notes");
        Console.WriteLine(
            $"Left region ({root.Left.Name}): contains {CountContainedNotes(testChord, root.Left.Region)} notes");
        Console.WriteLine(
            $"Right region ({root.Right.Name}): contains {CountContainedNotes(testChord, root.Right.Region)} notes");

        // Determine best fit
        var leftFit = CountContainedNotes(testChord, root.Left.Region);
        var rightFit = CountContainedNotes(testChord, root.Right.Region);
        var bestRegion = leftFit > rightFit ? root.Left : root.Right;

        Console.WriteLine(
            $"Best fit: {bestRegion.Name} (contains {Math.Max(leftFit, rightFit)} out of {testChord.Count} notes)");

        await Task.Delay(100); // Simulate async work
    }

    /// <summary>
    ///     Simple spatial distance calculation
    /// </summary>
    private static double CalculateSimpleSpatialDistance(PitchClassSet set1, PitchClassSet set2)
    {
        var intersection = set1.Intersect(set2).Count();
        var union = set1.Union(set2).Count();

        return union > 0 ? 1.0 - (double)intersection / union : 1.0;
    }

    /// <summary>
    ///     Get chord name from chord list
    /// </summary>
    private static string GetChordName(PitchClassSet chord, (string, PitchClassSet)[] chords)
    {
        var match = chords.FirstOrDefault(c => c.Item2.SetEquals(chord));
        return match.Item1 ?? "Unknown";
    }

    /// <summary>
    ///     Count how many notes from the test chord are contained in the region
    /// </summary>
    private static int CountContainedNotes(PitchClassSet testChord, PitchClassSet region)
    {
        return testChord.Intersect(region).Count();
    }
}
