using System.Collections.Immutable;
using GA.Business.Core.Intervals;

namespace GA.Business.Core.Notes;

[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Note(
    PitchClass PitchClass,
    NaturalNote NaturalNote)
{
    public abstract Accidental? Accidental { get; }

    public override string ToString() =>
        Accidental.HasValue
            ? $"{NaturalNote}{Accidental.Value}"
            : $"{NaturalNote}";

    /// <inheritdoc cref="Note"/>
    /// <summary>
    /// A sharp note.
    /// </summary>
    public sealed partial record Sharp(
            PitchClass PitchClass,
            NaturalNote NaturalNote,
            SharpAccidental? SharpAccidental = null)
        : Note(PitchClass, NaturalNote)
    {
        public static Sharp C => new(0, NaturalNote.C);
        public static Sharp CSharp => new(1, NaturalNote.C, Notes.SharpAccidental.Sharp);
        public static Sharp D => new(2, NaturalNote.D);
        public static Sharp DSharp => new(3, NaturalNote.D, Notes.SharpAccidental.Sharp);
        public static Sharp E => new(4, NaturalNote.E);
        public static Sharp F => new(5, NaturalNote.F);
        public static Sharp FSharp => new(6, NaturalNote.F, Notes.SharpAccidental.Sharp);
        public static Sharp G => new(7, NaturalNote.G);
        public static Sharp GSharp => new(8, NaturalNote.G, Notes.SharpAccidental.Sharp);
        public static Sharp A => new(9, NaturalNote.A);
        public static Sharp ASharp => new(10, NaturalNote.A, Notes.SharpAccidental.Sharp);
        public static Sharp B => new(11, NaturalNote.B);

        public static IReadOnlyCollection<Sharp> Values => new[] {C, CSharp, D, DSharp, E, F, FSharp, G, GSharp, A, ASharp, B};
        public static Sharp FromPitchClass(PitchClass pitchClass) => ValueByPitchClass[pitchClass];
        private static readonly IImmutableDictionary<PitchClass, Sharp> ValueByPitchClass = Values.ToImmutableDictionary(note => note.PitchClass);

        public override Accidental? Accidental => SharpAccidental;
    }

    /// <inheritdoc cref="Note"/>
    /// <summary>
    /// A sharp note.
    /// </summary>
    public sealed partial record Flat(
            PitchClass PitchClass,
            NaturalNote NaturalNote,
            FlatAccidental? FlatAccidental = null)
        : Note(PitchClass, NaturalNote)
    {
        public static Flat C => new(0, NaturalNote.C);
        public static Flat DFlat => new(1, NaturalNote.D, Notes.FlatAccidental.Flat);
        public static Flat D => new(2, NaturalNote.D);
        public static Flat EFlat => new(3, NaturalNote.E, Notes.FlatAccidental.Flat);
        public static Flat E => new(4, NaturalNote.E);
        public static Flat F => new(5, NaturalNote.F);
        public static Flat GFlat => new(6, NaturalNote.G, Notes.FlatAccidental.Flat);
        public static Flat G => new(7, NaturalNote.G);
        public static Flat AFlat => new(8, NaturalNote.A, Notes.FlatAccidental.Flat);
        public static Flat A => new(9, NaturalNote.A);
        public static Flat BFlat => new(10, NaturalNote.B, Notes.FlatAccidental.Flat);
        public static Flat B => new(11, NaturalNote.B);

        public static IReadOnlyCollection<Flat> Values => new[] {C, DFlat, D, EFlat, E, F, GFlat, G, AFlat, A, BFlat, B};
        public static Flat FromPitchClass(PitchClass pitchClass) => ValueByPitchClass[pitchClass];
        private static readonly IImmutableDictionary<PitchClass, Flat> ValueByPitchClass = Values.ToImmutableDictionary(note => note.PitchClass);

        public override Accidental? Accidental => FlatAccidental;
    }
}