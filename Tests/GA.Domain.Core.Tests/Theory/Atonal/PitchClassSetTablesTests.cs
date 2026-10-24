namespace GA.Domain.Core.Tests.Theory.Atonal;

using System.Collections.Immutable;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Core.Theory.Tonal;
using NUnit.Framework;

/// <summary>
///     <see cref="PitchClassSet.ToNormalForm" />, <see cref="PitchClassSet.IsNormalForm" />,
///     <see cref="PitchClassSet.ClosestDiatonicKey" /> and <see cref="PitchClassSet.GetCompatibleKeys" /> now
///     read 12-bit tables. The input domain is 4,096 sets, so each is proved equal to the implementation it
///     replaced on every input. The oracles below are those implementations, made static; the closest-key one
///     keeps its ordering but counts matches directly instead of building the collections it never read. The
///     learn site's csharp-advanced course measured them at 175 KB and 110 KB per call.
/// </summary>
[TestFixture]
public class PitchClassSetTablesTests
{
    private static readonly PitchClassSet[] AllSets = [.. PitchClassSetId.Items.Select(id => id.ToPitchClassSet())];

    [Test]
    public void ToNormalForm_MatchesTheSortedSetImplementation_OnAllSets()
    {
        var mismatches = AllSets
            .Where(set => !set.ToNormalForm().SequenceEqual(LegacyToNormalForm(set)))
            .Select(set => set.Id.Value)
            .ToList();

        Assert.That(mismatches, Is.Empty);
    }

    [Test]
    public void IsNormalForm_MatchesTheSortedSetImplementation_OnAllSets()
    {
        var mismatches = AllSets
            .Where(set => set.IsNormalForm != LegacyToNormalForm(set).SequenceEqual(set))
            .Select(set => set.Id.Value)
            .ToList();

        Assert.That(mismatches, Is.Empty);
    }

    [Test]
    public void ClosestDiatonicKey_MatchesTheDictionaryImplementation_OnAllSets()
    {
        var mismatches = AllSets
            .Where(set => set.ClosestDiatonicKey != LegacyClosestDiatonicKey(set))
            .Select(set => set.Id.Value)
            .ToList();

        Assert.That(mismatches, Is.Empty);
    }

    [Test]
    public void GetCompatibleKeys_MatchesTheSubsetImplementation_OnAllSets()
    {
        var mismatches = AllSets
            .Where(set => !set.GetCompatibleKeys().SequenceEqual(LegacyGetCompatibleKeys(set)))
            .Select(set => set.Id.Value)
            .ToList();

        Assert.That(mismatches, Is.Empty);
    }

    [Test]
    public void GetCompatibleKeys_OfCMajorTriad_ListsItsKeys()
    {
        var cMajor = new PitchClassSet([PitchClass.FromValue(0), PitchClass.FromValue(4), PitchClass.FromValue(7)]);

        Assert.That(cMajor.GetCompatibleKeys().Select(k => k.ToString()),
            Is.EqualTo(LegacyGetCompatibleKeys(cMajor).Select(k => k.ToString())).And.Not.Empty);
    }

    // ── The replaced implementations ──────────────────────────────────────────────────────────

    private static PitchClassSet LegacyToNormalForm(PitchClassSet set)
    {
        var normalForm = new List<PitchClass>();
        var minInterval = int.MaxValue;
        var rotations = GenerateRotations(set).ToImmutableArray();

        foreach (var rotation in rotations)
        {
            var intervalVector = CalculateIntervals(rotation);
            var intervalSpan = intervalVector.Max() - intervalVector.Min();
            if (intervalSpan < minInterval)
            {
                minInterval = intervalSpan;
                normalForm = [.. rotation];
                continue;
            }

            if (intervalSpan == minInterval
                &&
                IsMoreCompact(intervalVector, CalculateIntervals(normalForm)))
            {
                normalForm = [.. rotation];
            }
        }

        return new(normalForm);

        static ImmutableArray<int> CalculateIntervals(IReadOnlyList<PitchClass> pitchClasses)
        {
            var intervals = ImmutableArray.CreateBuilder<int>();
            for (var i = 0; i < pitchClasses.Count; i++)
            {
                var nextIndex = (i + 1) % pitchClasses.Count;
                var interval = (pitchClasses[nextIndex] - pitchClasses[i]).Value;
                intervals.Add(interval);
            }

            return intervals.ToImmutable();
        }

        static IEnumerable<ImmutableSortedSet<PitchClass>> GenerateRotations(PitchClassSet pitchClassSet)
        {
            var builder = ImmutableSortedSet.CreateBuilder<PitchClass>();
            foreach (var basePitchClass in pitchClassSet)
            {
                builder.Clear();
                foreach (var pitchClass in pitchClassSet)
                {
                    builder.Add(pitchClass - basePitchClass);
                }

                yield return builder.ToImmutable();
            }
        }

        static bool IsMoreCompact(IEnumerable<int> vector1, IEnumerable<int> vector2) =>
            vector1
                .Zip(vector2, (v1, v2) => v1.CompareTo(v2))
                .FirstOrDefault(cmp => cmp != 0) < 0;
    }

    private static Key LegacyClosestDiatonicKey(PitchClassSet set)
    {
        var normalForm = LegacyToNormalForm(set).SequenceEqual(set) ? set : LegacyToNormalForm(set);
        var containsMinorThird = normalForm.Contains(Note.Chromatic.DSharpOrEFlat.PitchClass);
        var expectedKeyMode = containsMinorThird ? KeyMode.Minor : KeyMode.Major;

        var list = new List<(Key Key, int Matches)>();
        foreach (var key in Key.Items)
        {
            var matches = key.Notes.ToImmutableList().Count(keyNote => set.Contains(keyNote.PitchClass));
            list.Add((key, matches));
        }

        return list
            .OrderByDescending(tuple => tuple.Matches)
            .ThenByDescending(tuple => tuple.Key.KeyMode == expectedKeyMode)
            .First()
            .Key;
    }

    private static IReadOnlyCollection<Key> LegacyGetCompatibleKeys(PitchClassSet set) => Key.Items
        .Where(key => set.IsSubsetOf(key.PitchClassSet))
        .OrderBy(key => key.KeySignature.AccidentalCount)
        .ToImmutableList();
}
