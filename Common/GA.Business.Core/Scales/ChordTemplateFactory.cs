namespace GA.Business.Core.Scales;

using Atonal;
using Extensions;
using Notes;

public class ChordTemplateFactory
{
    public static ImmutableList<ChordTemplate> CreateAllChordTemplates()
    {
        var chordTemplateByPcs = new Dictionary<PitchClassSet, ChordTemplate>();

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