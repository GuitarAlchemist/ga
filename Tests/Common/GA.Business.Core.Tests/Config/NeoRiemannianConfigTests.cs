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

    /// <summary>
    ///     The remark above the hexatonic cycles of the YAML file: the roots of a cycle form an augmented triad,
    ///     and the notes of its six triads a hexatonic set, that triad and the one a semitone below.
    /// </summary>
    [Test]
    public void HexatonicCycle_HasTheRootsOfAnAugmentedTriadAndTheNotesOfTwo()
    {
        var cycles = NeoRiemannianConfig.GetHexatonicCycles().ToList();

        Assert.That(cycles, Has.Count.EqualTo(4));
        Assert.Multiple(() =>
        {
            foreach (var cycle in cycles)
            {
                var roots = Sorted(cycle.Triads.Select(Root));

                Assert.That(roots, Is.EqualTo(new[] { roots[0], roots[0] + 4, roots[0] + 8 }), cycle.Name);
                Assert.That(Sorted(cycle.Triads.SelectMany(Notes)),
                    Is.EqualTo(Sorted(roots.Concat(roots.Select(root => (root + 11) % 12)))), cycle.Name);
            }
        });
    }

    /// <summary>
    ///     The remark above the octatonic cycles: the roots of a cycle form a diminished seventh, the notes of its
    ///     eight triads an octatonic collection, and the i and j of <c>OCT(i,j)</c> are two pitch classes, a
    ///     semitone apart, that this collection alone holds together.
    /// </summary>
    [Test]
    public void OctatonicCycle_IsNamedAfterTwoPitchClassesOnlyItHoldsTogether()
    {
        var cycles = NeoRiemannianConfig.GetOctatonicCycles()
            .Select(cycle => (cycle.Name, Roots: Sorted(cycle.Triads.Select(Root)),
                Notes: Sorted(cycle.Triads.SelectMany(Notes))))
            .ToList();

        Assert.That(cycles, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            foreach (var cycle in cycles)
            {
                var first = cycle.Roots[0];
                var named = cycle.Name[4..^1].Split(',').Select(int.Parse).ToList(); // "OCT(0,1)" names 0 and 1

                Assert.That(cycle.Roots, Is.EqualTo(new[] { first, first + 3, first + 6, first + 9 }), cycle.Name);
                Assert.That(cycle.Notes, Has.Count.EqualTo(8), cycle.Name);
                Assert.That(named[1] - named[0], Is.EqualTo(1), cycle.Name);
                Assert.That(cycles.Where(other => named.All(pc => other.Notes.Contains(pc))).Select(other => other.Name),
                    Is.EqualTo(new[] { cycle.Name }), cycle.Name);
            }
        });
    }

    private static int Root(string triad) => Array.IndexOf(Roots, triad.Split('_')[0]);

    private static int[] Notes(string triad)
    {
        var root = Root(triad);
        var third = triad.EndsWith("_maj", StringComparison.Ordinal) ? 4 : 3;
        return [root, (root + third) % 12, (root + 7) % 12];
    }

    private static List<int> Sorted(IEnumerable<int> pitchClasses) => [.. pitchClasses.Distinct().Order()];

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
