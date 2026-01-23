namespace GA.Domain.Core.Extensions;

using System.Collections.Generic;
using System.Linq;
using Primitives;
using Theory.Atonal;

public static class PitchClassSetExtensions
{
    public static PitchClassSet ToPitchClassSet(this AccidentedNoteCollection notes)
    {
        return new PitchClassSet(notes.Select(n => n.PitchClass));
    }

    public static PitchClassSet ToPitchClassSet(this IEnumerable<Note.Chromatic> notes)
    {
        return new PitchClassSet(notes.Select(n => n.PitchClass));
    }

    public static PitchClassSet ToPitchClassSet(this IEnumerable<Note> notes)
    {
        return new PitchClassSet(notes.Select(n => n.PitchClass));
    }

    public static PitchClassSet ToPitchClassSet(this IEnumerable<FormulaIntervalBase> intervals)
    {
        return new PitchClassSet(intervals.Select(i => i.PitchClass));
    }
}
