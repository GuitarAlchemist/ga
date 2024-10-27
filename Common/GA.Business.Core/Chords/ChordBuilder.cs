namespace GA.Business.Core.Chords;

using Atonal;
using GA.Core.Combinatorics;
using GA.Core.Extensions;
using Intervals;
using Intervals.Primitives;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

/// <summary>
/// 
/// </summary>
/// <remarks>
/// See the guitar grimoire https://mikesimm.djlemonk.com/bblog/Scales-and-Modes.pdf
/// https://www.smithfowler.org/music/Chord_Formulas.htm
/// https://www.guitarlessonsbybrian.com/chord_formula.pdf
/// https://en.wikibooks.org/wiki/Music_Theory/Complete_List_of_Chord_Patterns
/// https://ragajunglism.org/teaching/jazz-chord-formulas/ 
/// </remarks>
public class ChordBuilder
{
    public void Run()
    {
        var majorFormula = DiatonicIntervalCollection.Parse("1 3 5", null);
        var majorFormulaPcs = majorFormula.PitchClassSet;
        var id = majorFormulaPcs.Id;

        var setFormulaTuples = new List<(PitchClassSet Set, Formula Formula)>();

        var sets = 
            PitchClassSet.Items.Where(
                    set => set is { IsScale: true, IsClusterFree: true } 
                           && 
                           set.Cardinality >= 2 
                           && 
                           set.Cardinality <= 9)
            .ToImmutableList();
        var count = sets.Count;
        var setLookup = sets.ToLookup(set => set.Cardinality);
        var sb = new StringBuilder();
        foreach (var grouping in setLookup)
        {
            foreach (var set in grouping)
            {
                var notes = set.GetDiatonicNotes();
                var root = notes.First();
                var diatonicIntervals = new List<Interval.Simple>();
                foreach (var note in notes)
                {
                    var diatonicInterval = root.GetInterval(note);
                    diatonicIntervals.Add(diatonicInterval);
                }

                var intervals = diatonicIntervals.ToImmutableList().ToImmutableList().AsPrintable();
                var combinations = intervals.ToCombinations();
                foreach (var combination in combinations)
                {
                    if (TryCreateFormula(combination, out var formula))
                    {
                        if (!formula.PitchClassSet.IsScale)
                        {
                            Debugger.Break();
                        }

                        sb.AppendLine($"{set.Id} - {intervals} - formula: {formula}");
                        setFormulaTuples.Add((set, formula));
                    }
                }
            }
        }

        sb.AppendLine("==========");

        foreach (var grouping in setFormulaTuples.ToLookup(tuple => tuple.Item2))
        {
            var formula = grouping.Key;
            var groupingSets = grouping.Select(tuple => tuple.Item1).Distinct().OrderBy(set => set.Id).ToImmutableList();
            var theSet = groupingSets.FirstOrDefault(set => set.Id == formula.PitchClassSet.Id);
            if (theSet != null)
            {
                sb.AppendLine();
                sb.AppendLine($"== Formula: {formula} - {formula.PitchClassSet.Id}");
                sb.AppendLine($"=> {theSet.Id} - {theSet.ScalePageUrl} - {theSet.ScaleVideoUrl}");
            }
            else
            {
                sb.AppendLine($"== Formula: {formula} - {formula.PitchClassSet.Id}");
                sb.AppendLine();
                var set = groupingSets.MinBy(set => set.Id.Value);
                if (set != null)
                {
                    sb.AppendLine($"{set.Id} - {set.ScalePageUrl} - {set.ScaleVideoUrl}");
                }
            }
        }

        sb.AppendLine("==========");

        foreach (var grouping in setFormulaTuples.ToLookup(tuple => tuple.Item2))
        {
            var formula = grouping.Key;
            var groupingSets = grouping.Select(tuple => tuple.Item1).Distinct().OrderBy(set => set.Id).ToImmutableList();
            var theSet = groupingSets.FirstOrDefault(set => set.Id == formula.PitchClassSet.Id);
            if (theSet != null)
            {
            }
            else
            {
                sb.AppendLine($"== Formula: {formula} - {formula.PitchClassSet.Id}");
                sb.AppendLine();
                var set = groupingSets.MinBy(set => set.Id.Value);
                if (set != null)
                {
                    sb.AppendLine($"{set.Id} - {set.ScalePageUrl} - {set.ScaleVideoUrl}");
                }
            }
        }

        var formulas = 
            setFormulaTuples
                .Select(tuple => tuple.Formula)
                .Distinct()
                .ToImmutableList();

        var s = sb.ToString();
    }

    private static bool TryCreateFormula(
        Variation<Interval.Simple> combination,
        out Formula formula)
    {
        formula = null!;

        // Ensure root present
        var intervals = combination.ToImmutableHashSet();
        if (!intervals.Contains(Interval.Simple.P1)) return false;

        // Ensure at least 3 intervals
        if (intervals.Count < 3) return false;

        // Ensure 2nd, 3rd or 4th present
        var intervalSizes = intervals.Select(i => i.Size).ToImmutableHashSet();
        if (!(intervalSizes.Contains(2) ||
              intervalSizes.Contains(3) ||
              intervalSizes.Contains(4)))
        {
            return false;
        }

        // Ensure 5th present
        if (!intervalSizes.Contains(5)) return false;

        // Handle 7th extensions
        var intervalBySize =
            intervals
                .DistinctBy(simple => simple.Size)
                .Cast<Interval.Diatonic>()
                .ToDictionary(simple => simple.Size);
        if (intervalSizes.Contains(3))
        {
            MakeCompound(intervalBySize, [2, 4]);
        }
        else if (intervalSizes.Contains(2) && intervalSizes.Contains(4))
        {
            MakeCompound(intervalBySize, [2]);
        }

        if (intervalSizes.Contains(SimpleIntervalSize.Seventh))
        {
            MakeCompound(intervalBySize, [2, 4, 6]);
        }
        else
        {
            MakeCompound(
                intervalBySize,
                [2, 4, 6],
                size => intervalBySize.TryGetValue(size, out var interval)
                        &&
                        interval.IntervalAccidental.HasValue);
        }

        // Ensure at least 3 intervals remaining
        if (intervalBySize.Values.Count < 3) return false;

        var formulaIntervals =
            intervalBySize.Values
                .Select(interval => interval.ToFormulaInterval())
                .ToImmutableArray();
        formula = new(formulaIntervals);
        return true;
    }

    private static void MakeCompound(
        Dictionary<IIntervalSize, Interval.Diatonic> intervalBySize,
        IEnumerable<SimpleIntervalSize> simpleIntervalSizes,
        Func<IIntervalSize, bool>? predicate = null)
    {
        // Replace 2,4,6 intervals by their compound intervals
        foreach (var intervalSize in simpleIntervalSizes)
        {
            if (!intervalBySize.Remove(intervalSize, out var interval)) continue; // Interval size not found
            if (predicate != null && !predicate(intervalSize)) continue; // Predicate not met

            // Replace by compound interval
            var compoundInterval = ((Interval.Simple)interval).ToCompound();
            intervalBySize.Add(compoundInterval.Size, compoundInterval);
        }
    }
}