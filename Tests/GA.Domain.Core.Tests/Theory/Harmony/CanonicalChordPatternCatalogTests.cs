namespace GA.Domain.Core.Tests.Theory.Harmony;

using System.Linq;
using GA.Domain.Core.Theory.Harmony;
using NUnit.Framework;

/// <summary>
///     Alias resolution in <see cref="CanonicalChordPatternCatalog" />: some chord names are synonyms
///     for the same interval set. <see cref="CanonicalChordPatternCatalog.TryFindExact" /> returns the
///     primary name (lowest priority) and <see cref="CanonicalChordPatternCatalog.FindAllExact" />
///     returns every name, primary first, so the aliases stay reachable.
/// </summary>
[TestFixture]
public class CanonicalChordPatternCatalogTests
{
    // Primary name first, then its aliases.
    private static readonly string[][] _aliasGroups =
    [
        ["augmented-7", "dominant-7-sharp-5"], // C+7 = C7#5: C E G# Bb
        ["major-6-add-9", "6-9"], // C6/9: C D E G A
        ["minor-6-add-9", "minor-6-9"], // Cm6/9: C D Eb G A
        ["9-sus4", "dominant-11"], // C9sus4 = C11 without its third: C D F G Bb
    ];

    private static string Key(int[] intervals) => string.Join(",", intervals.OrderBy(i => i));

    [Test]
    public void DuplicateIntervalSets_AreExactlyTheKnownAliasGroups()
    {
        var duplicates = CanonicalChordPatternCatalog.All
            .GroupBy(p => Key(p.Intervals))
            .Where(g => g.Count() > 1)
            .Select(g => g.OrderBy(p => p.Priority).Select(p => p.Name).ToArray())
            .ToArray();

        Assert.That(duplicates, Is.EquivalentTo(_aliasGroups),
            "a new duplicate interval set needs a decision: add it to the alias groups or remove it");
    }

    [Test]
    public void TryFindExact_ReturnsThePrimaryName() =>
        Assert.Multiple(() =>
        {
            foreach (var group in _aliasGroups)
            {
                var intervals = CanonicalChordPatternCatalog.All.Single(p => p.Name == group[0]).Intervals;
                Assert.That(CanonicalChordPatternCatalog.TryFindExact(intervals)?.Name, Is.EqualTo(group[0]));
            }
        });

    [Test]
    public void FindAllExact_ReturnsPrimaryThenAliases() =>
        Assert.Multiple(() =>
        {
            foreach (var group in _aliasGroups)
            {
                var intervals = CanonicalChordPatternCatalog.All.Single(p => p.Name == group[1]).Intervals;
                var names = CanonicalChordPatternCatalog.FindAllExact(intervals).Select(p => p.Name);
                Assert.That(names, Is.EqualTo(group));
            }
        });

    [Test]
    public void FindAllExact_UniqueIntervalSet_ReturnsOneName()
    {
        var names = CanonicalChordPatternCatalog.FindAllExact([0, 4, 7, 11]).Select(p => p.Name);
        Assert.That(names, Is.EqualTo(new[] { "major-7" }));
    }
}
