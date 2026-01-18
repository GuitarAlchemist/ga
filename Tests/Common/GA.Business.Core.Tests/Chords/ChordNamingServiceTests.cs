namespace GA.Business.Core.Tests.Chords;

using GA.Business.Core.Atonal;
using GA.Business.Core.Chords;
using GA.Business.Core.Chords.Abstractions;
using GA.Business.Core.Unified;
using Microsoft.Extensions.DependencyInjection;

public class ChordNamingServiceTests
{
    private static UnifiedModeInstance MakeUnifiedModeInstance(int[] pcs, int rootPc = 0)
    {
        var set = new PitchClassSet([.. pcs.Select(PitchClass.FromValue)]);
        var root = PitchClass.FromValue(rootPc);
        var svc = new UnifiedModeService();
        return svc.FromPitchClassSet(set, root);
    }

    [Test]
    public void RomanNumerals_Ionian_SevenChords_AreNamed_AsExpected()
    {
        // Arrange
        // C Ionian (Major): {0,2,4,5,7,9,11}
        var mode = MakeUnifiedModeInstance([0, 2, 4, 5, 7, 9, 11]);

        // Act
        var i = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 1, ChordExtension.Seventh);
        var ii = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 2, ChordExtension.Seventh);
        var v = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 5, ChordExtension.Seventh);

        // Assert
        TestContext.WriteLine($"Mode: Ionian, Degree 1 (7th): Expected=Imaj7, Actual={i} (Major scale tonic 7th is Major 7th)");
        TestContext.WriteLine($"Mode: Ionian, Degree 2 (7th): Expected=ii7, Actual={ii} (Major scale supertonic 7th is Minor 7th)");
        TestContext.WriteLine($"Mode: Ionian, Degree 5 (7th): Expected=V7, Actual={v} (Major scale dominant 7th is Dominant 7th)");

        Assert.Multiple(() =>
        {
            Assert.That(i, Is.EqualTo("Imaj7"), "Degree 1 in Ionian should be a Major 7th chord (Imaj7).");
            Assert.That(ii, Is.EqualTo("ii7"), "Degree 2 in Ionian should be a Minor 7th chord (ii7).");
            Assert.That(v, Is.EqualTo("V7"), "Degree 5 in Ionian should be a Dominant 7th chord (V7).");
        });
    }

    [Test]
    public void RomanNumerals_Dorian_SevenChords_AreNamed_AsExpected()
    {
        // Arrange
        // C Dorian: {0,2,3,5,7,9,10}
        var mode = MakeUnifiedModeInstance([0, 2, 3, 5, 7, 9, 10]);

        // Act
        var i = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 1, ChordExtension.Seventh);
        var iv = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 4, ChordExtension.Seventh);

        // Assert
        TestContext.WriteLine($"Mode: Dorian, Degree 1 (7th): Expected=i7, Actual={i} (Dorian tonic 7th is Minor 7th)");
        TestContext.WriteLine($"Mode: Dorian, Degree 4 (7th): Expected=IV7, Actual={iv} (Dorian subdominant 7th is Dominant 7th)");

        Assert.Multiple(() =>
        {
            Assert.That(i, Is.EqualTo("i7"), "Degree 1 in Dorian should be a Minor 7th chord (i7).");
            Assert.That(iv, Is.EqualTo("IV7"), "Degree 4 in Dorian should be a Dominant 7th chord (IV7).");
        });
    }

    [Test]
    public void RomanNumerals_Degree_WrapAround_ProducesSameAsDegree1()
    {
        // Arrange
        var mode = MakeUnifiedModeInstance([0, 2, 4, 5, 7, 9, 11]); // Ionian

        // Act
        var d1 = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 1, ChordExtension.Seventh);
        var d8 = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 8, ChordExtension.Seventh);

        // Assert
        TestContext.WriteLine($"Degree 1: {d1}, Degree 8 (wrap around): Expected={d1}, Actual={d8} (Octave equivalence in modal degrees)");
        Assert.That(d8, Is.EqualTo(d1), "Degree 8 should wrap around to produce the same result as Degree 1.");
    }

    [Test]
    public void RomanNumerals_NonTertian_Quartal_AppendsSuffix()
    {
        // Arrange
        var mode = MakeUnifiedModeInstance([0, 2, 4, 5, 7, 9, 11]); // Ionian

        // Act
        var name = ChordTemplateNamingService.GenerateModalChordName(
            mode, degree: 1, ChordExtension.Seventh, ChordStackingType.Quartal);

        // Assert
        TestContext.WriteLine($"Ionian Degree 1 Quartal: Expected=Imaj7 (4ths), Actual={name} (Quartal stacking should append suffix)");
        Assert.That(name, Is.EqualTo("Imaj7 (4ths)"), "Non-tertian quartal chords should have the '(4ths)' suffix.");
    }

    [Test]
    public void DI_Resolves_IChordNamingService_And_Generates_RomanNumerals()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IChordNamingService, ChordNamingService>();
        var provider = services.BuildServiceProvider();
        var svc = provider.GetRequiredService<IChordNamingService>();
        var mode = MakeUnifiedModeInstance([0, 2, 4, 5, 7, 9, 11]); // Ionian

        // Act
        var result = svc.GenerateModalChordName(mode, degree: 1, ChordExtension.Seventh);

        // Assert
        TestContext.WriteLine($"DI Resolved Service Result: Expected=Imaj7, Actual={result} (Service interface should resolve correctly)");
        Assert.That(result, Is.EqualTo("Imaj7"), "The injected IChordNamingService should correctly generate modal chord names.");
    }

    [Test]
    public void RomanNumerals_Ionian_Triads_AllDegrees_AsExpected()
    {
        // Arrange
        // C Ionian (Major): {0,2,4,5,7,9,11}
        var mode = MakeUnifiedModeInstance([0, 2, 4, 5, 7, 9, 11]);

        // Act
        var i = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 1, ChordExtension.Triad);
        var ii = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 2, ChordExtension.Triad);
        var iii = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 3, ChordExtension.Triad);
        var iv = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 4, ChordExtension.Triad);
        var v = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 5, ChordExtension.Triad);
        var vi = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 6, ChordExtension.Triad);
        var vii = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 7, ChordExtension.Triad);

        // Assert
        TestContext.WriteLine($"Ionian Triads: Expected=I, ii, iii, IV, V, vi, vii°, Actual={i}, {ii}, {iii}, {iv}, {v}, {vi}, {vii} (Standard major scale diatonic triads)");

        Assert.Multiple(() =>
        {
            Assert.That(i, Is.EqualTo("I"), "Ionian degree 1 should be Major triad (I)");
            Assert.That(ii, Is.EqualTo("ii"), "Ionian degree 2 should be Minor triad (ii)");
            Assert.That(iii, Is.EqualTo("iii"), "Ionian degree 3 should be Minor triad (iii)");
            Assert.That(iv, Is.EqualTo("IV"), "Ionian degree 4 should be Major triad (IV)");
            Assert.That(v, Is.EqualTo("V"), "Ionian degree 5 should be Major triad (V)");
            Assert.That(vi, Is.EqualTo("vi"), "Ionian degree 6 should be Minor triad (vi)");
            Assert.That(vii, Is.EqualTo("vii°"), "Ionian degree 7 should be Diminished triad (vii°)");
        });
    }

    [Test]
    public void RomanNumerals_Mixolydian_I7_Dominant_AsExpected()
    {
        // Arrange
        // C Mixolydian: {0,2,4,5,7,9,10}
        var mode = MakeUnifiedModeInstance([0, 2, 4, 5, 7, 9, 10]);

        // Act
        var i7 = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 1, ChordExtension.Seventh);

        // Assert
        TestContext.WriteLine($"Mixolydian Degree 1 (7th): {i7}");
        Assert.That(i7, Is.EqualTo("I7"));
    }

    [Test]
    public void RomanNumerals_Degree_Normalization_ZeroAndNegative()
    {
        // Arrange
        var mode = MakeUnifiedModeInstance([0, 2, 4, 5, 7, 9, 11]); // Ionian

        // Act
        // degree 0 should wrap to degree 7
        var d0 = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 0, ChordExtension.Seventh);
        var d7 = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 7, ChordExtension.Seventh);
        // degree -6 should normalize equivalently to degree 1
        var dn6 = ChordTemplateNamingService.GenerateModalChordName(mode, degree: -6, ChordExtension.Seventh);
        var d1 = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 1, ChordExtension.Seventh);

        // Assert
        TestContext.WriteLine($"Degree 0: {d0}, Degree 7: {d7}");
        TestContext.WriteLine($"Degree -6: {dn6}, Degree 1: {d1}");

        Assert.Multiple(() =>
        {
            Assert.That(d0, Is.EqualTo(d7));
            Assert.That(dn6, Is.EqualTo(d1));
        });
    }

    [Test]
    public void RomanNumerals_NonTertian_Quintal_AppendsSuffix()
    {
        // Arrange
        var mode = MakeUnifiedModeInstance([0, 2, 4, 5, 7, 9, 11]); // Ionian

        // Act
        var name = ChordTemplateNamingService.GenerateModalChordName(
            mode, degree: 5, ChordExtension.Seventh, ChordStackingType.Quintal);

        // Assert
        TestContext.WriteLine($"Ionian Degree 5 Quintal: {name}");
        Assert.That(name, Is.EqualTo("V7 (5ths)"));
    }

    [Test]
    public void DI_Resolves_IChordNamingService_For_NonTertian_Stacking()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IChordNamingService, ChordNamingService>();
        var provider = services.BuildServiceProvider();
        var svc = provider.GetRequiredService<IChordNamingService>();
        var mode = MakeUnifiedModeInstance([0, 2, 4, 5, 7, 9, 11]); // Ionian

        // Act
        var result = svc.GenerateModalChordName(mode, degree: 4, ChordExtension.Seventh, ChordStackingType.Quartal);

        // Assert
        TestContext.WriteLine($"DI Resolved (Quartal): {result}");
        Assert.That(result, Is.EqualTo("IV7 (4ths)"));
    }
}
