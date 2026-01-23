namespace GA.Domain.Core.Instruments.Fretboard.Voicings.Analysis;

public record PhysicalLayout(int[] FretPositions, int[] StringsUsed, int[] MutedStrings, int[] OpenStrings, int MinFret, int MaxFret, string HandPosition, string StringSet);