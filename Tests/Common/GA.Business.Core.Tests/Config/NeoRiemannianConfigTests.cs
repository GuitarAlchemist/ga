namespace GA.Business.Core.Tests.Config;

using GA.Business.Config;

/// <summary>
///     <see cref="NeoRiemannianConfig" /> loads the catalogue of <c>NeoRiemannian.yaml</c>: six transforms on the
///     24 triads, four hexatonic cycles and three octatonic ones.
/// </summary>
/// <remarks>
///     The module returned an empty catalogue while its three YAML records were <c>private</c>: YamlDotNet could
///     not create them, and the loader turned the exception into three empty lists. The triads below are built
///     by the test, not read from the catalogue, so that an empty catalogue fails every test.
/// </remarks>
[TestFixture]
public class NeoRiemannianConfigTests
{
    private static readonly string[] Roots = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    private static readonly string[] Triads = [.. Enumerable.Range(0, 12).SelectMany(x => new[] { Major(x), Minor(x) })];

    private static string Major(int root) => $"{Roots[root % 12]}_maj";

    private static string Minor(int root) => $"{Roots[root % 12]}_min";

    /// <summary>Applies the transforms from right to left, as <c>L o P o R</c> is read.</summary>
    private static string? Compose(string triad, params string[] transforms)
    {
        string? current = triad;
        for (var i = transforms.Length - 1; i >= 0 && current is not null; i--)
        {
            current = NeoRiemannianConfig.Apply(transforms[i], current)?.Value;
        }

        return current;
    }

    [Test]
    public void GetAllTransforms_ReturnsTheSixTransformsWith24MappingsEach()
    {
        var transforms = NeoRiemannianConfig.GetAllTransforms().ToList();

        Assert.That(transforms.Select(t => t.Name), Is.EquivalentTo(new[] { "P", "L", "R", "S", "N", "H" }));
        Assert.That(transforms.Select(t => t.Mappings.Count), Is.All.EqualTo(24));
    }

    /// <summary>
    ///     Each transform pairs the major triad on x with the minor triad on x + offset, in both directions: the
    ///     derivation rules at the top of the YAML file. That makes each one an involution (invariant 14).
    /// </summary>
    [TestCase("P", 0, 1)]
    [TestCase("L", 4, 1)]
    [TestCase("R", 9, 2)]
    [TestCase("S", 1, 2)]
    [TestCase("N", 5, 2)]
    [TestCase("H", 8, 3)]
    public void Apply_PairsEachMajorTriadWithTheMinorTriadOfItsRule(string transform, int offset, int cost)
    {
        Assert.That(NeoRiemannianConfig.TryGetTransform(transform)?.Value.VoiceLeadingCost, Is.EqualTo(cost));

        Assert.Multiple(() =>
        {
            for (var x = 0; x < 12; x++)
            {
                Assert.That(Compose(Major(x), transform), Is.EqualTo(Minor(x + offset)));
                Assert.That(Compose(Minor(x + offset), transform), Is.EqualTo(Major(x)));
            }
        });
    }

    /// <summary>Invariant 15: S, N and H are the three-letter words the YAML file derives them from.</summary>
    [TestCase("S", "L", "P", "R")]
    [TestCase("N", "R", "L", "P")]
    [TestCase("H", "L", "P", "L")]
    public void Composite_EqualsItsThreeLetterWord(string composite, string third, string second, string first) =>
        Assert.Multiple(() =>
        {
            foreach (var triad in Triads)
            {
                Assert.That(Compose(triad, third, second, first), Is.EqualTo(Compose(triad, composite)), triad);
                Assert.That(Compose(triad, composite), Is.Not.Null, triad);
            }
        });

    [Test]
    public void GetHexatonicCycles_ReturnsFourCyclesOfSixTriadsAlternatingPAndL() =>
        AssertCycles(NeoRiemannianConfig.GetHexatonicCycles(), 4, 6, "P", "L");

    [Test]
    public void GetOctatonicCycles_ReturnsThreeCyclesOfEightTriadsAlternatingPAndR() =>
        AssertCycles(NeoRiemannianConfig.GetOctatonicCycles(), 3, 8, "P", "R");

    private static void AssertCycles(IEnumerable<NeoRiemannianConfig.Cycle> source, int count, int length,
        string evenStep, string oddStep)
    {
        var cycles = source.Select(cycle => cycle.Triads.ToList()).ToList();

        Assert.That(cycles, Has.Count.EqualTo(count));
        Assert.That(cycles.Select(triads => triads.Count), Is.All.EqualTo(length));
        Assert.That(cycles.SelectMany(triads => triads), Is.EquivalentTo(Triads), "the cycles partition the 24 triads");

        Assert.Multiple(() =>
        {
            foreach (var triads in cycles)
            {
                for (var i = 0; i < triads.Count; i++)
                {
                    var step = i % 2 == 0 ? evenStep : oddStep;
                    Assert.That(Compose(triads[i], step), Is.EqualTo(triads[(i + 1) % triads.Count]),
                        $"{step} from {triads[i]}");
                }
            }
        });
    }
}
