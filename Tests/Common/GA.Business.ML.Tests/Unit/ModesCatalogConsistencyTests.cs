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
/// The seventh chord family lists chord qualities rather than rotations, the major triad family holds the
/// major and the minor triads with their inversions, and the 16 "modes" of the all-interval tetrachord
/// family are not rotations of one set, so those are left out; the diminished and augmented triads'
/// inversions are their rotations.
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
        familyName != "Major Triad Family" &&
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

    // Each inversion transposed so that its bass is C: C major's first inversion, E G C, is C Eb Ab. The spelling
    // keeps the intervals above the bass: G# C E, a diminished fourth and a minor sixth, is C Fb Ab
    [TestCase("Major Triad First Inversion", "C Eb Ab")]
    [TestCase("Major Triad Second Inversion", "C F A")]
    [TestCase("Minor Triad First Inversion", "C E A")]
    [TestCase("Minor Triad Second Inversion", "C F Ab")]
    [TestCase("Diminished Triad First Inversion", "C Eb A")]
    [TestCase("Diminished Triad Second Inversion", "C F# A")]
    [TestCase("Augmented Triad First Inversion", "C E Ab")]
    [TestCase("Augmented Triad Second Inversion", "C Fb Ab")]
    public void TriadInversions_HaveTheNotesOfTheirName(string mode, string notes) =>
        Assert.That(ModesConfig.GetModalFamilies().SelectMany(f => f.Modes).Single(m => m.Name == mode).Notes,
            Is.EqualTo(notes));

    // Unquoted, YAML reads " #" as the start of a comment, and "Lydian #2 #6" came back as "Lydian"
    [TestCase("Lydian #2 #6", "Double Harmonic Family")]
    [TestCase("Ionian Augmented #2", "Double Harmonic Family")]
    [TestCase("Aeolian #4 (Lydian Diminished)", "Neapolitan Minor Family")]
    [TestCase("Lydian Dominant #5", "Neapolitan Major Family")]
    public void ModeNames_KeepWhatFollowsASharp(string name, string family)
    {
        var mode = ModesConfig.TryGetModeByName(name);

        Assert.Multiple(() =>
        {
            Assert.That(mode?.Value.Name, Is.EqualTo(name));
            Assert.That(mode?.Value.FamilyName?.Value, Is.EqualTo(family));
        });
    }

    [TestCase("Super Locrian", "Altered")]
    [TestCase("Altered bb7", "Ultralocrian")]
    [TestCase("Acoustic Scale", "Lydian Dominant")]
    [TestCase("Spanish Phrygian", "Phrygian Dominant")]
    [TestCase("Locrian Natural 2", "Locrian #2")]
    [TestCase("Dorian Sharp 4", "Dorian #4")]
    [TestCase("Lydian Sharp 2", "Lydian #2")]
    public void TryGetModeByName_FindsAModeByItsAlternateName(string alternateName, string expected) =>
        Assert.That(ModesConfig.TryGetModeByName(alternateName)?.Value.Name, Is.EqualTo(expected));

    // "Whole-Half Diminished" is a mode of the octatonic family and an alternate name in the diminished family
    [Test]
    public void TryGetModeByName_PrefersAModesOwnName() =>
        Assert.That(ModesConfig.TryGetModeByName("Whole-Half Diminished")?.Value.FamilyName?.Value,
            Is.EqualTo("Diminished (Octatonic) Family"));
}
