namespace GA.Business.Core.Analysis.Voicings;

public record PhysicalLayout(int[] FretPositions, int[] StringsUsed, int[] MutedStrings, int[] OpenStrings, int MinFret, int MaxFret, string HandPosition, string StringSet);
