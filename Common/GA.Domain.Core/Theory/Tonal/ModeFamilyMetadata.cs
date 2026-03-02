namespace GA.Domain.Core.Theory.Tonal;

using Atonal;

public sealed record ModeFamilyMetadata(
    string FamilyName,
    int NoteCount,
    string[] ModeNames,
    List<PitchClassSetId> ModeIds);
