namespace GA.Business.Core.Tests.Chords;

using GA.Business.Core.Atonal;
using GA.Business.Core.Chords;
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
        // C Ionian (Major): {0,2,4,5,7,9,11}
        var mode = MakeUnifiedModeInstance([0, 2, 4, 5, 7, 9, 11]);

        var i = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 1, ChordExtension.Seventh);
        var ii = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 2, ChordExtension.Seventh);
        var v = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 5, ChordExtension.Seventh);

        Assert.That(i, Is.EqualTo("Imaj7"));
        Assert.That(ii, Is.EqualTo("ii7"));
        Assert.That(v, Is.EqualTo("V7"));
    }

    [Test]
    public void RomanNumerals_Dorian_SevenChords_AreNamed_AsExpected()
    {
        // C Dorian: {0,2,3,5,7,9,10}
        var mode = MakeUnifiedModeInstance([0, 2, 3, 5, 7, 9, 10]);

        var i = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 1, ChordExtension.Seventh);
        var iv = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 4, ChordExtension.Seventh);

        Assert.That(i, Is.EqualTo("i7"));
        Assert.That(iv, Is.EqualTo("IV7"));
    }

    [Test]
    public void RomanNumerals_Degree_WrapAround_ProducesSameAsDegree1()
    {
        var mode = MakeUnifiedModeInstance([0, 2, 4, 5, 7, 9, 11]); // Ionian
        var d1 = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 1, ChordExtension.Seventh);
        var d8 = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 8, ChordExtension.Seventh);
        Assert.That(d8, Is.EqualTo(d1));
    }

    [Test]
    public void RomanNumerals_NonTertian_Quartal_AppendsSuffix()
    {
        var mode = MakeUnifiedModeInstance([0, 2, 4, 5, 7, 9, 11]); // Ionian
        var name = ChordTemplateNamingService.GenerateModalChordName(
            mode, degree: 1, ChordExtension.Seventh, ChordStackingType.Quartal);
        Assert.That(name, Is.EqualTo("Imaj7 (4ths)"));
    }

    [Test]
    public void DI_Resolves_IChordNamingService_And_Generates_RomanNumerals()
    {
        var services = new ServiceCollection();
        services.AddScoped<IChordNamingService, ChordNamingService>();
        var provider = services.BuildServiceProvider();

        var svc = provider.GetRequiredService<IChordNamingService>();

        var mode = MakeUnifiedModeInstance([0, 2, 4, 5, 7, 9, 11]); // Ionian
        var result = svc.GenerateModalChordName(mode, degree: 1, ChordExtension.Seventh);

        Assert.That(result, Is.EqualTo("Imaj7"));
    }

    [Test]
    public void RomanNumerals_Ionian_Triads_AllDegrees_AsExpected()
    {
        // C Ionian (Major): {0,2,4,5,7,9,11}
        var mode = MakeUnifiedModeInstance([0, 2, 4, 5, 7, 9, 11]);

        var i = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 1, ChordExtension.Triad);
        var ii = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 2, ChordExtension.Triad);
        var iii = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 3, ChordExtension.Triad);
        var iv = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 4, ChordExtension.Triad);
        var v = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 5, ChordExtension.Triad);
        var vi = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 6, ChordExtension.Triad);
        var vii = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 7, ChordExtension.Triad);

        Assert.Multiple(() =>
        {
            Assert.That(i, Is.EqualTo("I"));
            Assert.That(ii, Is.EqualTo("ii"));
            Assert.That(iii, Is.EqualTo("iii"));
            Assert.That(iv, Is.EqualTo("IV"));
            Assert.That(v, Is.EqualTo("V"));
            Assert.That(vi, Is.EqualTo("vi"));
            Assert.That(vii, Is.EqualTo("vii°"));
        });
    }

    [Test]
    public void RomanNumerals_Mixolydian_I7_Dominant_AsExpected()
    {
        // C Mixolydian: {0,2,4,5,7,9,10}
        var mode = MakeUnifiedModeInstance([0, 2, 4, 5, 7, 9, 10]);
        var i7 = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 1, ChordExtension.Seventh);
        Assert.That(i7, Is.EqualTo("I7"));
    }

    [Test]
    public void RomanNumerals_Degree_Normalization_ZeroAndNegative()
    {
        var mode = MakeUnifiedModeInstance([0, 2, 4, 5, 7, 9, 11]); // Ionian
        // degree 0 should wrap to degree 7, both are expected to be the same as generated output
        var d0 = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 0, ChordExtension.Seventh);
        var d7 = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 7, ChordExtension.Seventh);
        Assert.That(d0, Is.EqualTo(d7));

        // degree -6 should normalize equivalently to some degree; compare with its positive counterpart
        var dn6 = ChordTemplateNamingService.GenerateModalChordName(mode, degree: -6, ChordExtension.Seventh);
        var d1 = ChordTemplateNamingService.GenerateModalChordName(mode, degree: 1, ChordExtension.Seventh);
        // For Ionian, -6 ≡ 1 mod 7 (since (-6 - 1) % 7 == 0), so expect same as degree 1
        Assert.That(dn6, Is.EqualTo(d1));
    }

    [Test]
    public void RomanNumerals_NonTertian_Quintal_AppendsSuffix()
    {
        var mode = MakeUnifiedModeInstance([0, 2, 4, 5, 7, 9, 11]); // Ionian
        var name = ChordTemplateNamingService.GenerateModalChordName(
            mode, degree: 5, ChordExtension.Seventh, ChordStackingType.Quintal);
        Assert.That(name, Is.EqualTo("V7 (5ths)"));
    }

    [Test]
    public void DI_Resolves_IChordNamingService_For_NonTertian_Stacking()
    {
        var services = new ServiceCollection();
        services.AddScoped<IChordNamingService, ChordNamingService>();
        var provider = services.BuildServiceProvider();

        var svc = provider.GetRequiredService<IChordNamingService>();
        var mode = MakeUnifiedModeInstance([0, 2, 4, 5, 7, 9, 11]); // Ionian
        var result = svc.GenerateModalChordName(mode, degree: 4, ChordExtension.Seventh, ChordStackingType.Quartal);
        Assert.That(result, Is.EqualTo("IV7 (4ths)"));
    }
}
