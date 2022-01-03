namespace GA.Business.Core.Notes;

using Intervals;

[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Pitch(Octave Octave) : IComparable<Pitch>
{
    public int CompareTo(Pitch? other)
    {
        return other is null ? 1 : MidiNote.CompareTo(other.MidiNote);
    }

    public static bool operator <(Pitch? left, Pitch? right) => Comparer<Pitch>.Default.Compare(left, right) < 0;
    public static bool operator >(Pitch? left, Pitch? right) => Comparer<Pitch>.Default.Compare(left, right) > 0;
    public static bool operator <=(Pitch? left, Pitch? right) => Comparer<Pitch>.Default.Compare(left, right) <= 0;
    public static bool operator >=(Pitch? left, Pitch? right) => Comparer<Pitch>.Default.Compare(left, right) >= 0;

    protected abstract PitchClass GetPitchClass();

    public PitchClass PitchClass => GetPitchClass();
    public MidiNote MidiNote => GetMidiNote();

    public static implicit operator MidiNote(Pitch pitch) => pitch.GetMidiNote();

    private MidiNote GetMidiNote()
    {
        var pitchClass = GetPitchClass();
        var value = (Octave.Value - Octave.Min.Value) * 12
                    + pitchClass.Value;

        return new() {Value = value};
    }

    /// <inheritdoc cref="Pitch"/>
    /// <summary>
    /// A sharp pitch.
    /// </summary>
    [PublicAPI]
    public sealed partial record Sharp(Note.Sharp Note, Octave Octave) : Pitch(Octave)
    {
        public static Sharp C(Octave octave) => new(Notes.Note.Sharp.C, octave);
        public static Sharp CSharp(Octave octave) => new(Notes.Note.Sharp.CSharp, octave);
        public static Sharp D(Octave octave) => new(Notes.Note.Sharp.D, octave);
        public static Sharp DSharp(Octave octave) => new(Notes.Note.Sharp.DSharp, octave);
        public static Sharp E(Octave octave) => new(Notes.Note.Sharp.E, octave);
        public static Sharp F(Octave octave) => new(Notes.Note.Sharp.F, octave);
        public static Sharp FSharp(Octave octave) => new(Notes.Note.Sharp.FSharp, octave);
        public static Sharp G(Octave octave) => new(Notes.Note.Sharp.G, octave);
        public static Sharp GSharp(Octave octave) => new(Notes.Note.Sharp.GSharp, octave);
        public static Sharp A(Octave octave) => new(Notes.Note.Sharp.A, octave);
        public static Sharp ASharp(Octave octave) => new(Notes.Note.Sharp.ASharp, octave);
        public static Sharp B(Octave octave) => new(Notes.Note.Sharp.B, octave);

        public static Sharp C2 => Sharp2(Notes.Note.Sharp.C);
        public static Sharp CSharp2=> Sharp2(Notes.Note.Sharp.CSharp);
        public static Sharp D2 => Sharp2(Notes.Note.Sharp.D);
        public static Sharp DSharp2 => Sharp2(Notes.Note.Sharp.DSharp);
        public static Sharp E2 => Sharp2(Notes.Note.Sharp.E);
        public static Sharp F2 => Sharp2(Notes.Note.Sharp.F);
        public static Sharp FSharp2 => Sharp2(Notes.Note.Sharp.FSharp);
        public static Sharp G2 => Sharp2(Notes.Note.Sharp.G);
        public static Sharp GSharp2 => Sharp2(Notes.Note.Sharp.GSharp);
        public static Sharp A2 => Sharp2(Notes.Note.Sharp.A);
        public static Sharp ASharp2 => Sharp2(Notes.Note.Sharp.ASharp);
        public static Sharp B2 => Sharp2(Notes.Note.Sharp.B);

        public static Sharp C3 => Sharp3(Notes.Note.Sharp.C);
        public static Sharp CSharp3=> Sharp3(Notes.Note.Sharp.CSharp);
        public static Sharp D3 => Sharp3(Notes.Note.Sharp.D);
        public static Sharp DSharp3 => Sharp3(Notes.Note.Sharp.DSharp);
        public static Sharp E3 => Sharp3(Notes.Note.Sharp.E);
        public static Sharp F3 => Sharp3(Notes.Note.Sharp.F);
        public static Sharp FSharp3 => Sharp3(Notes.Note.Sharp.FSharp);
        public static Sharp G3 => Sharp3(Notes.Note.Sharp.G);
        public static Sharp GSharp3 => Sharp3(Notes.Note.Sharp.GSharp);
        public static Sharp A3 => Sharp3(Notes.Note.Sharp.A);
        public static Sharp ASharp3 => Sharp3(Notes.Note.Sharp.ASharp);
        public static Sharp B3 => Sharp3(Notes.Note.Sharp.B);

        public static Sharp C4 => Sharp4(Notes.Note.Sharp.C);
        public static Sharp CSharp4=> Sharp4(Notes.Note.Sharp.CSharp);
        public static Sharp D4 => Sharp4(Notes.Note.Sharp.D);
        public static Sharp DSharp4 => Sharp4(Notes.Note.Sharp.DSharp);
        public static Sharp E4 => Sharp4(Notes.Note.Sharp.E);
        public static Sharp F4 => Sharp4(Notes.Note.Sharp.F);
        public static Sharp FSharp4 => Sharp4(Notes.Note.Sharp.FSharp);
        public static Sharp G4 => Sharp4(Notes.Note.Sharp.G);
        public static Sharp GSharp4 => Sharp4(Notes.Note.Sharp.GSharp);
        public static Sharp A4 => Sharp4(Notes.Note.Sharp.A);
        public static Sharp ASharp4 => Sharp4(Notes.Note.Sharp.ASharp);
        public static Sharp B4 => Sharp4(Notes.Note.Sharp.B);

        private static Sharp Sharp2(Note.Sharp note) => new(note, 2);
        private static Sharp Sharp3(Note.Sharp note) => new(note, 3);
        private static Sharp Sharp4(Note.Sharp note) => new(note, 4);

        public override string ToString() => $"{Note.NaturalNote}{Note.Accidental}{Octave.Value}";

        protected override PitchClass GetPitchClass() => Note.PitchClass;
    }

    /// <inheritdoc cref="Pitch"/>
    /// <summary>
    /// A flat pitch.
    /// </summary>
    [PublicAPI]
    public sealed partial record Flat(Note.Flat Note, Octave Octave) : Pitch(Octave)
    {
        public static Flat C(Octave octave) => new(Notes.Note.Flat.C, octave);
        public static Flat DFlat(Octave octave) => new(Notes.Note.Flat.DFlat, octave);
        public static Flat D(Octave octave) => new(Notes.Note.Flat.D, octave);
        public static Flat EFlat(Octave octave) => new(Notes.Note.Flat.EFlat, octave);
        public static Flat E(Octave octave) => new(Notes.Note.Flat.E, octave);
        public static Flat F(Octave octave) => new(Notes.Note.Flat.F, octave);
        public static Flat GFlat(Octave octave) => new(Notes.Note.Flat.GFlat, octave);
        public static Flat G(Octave octave) => new(Notes.Note.Flat.G, octave);
        public static Flat AFlat(Octave octave) => new(Notes.Note.Flat.AFlat, octave);
        public static Flat A(Octave octave) => new(Notes.Note.Flat.A, octave);
        public static Flat ASharp(Octave octave) => new(Notes.Note.Flat.BFlat, octave);
        public static Flat B(Octave octave) => new(Notes.Note.Flat.B, octave);

        public override string ToString() => $"{Note.NaturalNote}{Note.Accidental}{Octave.Value}";

        protected override PitchClass GetPitchClass() => Note.PitchClass;
    }
}