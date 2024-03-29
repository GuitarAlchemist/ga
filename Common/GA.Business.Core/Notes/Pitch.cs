﻿namespace GA.Business.Core.Notes;

using PCRE;
using Atonal;
using Primitives;
using Intervals;

[PublicAPI]
public abstract record Pitch(Octave Octave) : IComparable<Pitch>
{
    public int CompareTo(Pitch? other) => other is null ? 1 : MidiNote.CompareTo(other.MidiNote);
    public static bool operator <(Pitch? left, Pitch? right) => Comparer<Pitch>.Default.Compare(left, right) < 0;
    public static bool operator >(Pitch? left, Pitch? right) => Comparer<Pitch>.Default.Compare(left, right) > 0;
    public static bool operator <=(Pitch? left, Pitch? right) => Comparer<Pitch>.Default.Compare(left, right) <= 0;
    public static bool operator >=(Pitch? left, Pitch? right) => Comparer<Pitch>.Default.Compare(left, right) >= 0;

    public abstract PitchClass PitchClass { get; }
    public MidiNote MidiNote => GetMidiNote();

    public static implicit operator MidiNote(Pitch pitch) => pitch.GetMidiNote();

    private MidiNote GetMidiNote() => MidiNote.Create(Octave, PitchClass);

    [PublicAPI]
    public sealed record Chromatic(Note.Chromatic Note, Octave Octave) : Pitch(Octave)
    {
        public static Chromatic FromPitch(Pitch pitch) => pitch.PitchClass.ToChromaticPitch(pitch.Octave);

        public override PitchClass PitchClass => Note.PitchClass;

        public override string ToString()
        {
            var sharp = Note.ToSharp();
            return sharp.SharpAccidental.HasValue
                ? $"{sharp}/{Note.ToFlat()}"
                : $"{sharp}";
        }
    }

    /// <inheritdoc cref="Pitch"/>
    /// <summary>
    /// A sharp pitch.
    /// </summary>
    [PublicAPI]
    public sealed record Sharp(Note.Sharp Note, Octave Octave) : Pitch(Octave)
    {
        //language=regexp
        public static readonly string RegexPattern = "([Am-Gm])(#?)(10|11|[0-9])";
        private static readonly PcreRegex _regex = new(RegexPattern, PcreOptions.Compiled | PcreOptions.IgnoreCase);

        public static bool TryParse(string s, out Sharp parsedPitch)
        {
            parsedPitch = C0;
            var match = _regex.Match(s);
            if (!match.Success) return false; // Failed

            var noteGroup = match.Groups[1];
            var accidentalGroup = match.Groups[2];
            var octaveGroup = match.Groups[3];
            if (!noteGroup.IsDefined) return false; // Missing note
            if (!octaveGroup.IsDefined) return false; // Missing octave

            // NaturalMinor note
            if (!NaturalNote.TryParse(noteGroup.Value, null, out var naturalNote)) return false;

            // Sharp accidental
            SharpAccidental? accidental = null;
            if (accidentalGroup.IsDefined && SharpAccidental.TryParse(accidentalGroup.Value, out var parsedAccidental)) accidental = parsedAccidental;

            // Octave
            if (!int.TryParse(octaveGroup, out var parsedOctaveValue)) return false;
            var octave = new Octave() {Value = parsedOctaveValue};

            var note = new Note.Sharp(naturalNote, accidental);
            parsedPitch = new(note, octave);
            return true;
        }

        public static Sharp Default => C0;

        public static Sharp C(Octave octave) => new(Notes.Note.Sharp.C, octave);
        public static Sharp CSharp(Octave octave) => new(Notes.Note.Sharp.CSharp, octave);
        public static Sharp D(Octave octave) => new(Notes.Note.Sharp.D, octave);
        public static Sharp DSharp(Octave octave) => new(Notes.Note.Sharp.D, octave);
        public static Sharp E(Octave octave) => new(Notes.Note.Sharp.E, octave);
        public static Sharp F(Octave octave) => new(Notes.Note.Sharp.F, octave);
        public static Sharp FSharp(Octave octave) => new(Notes.Note.Sharp.G, octave);
        public static Sharp G(Octave octave) => new(Notes.Note.Sharp.G, octave);
        public static Sharp GSharp(Octave octave) => new(Notes.Note.Sharp.A, octave);
        public static Sharp A(Octave octave) => new(Notes.Note.Sharp.A, octave);
        public static Sharp ASharp(Octave octave) => new(Notes.Note.Sharp.ASharp, octave);
        public static Sharp B(Octave octave) => new(Notes.Note.Sharp.B, octave);

        public static Sharp C0 => Sharp0(Notes.Note.Sharp.C);
        public static Sharp CSharp0 => Sharp0(Notes.Note.Sharp.CSharp);
        public static Sharp D0 => Sharp0(Notes.Note.Sharp.D);
        public static Sharp DSharp0 => Sharp0(Notes.Note.Sharp.DSharp);
        public static Sharp E0 => Sharp0(Notes.Note.Sharp.E);
        public static Sharp F0 => Sharp0(Notes.Note.Sharp.F);
        public static Sharp FSharp0 => Sharp0(Notes.Note.Sharp.FSharp);
        public static Sharp G0 => Sharp0(Notes.Note.Sharp.G);
        public static Sharp GSharp0 => Sharp0(Notes.Note.Sharp.GSharp);
        public static Sharp A0 => Sharp0(Notes.Note.Sharp.A);
        public static Sharp ASharp0 => Sharp0(Notes.Note.Sharp.ASharp);
        public static Sharp B0 => Sharp0(Notes.Note.Sharp.B);

        public static Sharp C1 => Sharp1(Notes.Note.Sharp.C);
        public static Sharp CSharp1 => Sharp1(Notes.Note.Sharp.CSharp);
        public static Sharp D1 => Sharp1(Notes.Note.Sharp.D);
        public static Sharp DSharp1 => Sharp1(Notes.Note.Sharp.DSharp);
        public static Sharp E1 => Sharp1(Notes.Note.Sharp.E);
        public static Sharp F1 => Sharp1(Notes.Note.Sharp.F);
        public static Sharp FSharp1 => Sharp1(Notes.Note.Sharp.FSharp);
        public static Sharp G1 => Sharp1(Notes.Note.Sharp.G);
        public static Sharp GSharp1 => Sharp1(Notes.Note.Sharp.GSharp);
        public static Sharp A1 => Sharp1(Notes.Note.Sharp.A);
        public static Sharp ASharp1 => Sharp1(Notes.Note.Sharp.ASharp);
        public static Sharp B1 => Sharp1(Notes.Note.Sharp.B);

        public static Sharp C2 => Sharp2(Notes.Note.Sharp.C);
        public static Sharp CSharp2 => Sharp2(Notes.Note.Sharp.CSharp);
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
        public static Sharp CSharp3 => Sharp3(Notes.Note.Sharp.CSharp);
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
        public static Sharp CSharp4 => Sharp4(Notes.Note.Sharp.CSharp);
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

        public static Sharp C5 => Sharp5(Notes.Note.Sharp.C);
        public static Sharp CSharp5 => Sharp5(Notes.Note.Sharp.CSharp);
        public static Sharp D5 => Sharp5(Notes.Note.Sharp.D);
        public static Sharp DSharp5 => Sharp5(Notes.Note.Sharp.DSharp);
        public static Sharp E5 => Sharp5(Notes.Note.Sharp.E);
        public static Sharp F5 => Sharp5(Notes.Note.Sharp.F);
        public static Sharp FSharp5 => Sharp5(Notes.Note.Sharp.FSharp);
        public static Sharp G5 => Sharp5(Notes.Note.Sharp.G);
        public static Sharp GSharp5 => Sharp5(Notes.Note.Sharp.GSharp);
        public static Sharp A5 => Sharp5(Notes.Note.Sharp.A);
        public static Sharp ASharp5 => Sharp5(Notes.Note.Sharp.ASharp);
        public static Sharp B5 => Sharp5(Notes.Note.Sharp.B);

        private static Sharp Sharp0(Note.Sharp note) => new(note, 0);
        private static Sharp Sharp1(Note.Sharp note) => new(note, 1);
        private static Sharp Sharp2(Note.Sharp note) => new(note, 2);
        private static Sharp Sharp3(Note.Sharp note) => new(note, 3);
        private static Sharp Sharp4(Note.Sharp note) => new(note, 4);
        private static Sharp Sharp5(Note.Sharp note) => new(note, 5);

        public static Sharp FromPitch(Pitch pitch) => pitch.PitchClass.ToSharpPitch(pitch.Octave);

        public override PitchClass PitchClass => Note.PitchClass;
        public override string ToString() => $"{Note}{Octave.Value}";
    }

    /// <inheritdoc cref="Pitch"/>
    /// <summary>
    /// Am flat pitch.
    /// </summary>
    [PublicAPI]
    public sealed record Flat(Note.Flat Note, Octave Octave) : Pitch(Octave)
    {
        public static Flat Default => C(0);

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

        public static Flat FromPitch(Pitch pitch) => pitch.PitchClass.ToFlatPitch(pitch.Octave);

        public override PitchClass PitchClass => Note.PitchClass;
        public override string ToString() => $"{Note}{Octave.Value}";
    }
}