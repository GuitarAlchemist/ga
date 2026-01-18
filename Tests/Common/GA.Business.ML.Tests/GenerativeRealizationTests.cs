namespace GA.Business.ML.Tests;

using GA.Business.Core.Fretboard;
using GA.Business.Core.Fretboard.Analysis;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Notes;
using GA.Business.Core.Tonal;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Retrieval;
using GA.Business.ML.Tabs;
using GA.Business.Core.Player;
using NUnit.Framework;

[TestFixture]
public class GenerativeRealizationTests
{
    private FretboardPositionMapper _mapper;
    private PhysicalCostService _costService;

    [SetUp]
    public void Setup()
    {
        _mapper = new FretboardPositionMapper(Tuning.Default);
        _costService = new PhysicalCostService();
    }

    [Test]
    public void MapPitch_HighE_Note()
    {
        // High E4 (MIDI 64)
        var highE = Pitch.Sharp.Parse("E4");
        var positions = _mapper.MapPitch(highE).ToList();

        // 1. Open High E (String 1, Fret 0)
        // 2. B string 5th fret (String 2, Fret 5)
        // 3. G string 9th fret (String 3, Fret 9)
        // 4. D string 14th fret (String 4, Fret 14)
        // 5. A string 19th fret (String 5, Fret 19)
        // 6. Low E string 24th fret (String 6, Fret 24)
        
        Assert.That(positions.Count, Is.EqualTo(6));
        Assert.That(positions.Any(p => p.StringIndex.Value == 1 && p.Fret == 0));
        Assert.That(positions.Any(p => p.StringIndex.Value == 2 && p.Fret == 5));
    }

    [Test]
    public void MapChord_CMajor()
    {
        // C Major: C3, E3, G3
        var pitches = new Pitch[] { Pitch.Sharp.Parse("C3"), Pitch.Sharp.Parse("E3"), Pitch.Sharp.Parse("G3") };
        var realizations = _mapper.MapChord(pitches).ToList();

        Assert.That(realizations.Count, Is.GreaterThan(0));

        // Evaluate costs
        var ranked = realizations
            .Select(r => new { Shape = r, Cost = _costService.CalculateStaticCost(r) })
            .OrderBy(x => x.Cost.TotalCost)
            .ToList();

        var best = ranked.First();
        
        TestContext.WriteLine($"Best realization for C Major (C3, E3, G3):");
        foreach (var pos in best.Shape)
        {
            TestContext.WriteLine($"  String {pos.StringIndex.Value}, Fret {pos.Fret}");
        }
        TestContext.WriteLine($"Cost: {best.Cost.TotalCost:F2}");

        Assert.That(best.Shape.Any(p => p.Fret == 0), Is.True, "Should favor open strings for lowest cost");
    }

    [Test]
    public void Test_Ukulele_Mapping()
    {
        // Ukulele tuning: G4, C4, E4, A4
        var ukeMapper = new FretboardPositionMapper(Tuning.Ukulele);
        
        // Map C Major on Ukulele: C4, E4, G4
        var pitches = new[] { Pitch.Sharp.Parse("C4"), Pitch.Sharp.Parse("E4"), Pitch.Sharp.Parse("G4") };
        var realizations = ukeMapper.MapChord(pitches).ToList();

        // One standard realization is 0-0-0-3 (G-C-E-A) -> No, that's C major triad + A? 
        // Triad C-E-G: 0-0-0-x or similar.
        Assert.That(realizations.Any(), Is.True);
        
        var best = realizations
            .Select(r => new { Shape = r, Cost = _costService.CalculateStaticCost(r) })
            .OrderBy(x => x.Cost.TotalCost)
            .First();

        TestContext.WriteLine($"Best Ukulele C Major realization:");
        foreach (var pos in best.Shape)
        {
            TestContext.WriteLine($"  String {pos.StringIndex.Value}, Fret {pos.Fret}");
        }
    }

    [Test]
    public void Test_BeginnerProfile_AvoidsStretches()
    {
        // G Major voicing with a stretch: G2 (3), B2 (2), D3 (0), G3 (0), B3 (0), G4 (3)
        // vs G Major with a wide stretch: G2 (3), B2 (2), D3 (0), G3 (0), B3 (0), G4 (15) -- wait G4 is not 15
        
        var p1 = new FretboardPosition(Str.FromValue(6), 3, Pitch.Sharp.Parse("G2"));
        var p2 = new FretboardPosition(Str.FromValue(1), 10, Pitch.Sharp.Parse("D4")); // Span 7
        var shape = new List<FretboardPosition> { p1, p2 };

        var defaultCost = new PhysicalCostService(new PlayerProfile()).CalculateStaticCost(shape);
        var beginnerCost = new PhysicalCostService(PlayerProfile.Beginner()).CalculateStaticCost(shape);

        TestContext.WriteLine($"Default Cost: {defaultCost.TotalCost:F2}");
        TestContext.WriteLine($"Beginner Cost: {beginnerCost.TotalCost:F2}");

        Assert.That(beginnerCost.TotalCost, Is.GreaterThan(defaultCost.TotalCost), 
            "Beginner profile should have higher cost for wide stretches");
    }

    [Test]
    public void Test_TransitionCost_Smooth_vs_Jump()
    {
        // From: C Major Open (A-3, D-2, G-0)
        var cMajor = new List<FretboardPosition> {
            new(Str.FromValue(5), 3, Pitch.Sharp.Parse("C3")),
            new(Str.FromValue(4), 2, Pitch.Sharp.Parse("E3")),
            new(Str.FromValue(3), 0, Pitch.Sharp.Parse("G3"))
        };

        // Option A: Smooth move to G Major (E-3, A-2, D-0)
        var gMajorSmooth = new List<FretboardPosition> {
            new(Str.FromValue(6), 3, Pitch.Sharp.Parse("G2")),
            new(Str.FromValue(5), 2, Pitch.Sharp.Parse("B2")),
            new(Str.FromValue(4), 0, Pitch.Sharp.Parse("D3"))
        };

        // Option B: Jump to G Major at 10th fret
        var gMajorJump = new List<FretboardPosition> {
            new(Str.FromValue(5), 10, Pitch.Sharp.Parse("G3")),
            new(Str.FromValue(4), 9, Pitch.Sharp.Parse("B3")),
            new(Str.FromValue(3), 7, Pitch.Sharp.Parse("D4"))
        };

        double costSmooth = _costService.CalculateTransitionCost(cMajor, gMajorSmooth);
        double costJump = _costService.CalculateTransitionCost(cMajor, gMajorJump);

        TestContext.WriteLine($"Transition Cost (Smooth): {costSmooth}");
        TestContext.WriteLine($"Transition Cost (Jump): {costJump}");

        Assert.That(costSmooth, Is.LessThan(costJump), "Smooth transition should have lower cost than jumping across the neck");
    }

    [Test]
    public void Test_HighRegister_Preference()
    {
        // Scenario: MIDI notes in high register (E5, G5, B5)
        // Heuristic should find them at frets 12-15, NOT at frets 24+ (if they existed)
        // Or NOT try to find weird low-string versions if high-string is more ergonomic.
        var pitches = new[] { Pitch.Sharp.Parse("E5"), Pitch.Sharp.Parse("G5"), Pitch.Sharp.Parse("B5") };
        var realizations = _mapper.MapChord(pitches).ToList();

        var ranked = realizations
            .Select(r => new { Shape = r, Cost = _costService.CalculateStaticCost(r) })
            .OrderBy(x => x.Cost.TotalCost)
            .ToList();

        var best = ranked.First();
        TestContext.WriteLine($"Best High-Register realization:");
        foreach (var pos in best.Shape)
        {
            TestContext.WriteLine($"  String {pos.StringIndex.Value}, Fret {pos.Fret}");
        }

        // For E5, G5, B5 on standard guitar:
        // E5: String 1 Fret 12
        // G5: String 1 Fret 15 or String 2 Fret 20
        // B5: String 1 Fret 19 or String 2 Fret 24
        
        Assert.That(best.Shape.Max(p => p.Fret), Is.LessThan(24), "Should not push into extreme frets if better options exist");
        Assert.That(best.Shape.Min(p => p.Fret), Is.GreaterThanOrEqualTo(12), "High register pitches should be found in high register frets");
    }
}
