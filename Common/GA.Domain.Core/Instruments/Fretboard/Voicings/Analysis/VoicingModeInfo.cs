namespace GA.Domain.Core.Instruments.Fretboard.Voicings.Analysis;

public record VoicingModeInfo(string ModeName, int Degree, string FamilyName, int DegreeInFamily = 0, int NoteCount = 0);