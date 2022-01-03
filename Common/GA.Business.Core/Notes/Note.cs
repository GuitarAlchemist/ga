using System.Collections.Immutable;
using GA.Business.Core.Intervals;

namespace GA.Business.Core.Notes;

[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Note(NaturalNote NaturalNote)
{
    public abstract Accidental? Accidental { get; }
    public abstract PitchClass PitchClass { get; }

    /// <inheritdoc cref="Note"/>
    /// <summary>
    /// A sharp note.
    /// </summary>
    public sealed partial record Sharp(
            NaturalNote NaturalNote,
            SharpAccidental? SharpAccidental = null)
        : Note(NaturalNote)
    {
        public static Sharp C => new(NaturalNote.C);
        public static Sharp CSharp => new(NaturalNote.C, Notes.SharpAccidental.Sharp);
        public static Sharp D => new(NaturalNote.D);
        public static Sharp DSharp => new(NaturalNote.D, Notes.SharpAccidental.Sharp);
        public static Sharp E => new(NaturalNote.E);
        public static Sharp F => new(NaturalNote.F);
        public static Sharp FSharp => new(NaturalNote.F, Notes.SharpAccidental.Sharp);
        public static Sharp G => new(NaturalNote.G);
        public static Sharp GSharp => new( NaturalNote.G, Notes.SharpAccidental.Sharp);
        public static Sharp A => new( NaturalNote.A);
        public static Sharp ASharp => new( NaturalNote.A, Notes.SharpAccidental.Sharp);
        public static Sharp B => new(NaturalNote.B);

        public override Accidental? Accidental => SharpAccidental;
        public override PitchClass PitchClass => NaturalNote.GetPitchClass() + (SharpAccidental?.Value ?? 0);

        public override string ToString() =>
            SharpAccidental.HasValue
                ? $"{NaturalNote}{SharpAccidental.Value}"
                : $"{NaturalNote}";
    }

    /// <inheritdoc cref="Note"/>
    /// <summary>
    /// A sharp note.
    /// </summary>
    public sealed partial record Flat(
            NaturalNote NaturalNote,
            FlatAccidental? FlatAccidental = null)
        : Note(NaturalNote)
    {
        public static Flat CFlat => new(NaturalNote.C, Notes.FlatAccidental.Flat);
        public static Flat C => new( NaturalNote.C);
        public static Flat DFlat => new(NaturalNote.D, Notes.FlatAccidental.Flat);
        public static Flat D => new( NaturalNote.D);
        public static Flat EFlat => new(NaturalNote.E, Notes.FlatAccidental.Flat);
        public static Flat E => new(NaturalNote.E);
        public static Flat FFlat => new(NaturalNote.F, Notes.FlatAccidental.Flat);
        public static Flat F => new(NaturalNote.F);
        public static Flat GFlat => new(NaturalNote.G, Notes.FlatAccidental.Flat);
        public static Flat G => new(NaturalNote.G);
        public static Flat AFlat => new(NaturalNote.A, Notes.FlatAccidental.Flat);
        public static Flat A => new(NaturalNote.A);
        public static Flat BFlat => new( NaturalNote.B, Notes.FlatAccidental.Flat);
        public static Flat B => new( NaturalNote.B);

        public override Accidental? Accidental => FlatAccidental;
        public override PitchClass PitchClass => NaturalNote.GetPitchClass() + FlatAccidental?.Value ?? 0;

        public override string ToString() =>
            FlatAccidental.HasValue
                ? $"{NaturalNote}{FlatAccidental.Value}"
                : $"{NaturalNote}";

    }
}