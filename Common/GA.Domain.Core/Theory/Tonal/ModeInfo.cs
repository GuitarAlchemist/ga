namespace GA.Domain.Core.Theory.Tonal;

public sealed record ModeInfo(
    string FamilyName,
    string ModeName,
    int Degree,
    IReadOnlyList<string> CharacteristicIntervals);
