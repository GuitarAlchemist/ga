namespace GA.Business.ML.Tests.Unit;

using GA.Business.Config;

/// <summary>
/// Pins the music theory in <c>Modes.yaml</c>, which <see cref="GA.Business.ML.Agents.Skills.ModesSkill"/>
/// serves verbatim: mode k of a family is the k-th rotation of mode 1, and the family's interval class
/// vector is the one its notes produce.
/// </summary>
/// <remarks>
/// A 2026-10-03 audit found 36 modes that broke the rotation rule (the pentatonic modes 3–5 swapped,
/// most Hungarian major, Enigmatic, In Sen, Prometheus and bebop modes wrong) and 7 wrong vectors.
/// The chord and triad families list chord qualities rather than rotations, and the 16 "modes" of the
/// all-interval tetrachord family are not rotations of one set, so those are left out.
/// </remarks>
[TestFixture]
public sealed class ModesCatalogConsistencyTests
{
    private static readonly Dictionary<char, int> Natural = new()
    {
        ['C'] = 0, ['D'] = 2, ['E'] = 4, ['F'] = 5, ['G'] = 7, ['A'] = 9, ['B'] = 11,
    };

    private static bool IsRotationFamily(string familyName) =>
        !familyName.Contains("Chord Family", StringComparison.Ordinal) &&
        !familyName.Contains("Triad Family", StringComparison.Ordinal) &&
        familyName != "All Interval Tetrachord Family";

    private static int PitchClass(string note)
    {
        var value = Natural[note[0]];
        foreach (var c in note[1..])
            value += c switch { '#' => 1, 'x' => 2, 'b' => -1, _ => throw new FormatException(note) };
        return ((value % 12) + 12) % 12;
    }

    private static int[] Steps(string notes)
    {
        var pcs = notes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(PitchClass).ToArray();
        return pcs.Select((pc, i) => ((pcs[(i + 1) % pcs.Length] - pc) % 12 + 12) % 12).ToArray();
    }

    private static string IntervalClassVector(string notes)
    {
        var pcs = notes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(PitchClass).Distinct().ToArray();
        var icv = new int[6];
        for (var i = 0; i < pcs.Length; i++)
            for (var j = i + 1; j < pcs.Length; j++)
            {
                var d = ((pcs[j] - pcs[i]) % 12 + 12) % 12;
                icv[Math.Min(d, 12 - d) - 1]++;
            }
        return $"<{string.Join(' ', icv)}>";
    }

    [Test]
    public void EveryMode_IsTheMatchingRotationOfItsFamilysFirstMode()
    {
        var failures = new List<string>();
        foreach (var family in ModesConfig.GetModalFamilies().Where(f => IsRotationFamily(f.Name)))
        {
            var first = Steps(family.Modes[0].Notes);
            for (var k = 0; k < family.Modes.Count; k++)
            {
                var expected = first.Skip(k).Concat(first.Take(k)).ToArray();
                var actual = Steps(family.Modes[k].Notes);
                if (!actual.SequenceEqual(expected))
                    failures.Add($"{family.Name} #{k + 1} {family.Modes[k].Name}: {family.Modes[k].Notes}");
            }
        }

        Assert.That(failures, Is.Empty);
    }

    [Test]
    public void FamilyIntervalClassVector_MatchesItsNotes()
    {
        var failures = ModesConfig.GetAllModes()
            .Where(m => m.FamilyName is not null && IsRotationFamily(m.FamilyName.Value))
            .GroupBy(m => m.FamilyName.Value)
            .Select(g => g.First())
            .Where(m => !string.IsNullOrEmpty(m.IntervalClassVector) && m.IntervalClassVector != "<0 0 0 0 0 0>")
            .Where(m => m.IntervalClassVector.Replace(" ", "") != IntervalClassVector(m.Notes).Replace(" ", ""))
            .Select(m => $"{m.FamilyName.Value}: {m.IntervalClassVector}, notes give {IntervalClassVector(m.Notes)}")
            .ToList();

        Assert.That(failures, Is.Empty);
    }

    [TestCase("Blues Minor", "C Eb F Ab Bb")]
    [TestCase("Blues Major", "C D F G A")]
    [TestCase("Minor Pentatonic", "C Eb F G Bb")]
    public void PentatonicModes_HaveTheirStandardNotes(string mode, string notes)
    {
        var family = ModesConfig.GetModalFamilies().Single(f => f.Name == "Major Pentatonic Family");

        Assert.That(family.Modes.Single(m => m.Name == mode).Notes, Is.EqualTo(notes));
    }
}
