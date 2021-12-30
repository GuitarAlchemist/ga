namespace GA.Business.Core.Notes;

using Intervals;

[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Pitch(Octave Octave)
{
    public abstract Note Note { get; }

    public MidiNote GetMidiNote()
    {
        return new()
        {
            Value = 
                (Octave.Value - Octave.Min.Value) * 12 
                + Note.PitchClass.Value
        };
    }

    public override string ToString() => $"{Note.NaturalNote}{Octave}{Note.Accidental}";

    /// <inheritdoc cref="Pitch"/>
    /// <summary>
    /// A sharp pitch.
    /// </summary>
    [PublicAPI]
    public sealed partial record Sharp(
            Note.Sharp SharpNote,
            Octave Octave)
        : Pitch(Octave)
    {
        public static Sharp C(Octave octave) => new(Note.Sharp.C, octave);
        public static Sharp CSharp(Octave octave) => new(Note.Sharp.CSharp, octave);
        public static Sharp D(Octave octave) => new(Note.Sharp.D, octave);
        public static Sharp DSharp(Octave octave) => new(Note.Sharp.DSharp, octave);
        public static Sharp E(Octave octave) => new(Note.Sharp.E, octave);
        public static Sharp F(Octave octave) => new(Note.Sharp.F, octave);
        public static Sharp FSharp(Octave octave) => new(Note.Sharp.FSharp, octave);
        public static Sharp G(Octave octave) => new(Note.Sharp.G, octave);
        public static Sharp GSharp(Octave octave) => new(Note.Sharp.GSharp, octave);
        public static Sharp A(Octave octave) => new(Note.Sharp.A, octave);
        public static Sharp ASharp(Octave octave) => new(Note.Sharp.ASharp, octave);
        public static Sharp B(Octave octave) => new(Note.Sharp.B, octave);

        public static Sharp C2 => Sharp2(Note.Sharp.C);
        public static Sharp C2Sharp=> Sharp2(Note.Sharp.CSharp);
        public static Sharp D2 => Sharp2(Note.Sharp.D);
        public static Sharp D2Sharp => Sharp2(Note.Sharp.DSharp);
        public static Sharp E2 => Sharp2(Note.Sharp.E);
        public static Sharp F2 => Sharp2(Note.Sharp.F);
        public static Sharp F2Sharp => Sharp2(Note.Sharp.FSharp);
        public static Sharp G2 => Sharp2(Note.Sharp.G);
        public static Sharp G2Sharp => Sharp2(Note.Sharp.GSharp);
        public static Sharp A2 => Sharp2(Note.Sharp.A);
        public static Sharp A2Sharp => Sharp2(Note.Sharp.ASharp);
        public static Sharp B2 => Sharp2(Note.Sharp.B);

        public static Sharp C3 => Sharp3(Note.Sharp.C);
        public static Sharp C3Sharp=> Sharp3(Note.Sharp.CSharp);
        public static Sharp D3 => Sharp3(Note.Sharp.D);
        public static Sharp D3Sharp => Sharp3(Note.Sharp.DSharp);
        public static Sharp E3 => Sharp3(Note.Sharp.E);
        public static Sharp F3 => Sharp3(Note.Sharp.F);
        public static Sharp F3Sharp => Sharp3(Note.Sharp.FSharp);
        public static Sharp G3 => Sharp3(Note.Sharp.G);
        public static Sharp G3Sharp => Sharp3(Note.Sharp.GSharp);
        public static Sharp A3 => Sharp3(Note.Sharp.A);
        public static Sharp A3Sharp => Sharp3(Note.Sharp.ASharp);
        public static Sharp B3 => Sharp3(Note.Sharp.B);

        public static Sharp C4 => Sharp4(Note.Sharp.C);
        public static Sharp C4Sharp=> Sharp4(Note.Sharp.CSharp);
        public static Sharp D4 => Sharp4(Note.Sharp.D);
        public static Sharp D4Sharp => Sharp4(Note.Sharp.DSharp);
        public static Sharp E4 => Sharp4(Note.Sharp.E);
        public static Sharp F4 => Sharp4(Note.Sharp.F);
        public static Sharp F4Sharp => Sharp4(Note.Sharp.FSharp);
        public static Sharp G4 => Sharp4(Note.Sharp.G);
        public static Sharp G4Sharp => Sharp4(Note.Sharp.GSharp);
        public static Sharp A4 => Sharp4(Note.Sharp.A);
        public static Sharp A4Sharp => Sharp4(Note.Sharp.ASharp);
        public static Sharp B4 => Sharp4(Note.Sharp.B);

        private static Sharp Sharp2(Note.Sharp note) => new(note, 2);
        private static Sharp Sharp3(Note.Sharp note) => new(note, 3);
        private static Sharp Sharp4(Note.Sharp note) => new(note, 4);

        public override Note Note => SharpNote;
    }

    /// <inheritdoc cref="Pitch"/>
    /// <summary>
    /// A flat pitch.
    /// </summary>
    [PublicAPI]
    public sealed partial record Flat(
            Note.Flat FlatNote,
            Octave Octave,
            FlatAccidental? Accidental = null)
        : Pitch(Octave)
    {
        public static Flat C(Octave octave) => new(Note.Flat.C, octave);
        public static Flat DFlat(Octave octave) => new(Note.Flat.DFlat, octave);
        public static Flat D(Octave octave) => new(Note.Flat.D, octave);
        public static Flat EFlat(Octave octave) => new(Note.Flat.EFlat, octave);
        public static Flat E(Octave octave) => new(Note.Flat.E, octave);
        public static Flat F(Octave octave) => new(Note.Flat.F, octave);
        public static Flat GFlat(Octave octave) => new(Note.Flat.GFlat, octave);
        public static Flat G(Octave octave) => new(Note.Flat.G, octave);
        public static Flat AFlat(Octave octave) => new(Note.Flat.AFlat, octave);
        public static Flat A(Octave octave) => new(Note.Flat.A, octave);
        public static Flat ASharp(Octave octave) => new(Note.Flat.BFlat, octave);
        public static Flat B(Octave octave) => new(Note.Flat.B, octave);

        public override Note Note => FlatNote;
    }
}