namespace GA.Domain.Core.Instruments.Fretboard.Voicings.Analysis;

public record ErgonomicsInfo(int StringSkips, string? FingerAssignment, bool RequiresThumb, bool IsImpossible, string? Notes);