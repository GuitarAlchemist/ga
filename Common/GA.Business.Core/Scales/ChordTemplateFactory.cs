﻿﻿﻿﻿﻿﻿﻿namespace GA.Business.Core.Scales;

using Atonal;
using Extensions;
using Notes;

public class ChordTemplateFactory
{
    public static ImmutableList<ChordTemplate> CreateAllChordTemplates()
    {
        var chordTemplateByPcs = new Dictionary<PitchClassSet, ChordTemplate>();

        // Add common chord types explicitly
        AddCommonTriads(chordTemplateByPcs);
        AddCommonSeventhChords(chordTemplateByPcs);
        AddExtendedChords(chordTemplateByPcs);
        AddSuspendedAndAddedToneChords(chordTemplateByPcs);

        // Process scales
        foreach (var scale in CommonScales.Instance)
        {
            ProcessScale(scale, chordTemplateByPcs);

            if (scale is not IModalScale modalScale) continue; // Scale is not modal, skip

            // Process scale modes
            foreach (var mode in modalScale.Modes)
            {
                ProcessScale(new Scale(mode.Notes), chordTemplateByPcs);
            }
        }

        return chordTemplateByPcs.Values.ToImmutableList();
    }

    private static void AddCommonTriads(Dictionary<PitchClassSet, ChordTemplate> chordTemplateByPcs)
    {
        // Major triad (C E G) - PitchClassSetId 145
        var majorTriadPcs = PitchClassSet.FromId(145);
        var majorTriadTemplate = new ChordTemplate(majorTriadPcs);
        chordTemplateByPcs[majorTriadPcs] = majorTriadTemplate;

        // Minor triad (C Eb G) - PitchClassSetId 137
        var minorTriadPcs = PitchClassSet.FromId(137);
        var minorTriadTemplate = new ChordTemplate(minorTriadPcs);
        chordTemplateByPcs[minorTriadPcs] = minorTriadTemplate;

        // Diminished triad (C Eb Gb) - PitchClassSetId 73
        var dimTriadPcs = PitchClassSet.FromId(73);
        var dimTriadTemplate = new ChordTemplate(dimTriadPcs);
        chordTemplateByPcs[dimTriadPcs] = dimTriadTemplate;

        // Augmented triad (C E G#) - PitchClassSetId 273
        var augTriadPcs = PitchClassSet.FromId(273);
        var augTriadTemplate = new ChordTemplate(augTriadPcs);
        chordTemplateByPcs[augTriadPcs] = augTriadTemplate;
    }

    private static void AddCommonSeventhChords(Dictionary<PitchClassSet, ChordTemplate> chordTemplateByPcs)
    {
        // Major seventh (C E G B) - PitchClassSetId 1169
        var maj7Pcs = PitchClassSet.FromId(1169);
        var maj7Template = new ChordTemplate(maj7Pcs);
        chordTemplateByPcs[maj7Pcs] = maj7Template;

        // Dominant seventh (C E G Bb) - PitchClassSetId 1165
        var dom7Pcs = PitchClassSet.FromId(1165);
        var dom7Template = new ChordTemplate(dom7Pcs);
        chordTemplateByPcs[dom7Pcs] = dom7Template;

        // Minor seventh (C Eb G Bb) - PitchClassSetId 1161
        var min7Pcs = PitchClassSet.FromId(1161);
        var min7Template = new ChordTemplate(min7Pcs);
        chordTemplateByPcs[min7Pcs] = min7Template;

        // Half-diminished seventh (C Eb Gb Bb) - PitchClassSetId 1097
        var halfDim7Pcs = PitchClassSet.FromId(1097);
        var halfDim7Template = new ChordTemplate(halfDim7Pcs);
        chordTemplateByPcs[halfDim7Pcs] = halfDim7Template;

        // Diminished seventh (C Eb Gb A) - PitchClassSetId 585
        var dim7Pcs = PitchClassSet.FromId(585);
        var dim7Template = new ChordTemplate(dim7Pcs);
        chordTemplateByPcs[dim7Pcs] = dim7Template;
    }

    private static void AddExtendedChords(Dictionary<PitchClassSet, ChordTemplate> chordTemplateByPcs)
    {
        // Major ninth (C E G B D) - PitchClassSetId 1173
        var maj9Pcs = PitchClassSet.FromId(1173);
        var maj9Template = new ChordTemplate(maj9Pcs);
        chordTemplateByPcs[maj9Pcs] = maj9Template;

        // Dominant ninth (C E G Bb D) - PitchClassSetId 1171
        var dom9Pcs = PitchClassSet.FromId(1171);
        var dom9Template = new ChordTemplate(dom9Pcs);
        chordTemplateByPcs[dom9Pcs] = dom9Template;

        // Minor ninth (C Eb G Bb D) - PitchClassSetId 1169);
        var min9Pcs = PitchClassSet.FromId(1169);
        var min9Template = new ChordTemplate(min9Pcs);
        chordTemplateByPcs[min9Pcs] = min9Template;

        // Major eleventh (C E G B D F) - PitchClassSetId 1175
        var maj11Pcs = PitchClassSet.FromId(1175);
        var maj11Template = new ChordTemplate(maj11Pcs);
        chordTemplateByPcs[maj11Pcs] = maj11Template;

        // Dominant eleventh (C E G Bb D F) - PitchClassSetId 1173
        var dom11Pcs = PitchClassSet.FromId(1173);
        var dom11Template = new ChordTemplate(dom11Pcs);
        chordTemplateByPcs[dom11Pcs] = dom11Template;

        // Minor eleventh (C Eb G Bb D F) - PitchClassSetId 1171
        var min11Pcs = PitchClassSet.FromId(1171);
        var min11Template = new ChordTemplate(min11Pcs);
        chordTemplateByPcs[min11Pcs] = min11Template;

        // Major thirteenth (C E G B D F A) - PitchClassSetId 1183
        var maj13Pcs = PitchClassSet.FromId(1183);
        var maj13Template = new ChordTemplate(maj13Pcs);
        chordTemplateByPcs[maj13Pcs] = maj13Template;

        // Dominant thirteenth (C E G Bb D F A) - PitchClassSetId 1181
        var dom13Pcs = PitchClassSet.FromId(1181);
        var dom13Template = new ChordTemplate(dom13Pcs);
        chordTemplateByPcs[dom13Pcs] = dom13Template;

        // Minor thirteenth (C Eb G Bb D F A) - PitchClassSetId 1179
        var min13Pcs = PitchClassSet.FromId(1179);
        var min13Template = new ChordTemplate(min13Pcs);
        chordTemplateByPcs[min13Pcs] = min13Template;
    }

    private static void AddSuspendedAndAddedToneChords(Dictionary<PitchClassSet, ChordTemplate> chordTemplateByPcs)
    {
        // Suspended 2nd (C D G) - PitchClassSetId 133
        var sus2Pcs = PitchClassSet.FromId(133);
        var sus2Template = new ChordTemplate(sus2Pcs);
        chordTemplateByPcs[sus2Pcs] = sus2Template;

        // Suspended 4th (C F G) - PitchClassSetId 149
        var sus4Pcs = PitchClassSet.FromId(149);
        var sus4Template = new ChordTemplate(sus4Pcs);
        chordTemplateByPcs[sus4Pcs] = sus4Template;

        // Added 9th (C E G D) - PitchClassSetId 149
        var add9Pcs = PitchClassSet.FromId(149);
        var add9Template = new ChordTemplate(add9Pcs);
        chordTemplateByPcs[add9Pcs] = add9Template;

        // Added 11th (C E G F) - PitchClassSetId 153
        var add11Pcs = PitchClassSet.FromId(153);
        var add11Template = new ChordTemplate(add11Pcs);
        chordTemplateByPcs[add11Pcs] = add11Template;

        // 6th chord (C E G A) - PitchClassSetId 169
        var sixth6Pcs = PitchClassSet.FromId(169);
        var sixth6Template = new ChordTemplate(sixth6Pcs);
        chordTemplateByPcs[sixth6Pcs] = sixth6Template;

        // Minor 6th chord (C Eb G A) - PitchClassSetId 165
        var minorSixth6Pcs = PitchClassSet.FromId(165);
        var minorSixth6Template = new ChordTemplate(minorSixth6Pcs);
        chordTemplateByPcs[minorSixth6Pcs] = minorSixth6Template;

        // 6/9 chord (C E G A D) - PitchClassSetId 173
        var sixth9Pcs = PitchClassSet.FromId(173);
        var sixth9Template = new ChordTemplate(sixth9Pcs);
        chordTemplateByPcs[sixth9Pcs] = sixth9Template;
    }

    private static void ProcessScale(Scale scale, Dictionary<PitchClassSet, ChordTemplate> chordTemplateByPcs)
    {
        var scaleNotes = scale.ToImmutableList();

        for (var chordSize = 3; chordSize <= Math.Min(5, scale.Count); chordSize++)
        {
            for (var startIndex = 0; startIndex < scale.Count; startIndex++)
            {
                var chordNotes = GetCircularSubset(scaleNotes, startIndex, chordSize);
                var chordPcs = chordNotes.ToPitchClassSet();

                if (!chordTemplateByPcs.TryGetValue(chordPcs, out var template))
                {
                    template = new ChordTemplate(chordPcs);
                    chordTemplateByPcs[chordPcs] = template;
                }
                template.AddAssociatedScale(scale);
            }
        }
    }

    private static IEnumerable<Note> GetCircularSubset(IReadOnlyList<Note> notes, int startIndex, int count) =>
        Enumerable
            .Range(0, count)
            .Select(i => notes[(startIndex + i) % notes.Count]);
}