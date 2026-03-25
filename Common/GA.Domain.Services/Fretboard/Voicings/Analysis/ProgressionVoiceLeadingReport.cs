namespace GA.Domain.Services.Fretboard.Voicings.Analysis;

using System.Collections.Generic;
using System.Linq;

public enum ParallelMotionType { Fifths, Octaves }

public record ParallelMotionIssue(
    int StringA,
    int StringB,
    ParallelMotionType Type,
    int FromMidiA,
    int ToMidiA,
    int FromMidiB,
    int ToMidiB);

public record ProgressionVoiceLeadingReport(
    IReadOnlyList<ParallelMotionIssue> Issues)
{
    public bool HasParallelFifths  => Issues.Any(i => i.Type == ParallelMotionType.Fifths);
    public bool HasParallelOctaves => Issues.Any(i => i.Type == ParallelMotionType.Octaves);
    public bool IsClean            => Issues.Count == 0;

    public static ProgressionVoiceLeadingReport Clean { get; } =
        new([]);
}
