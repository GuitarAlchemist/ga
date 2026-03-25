namespace GA.Domain.Services.Fretboard.Voicings.Analysis;

using System;
using System.Collections.Generic;
using System.Linq;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Core.Instruments.Primitives;

public static class ProgressionVoiceLeadingAnalyzer
{
    private const int PerfectFifth  = 7;  // semitones (mod 12 catches compound intervals)
    private const int PerfectOctave = 0;  // semitones mod 12

    /// <summary>
    /// Analyses an ordered chord progression for parallel perfect fifths and octaves.
    /// </summary>
    public static ProgressionVoiceLeadingReport Analyze(IReadOnlyList<Voicing> progression)
    {
        if (progression.Count < 2)
            return ProgressionVoiceLeadingReport.Clean;

        var issues = new List<ParallelMotionIssue>();
        for (var i = 0; i < progression.Count - 1; i++)
            issues.AddRange(DetectParallelMotion(progression[i], progression[i + 1]));

        return new(issues);
    }

    /// <summary>
    /// Detects parallel perfect fifths or octaves between two consecutive voicings.
    /// Voices are identified by guitar string (1 = highest, 6 = bass).
    /// </summary>
    public static IReadOnlyList<ParallelMotionIssue> DetectParallelMotion(Voicing from, Voicing to)
    {
        var issues = new List<ParallelMotionIssue>();

        var fromMap = BuildVoiceMap(from);
        var toMap   = BuildVoiceMap(to);

        var shared = fromMap.Keys.Intersect(toMap.Keys).OrderBy(s => s).ToList();
        if (shared.Count < 2) return issues;

        for (var a = 0; a < shared.Count - 1; a++)
        for (var b = a + 1; b < shared.Count; b++)
        {
            var strA = shared[a];
            var strB = shared[b];

            var fromA = fromMap[strA]; var toA = toMap[strA];
            var fromB = fromMap[strB]; var toB = toMap[strB];

            if (fromA == toA && fromB == toB) continue;

            var dirA = Math.Sign(toA - fromA);
            var dirB = Math.Sign(toB - fromB);
            if (dirA == 0 || dirB == 0 || dirA != dirB) continue;

            var icFrom = Math.Abs(fromB - fromA) % 12;
            var icTo   = Math.Abs(toB   - toA)   % 12;

            if (icFrom == PerfectFifth && icTo == PerfectFifth)
                issues.Add(new(strA, strB, ParallelMotionType.Fifths,  fromA, toA, fromB, toB));
            else if (icFrom == PerfectOctave && icTo == PerfectOctave)
                issues.Add(new(strA, strB, ParallelMotionType.Octaves, fromA, toA, fromB, toB));
        }

        return issues;
    }

    private static Dictionary<int, int> BuildVoiceMap(Voicing v) =>
        v.Positions
         .OfType<Position.Played>()
         .ToDictionary(p => p.Location.Str.Value, p => p.MidiNote.Value);
}
