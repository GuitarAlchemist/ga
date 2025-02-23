namespace GA.Business.Core.Scales;

using Atonal;
using Atonal.Primitives;
using Extensions;
using Notes;

public class CommonPitchClassSets
{
    public static CommonPitchClassSets Instance => _lazyInstance.Value;
    private static readonly Lazy<CommonPitchClassSets> _lazyInstance = new(() => new()); 
    private readonly ImmutableList<PitchClassSetId> _pitchClassSetIds;

    private CommonPitchClassSets()
    {
        _pitchClassSetIds = GetPitchClassSetIds();
    }

    private static ImmutableList<PitchClassSetId> GetPitchClassSetIds()
    {
        var aa = PitchClassSet.Items.Where(set => set.Cardinality == 7 && set is { IsModal: true }).ToImmutableSortedSet();
        var aaByModalFamilies = aa.ToLookup(set => set.ModalFamily);
        var bb =
            aaByModalFamilies.Where(grouping => grouping.Key is { IntervalClassVector.Hemitonia: <= 2 })
                .OrderBy(grouping => grouping.Key!.IntervalClassVector.Hemitonia);

        var sb = new StringBuilder();
        foreach (var grouping in aaByModalFamilies)
        {
            sb.AppendLine("========");
            sb.AppendLine(grouping.Key!.IntervalClassVector.ToString());
            foreach (var item in grouping)
            {
                sb.AppendLine($"{item.Id} - https://ianring.com/musictheory/scales/{item.Id}");
            }
        }
        var s = sb.ToString();

        // Major scale and modes
        var majorScalePcs = AccidentedNoteCollection.Parse("C D E F G A B").ToPitchClassSet();
        var id = majorScalePcs.Id;

        // See https://en.wikipedia.org/wiki/Heptatonic_scale
        // See https://en.wikipedia.org/wiki/Anhemitonic_scale#Modes_of_the_ancohemitonic_heptatonic_scales_and_the_key_signature_system

        return ImmutableList<PitchClassSetId>.Empty;
    }

    public sealed record PitchClassSetInfo(PitchClassSetId Id, PitchClassSetObject Object);

    public abstract record PitchClassSetObject
    {
        public sealed record Scale() : PitchClassSetObject;

        public sealed record ModalScale() : PitchClassSetObject;

        public sealed record Chord() : PitchClassSetObject;
    }
}