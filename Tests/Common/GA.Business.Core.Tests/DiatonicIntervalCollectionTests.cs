namespace GA.Business.Core.Tests;

using System.Text;
using Chords;
using Core.Atonal;
using Core.Atonal.Primitives;
using Extensions;
using GA.Business.Core.Scales;
using Intervals;

[TestFixture]
public class DiatonicIntervalCollectionTests
{
    [Test]
    public void DiatonicIntervalCollection_Parse_MajorChordFormula()
    {
        var aa = DiatonicIntervalCollection.Parse("1 3 5", null);
    }

    [Test]
    public void ChordBuilder_Run()
    {
        // https://www.reddit.com/r/jazztheory/comments/hkvdp9/42_modes_all_ancohemitonic_heptatonics_each_step/
        // https://chromatone.center/
        var pcs = PitchClassSetId.FromValue(803).ToPitchClassSet(); // Loritonic
        var normalPcs = pcs.ToNormalForm();
        var notes = pcs.GetDiatonicNotes();
        var sNotes = notes.PrintOut;
        var key = pcs.ClosestDiatonicKey;

        // ==
        var sets = PitchClassSet.Items.Where(set =>
                set is { IsScale: true, IsClusterFree: true } 
                && 
                set.Cardinality >= 3 
                && 
                set.Cardinality <= 9)
            .ToImmutableList();
        var count = sets.Count;
        var setLookup = sets.ToLookup(set => set.Cardinality);
        var sb = new StringBuilder();
        foreach (var grouping in setLookup)
        {
            sb.AppendLine($"Cardinality: {grouping.Key}");
            foreach (var modePcs in grouping)
            {
                var diatonicNotes = modePcs.GetDiatonicNotes();
                var sDiatonicNotes = string.Join(" ", diatonicNotes);
                sb.AppendLine($"{modePcs.Id.Value} - {sDiatonicNotes}");
            }
        }
        var modes = sb.ToString();

        var aa = new ChordBuilder();
        aa.Run();

        var intervalStructure = IntervalStructure.Parse("2 2 1 2 2 2 1", null);
        var pcs1 = intervalStructure.IntervalsFromRoot.PitchClassSet;
        var icv = pcs1.IntervalClassVector;
    }
}