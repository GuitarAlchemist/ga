namespace GA.Domain.Core.Instruments.Fretboard.Voicings.Analysis;

public record PlayabilityInfo(string Difficulty, int HandStretch, bool BarreRequired, string? BarreInfo, int MinimumFingers, string? CagedShape, string? ShellFamily, double DifficultyScore);