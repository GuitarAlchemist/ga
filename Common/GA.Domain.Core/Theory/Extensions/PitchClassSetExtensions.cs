namespace GA.Domain.Core.Theory.Extensions;

using Atonal;
using Primitives.Formulas;
using Primitives.Notes;

public static class PitchClassSetExtensions
{
    public static PitchClassSet ToPitchClassSet(this AccidentedNoteCollection notes) => new(notes.Select(n => n.PitchClass));

    public static PitchClassSet ToPitchClassSet(this IEnumerable<Note.Chromatic> notes) => new(notes.Select(n => n.PitchClass));

    public static PitchClassSet ToPitchClassSet(this IEnumerable<Note> notes) => new(notes.Select(n => n.PitchClass));

    public static PitchClassSet ToPitchClassSet(this IEnumerable<FormulaIntervalBase> intervals) => new(intervals.Select(i => i.PitchClass));
}
