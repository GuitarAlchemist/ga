namespace GA.Business.Core.Scales;

using Atonal;
using Atonal.Primitives;
using Extensions;
using Microsoft.FSharp.Linq.RuntimeHelpers;
using Notes;

public class CommonPitchClassSets
{
    public static CommonPitchClassSets Instance => _lazyInstance.Value;
    private static readonly Lazy<CommonPitchClassSets> _lazyInstance = new(() => new CommonPitchClassSets()); 
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
            aaByModalFamilies.Where(grouping => grouping.Key.IntervalClassVector.Hemitonia <= 2)
                .OrderBy(grouping => grouping.Key.IntervalClassVector.Hemitonia);

        var sb = new StringBuilder();
        foreach (var grouping in aaByModalFamilies)
        {
            sb.AppendLine("========");
            sb.AppendLine(grouping.Key.IntervalClassVector.ToString());
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

public class CommonScales : IEnumerable<Scale>
{
    public static readonly CommonScales Instance = new();
    private readonly ImmutableList<ScaleInfo> _scaleInfos;

    private CommonScales()
    {
        _scaleInfos = GetScaleInfos();
    }

    private static ImmutableList<ScaleInfo> GetScaleInfos()
    {
        var scaleInfosBuilder = ImmutableList.CreateBuilder<ScaleInfo>();
        scaleInfosBuilder.Add(new ScaleInfo("Major", new("C D E F G A B")));
        scaleInfosBuilder.Add(new ScaleInfo("Natural Minor", new("A B C D E F G")));
        scaleInfosBuilder.Add(new ScaleInfo("Harmonic Minor", new("A B C D E F G#")));
        scaleInfosBuilder.Add(new ScaleInfo("Melodic Minor", new("A B C D E F# G#")));
        
        //scaleByName["Pentatonic Major"] = new("C D E G A");
        //scaleByName["Blues"] = new("C Eb F F# G Bb");

        //scaleByName["Whole Tone"] = new("C D E F# G# A#");
        //scaleByName["Chromatic (Sharp)"] = new("C C# D D# E F F# G G# A A# B");
        //scaleByName["Chromatic (Flat)"] = new("C Db D Eb E F Gb G Ab A Bb B");

        return scaleInfosBuilder.ToImmutable();
    }

    public IEnumerator<Scale> GetEnumerator()
    {
        return _scaleInfos.Select(info => info.Scale).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private sealed record ScaleInfo(string ScaleName, Scale Scale);
}