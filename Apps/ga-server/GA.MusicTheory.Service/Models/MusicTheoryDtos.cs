namespace GA.MusicTheory.Service.Models;

/// <summary>
///     DTO for musical key information
/// </summary>
public class KeyDto
{
    public required string Name { get; set; }
    public required string Root { get; set; }
    public required string Mode { get; set; }
    public required int KeySignature { get; set; }
    public required string AccidentalKind { get; set; }
    public required List<string> Notes { get; set; }
}

/// <summary>
///     DTO for mode information
/// </summary>
public class ModeDto
{
    public required string Name { get; set; }
    public required int Degree { get; set; }
    public required bool IsMinor { get; set; }
    public required List<string> Intervals { get; set; }
    public required List<string> CharacteristicNotes { get; set; }
}

/// <summary>
///     DTO for scale degree information
/// </summary>
public class ScaleDegreeDto
{
    public required int Degree { get; set; }
    public required string RomanNumeral { get; set; }
    public required string Name { get; set; }
}

/// <summary>
///     DTO for key notes with fretboard positions
/// </summary>
public class KeyNotesDto
{
    public required string KeyName { get; set; }
    public required string Root { get; set; }
    public required string Mode { get; set; }
    public required List<string> Notes { get; set; }
    public required int KeySignature { get; set; }
    public required string AccidentalKind { get; set; }
}

