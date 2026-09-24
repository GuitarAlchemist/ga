namespace GA.Business.Core.Tests.Chords;

using System.Linq;
using System.Threading.Tasks;
using Domain.Core.Theory.Atonal;
using Domain.Services.Chords;

/// <summary>
///     <see cref="CanonicalChordRecognizer.Identify" /> computes each pitch-class set once and applies
///     the bass hint on top. These tests check that contract on all 4096 sets and the 12 basses:
///     the bass only ever changes the slash suffix, the suffix appears exactly when the bass is not
///     the root of a pattern match, and repeated or concurrent calls give the same answer.
/// </summary>
[TestFixture]
public class CanonicalChordRecognizerCacheTests
{
    private static readonly string[] NoteNames = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];

    private static string Key(CanonicalChordResult r) =>
        $"{r.CanonicalName}|{r.Root}|{r.Quality}|{r.Extension}|{string.Join(",", r.Alterations)}|{r.SlashSuffix}|{r.PatternName}|{r.MatchDistance}|{r.IsNaturallyOccurring}|{r.DisplayName}";

    private static PitchClassSet Set(int id) => PitchClassSet.FromId(PitchClassSetId.FromValue(id));

    [Test]
    public void Bass_OnlyChangesTheSlashSuffix_ForEverySet()
    {
        var failures = 0;
        for (var id = 0; id < 4096; id++)
        {
            var set = Set(id);
            var plain = CanonicalChordRecognizer.Identify(set);
            if (plain.SlashSuffix is not null) failures++;

            // Only a pattern match on three or more pitch classes has a root a bass can differ from
            var hasChordRoot = set.Count >= 3 && plain.MatchDistance >= 0;
            for (var bass = 0; bass < 12; bass++)
            {
                var withBass = CanonicalChordRecognizer.Identify(set, PitchClass.FromValue(bass));
                var expectedSuffix = hasChordRoot && plain.Root != NoteNames[bass] ? $"/{NoteNames[bass]}" : null;
                if (withBass.SlashSuffix != expectedSuffix) failures++;
                if (Key(withBass with { SlashSuffix = null }) != Key(plain)) failures++;
            }
        }

        Assert.That(failures, Is.Zero);
    }

    [Test]
    public void RepeatedAndConcurrentCalls_ReturnTheSameAnswer()
    {
        var first = Enumerable.Range(0, 4096).Select(id => Key(CanonicalChordRecognizer.Identify(Set(id), PitchClass.FromValue(id % 12)))).ToArray();
        var concurrent = new string[4096];
        Parallel.For(0, 4096, id => concurrent[id] = Key(CanonicalChordRecognizer.Identify(Set(id), PitchClass.FromValue(id % 12))));

        Assert.That(concurrent, Is.EqualTo(first));
    }
}
