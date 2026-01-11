namespace GA.Business.Core.Notes;

using System;
using System.Collections.Generic;
using Atonal;
using Atonal.Abstractions;
using Intervals;
using JetBrains.Annotations;
using Primitives;

/// <summary>
///     Pitch discriminated union
/// </summary>
/// <remarks>
///     <see cref="Chromatic" /> | <see cref="Sharp" /> | <see cref="Flat" />
/// </remarks>
/// <param name="Octave"></param>
[PublicAPI]
public abstract record Pitch(Octave Octave) : IComparable<Pitch>,
    IPitchClass
{
    /// <summary>
    ///     Gets the <see cref="MidiNote" />
    /// </summary>
    public MidiNote MidiNote => GetMidiNote();

    #region IPitchClass Members

    /// <inheritdoc cref="IPitchClass.PitchClass" />
    public abstract PitchClass PitchClass { get; }

    #endregion

    #region Operators

    public static implicit operator MidiNote(Pitch pitch)
    {
        return pitch.GetMidiNote();
    }

    #endregion

    private MidiNote GetMidiNote()
    {
        return MidiNote.Create(Octave, PitchClass);
    }

    #region Chromatic Pitch

    [PublicAPI]
    public sealed record Chromatic(Note.Chromatic Note, Octave Octave) : Pitch(Octave)
    {
        /// <inheritdoc />
        public override PitchClass PitchClass => Note.PitchClass;

        public static Chromatic FromPitch(Pitch pitch)
        {
            return pitch.PitchClass.ToChromaticPitch(pitch.Octave);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var sharp = Note.ToSharp();
            return sharp.SharpAccidental.HasValue
                ? $"{sharp}{Octave.Value}/{Note.ToFlat()}"
                : $"{sharp}{Octave.Value}";
        }
    }

    #endregion

    #region Sharp Pitch

    /// <inheritdoc cref="Pitch" />
    /// <summary>
    ///     A sharp pitch.
    /// </summary>
    [PublicAPI]
    public sealed record Sharp(Note.Sharp Note, Octave Octave) : Pitch(Octave), IParsable<Sharp>
    {
        /// <inheritdoc />
        public override PitchClass PitchClass => Note.PitchClass;

        /// <summary>
        ///     Gets a sharp pitch from a pitch (e.g. C#/Db => C#)
        /// </summary>
        /// <param name="pitch">The <see cref="Pitch" /></param>
        /// <returns>The <see cref="Pitch.Sharp" /></returns>
        public static Sharp FromPitch(Pitch pitch)
        {
            return pitch.PitchClass.ToSharpPitch(pitch.Octave);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Note}{Octave.Value}";
        }

        #region IParsable Members

        /// <inheritdoc />
        public static Sharp Parse(string s, IFormatProvider? provider = null)
        {
            if (!TryParse(s, provider, out var result))
            {
                throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
            }

            return result;
        }

        /// <inheritdoc />
        public static bool TryParse(string? s, IFormatProvider? provider, out Sharp result)
        {
            return PitchParser.TryParse<SharpAccidental, Note.Sharp, Sharp>(
                s,
                (naturalNote, accidental) => new(naturalNote, accidental),
                (note, octave) => new(note, octave),
                out result);
        }

        #endregion

        #region Well-known sharp pitches

        public static Sharp Default => C0;

        public static Sharp C(Octave octave)
        {
            return new(Notes.Note.Sharp.C, octave);
        }

        public static Sharp CSharp(Octave octave)
        {
            return new(Notes.Note.Sharp.CSharp, octave);
        }

        public static Sharp D(Octave octave)
        {
            return new(Notes.Note.Sharp.D, octave);
        }

        public static Sharp DSharp(Octave octave)
        {
            return new(Notes.Note.Sharp.DSharp, octave);
        }

        public static Sharp E(Octave octave)
        {
            return new(Notes.Note.Sharp.E, octave);
        }

        public static Sharp F(Octave octave)
        {
            return new(Notes.Note.Sharp.F, octave);
        }

        public static Sharp FSharp(Octave octave)
        {
            return new(Notes.Note.Sharp.FSharp, octave);
        }

        public static Sharp G(Octave octave)
        {
            return new(Notes.Note.Sharp.G, octave);
        }

        public static Sharp GSharp(Octave octave)
        {
            return new(Notes.Note.Sharp.GSharp, octave);
        }

        public static Sharp A(Octave octave)
        {
            return new(Notes.Note.Sharp.A, octave);
        }

        public static Sharp ASharp(Octave octave)
        {
            return new(Notes.Note.Sharp.ASharp, octave);
        }

        public static Sharp B(Octave octave)
        {
            return new(Notes.Note.Sharp.B, octave);
        }

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

        private static Sharp Sharp0(Note.Sharp note)
        {
            return new(note, 0);
        }

        private static Sharp Sharp1(Note.Sharp note)
        {
            return new(note, 1);
        }

        private static Sharp Sharp2(Note.Sharp note)
        {
            return new(note, 2);
        }

        private static Sharp Sharp3(Note.Sharp note)
        {
            return new(note, 3);
        }

        private static Sharp Sharp4(Note.Sharp note)
        {
            return new(note, 4);
        }

        private static Sharp Sharp5(Note.Sharp note)
        {
            return new(note, 5);
        }

        #endregion
    }

    #endregion

    #region Flat Pitch

    /// <inheritdoc cref="Pitch" />
    /// <summary>
    ///     Am flat pitch.
    /// </summary>
    [PublicAPI]
    public sealed record Flat(Note.Flat Note, Octave Octave) : Pitch(Octave), IParsable<Flat>
    {
        /// <inheritdoc />
        public override PitchClass PitchClass => Note.PitchClass;

        /// <summary>
        ///     Gets a flat pitch from a pitch (e.g. C#/Db => Db)
        /// </summary>
        /// <param name="pitch">The <see cref="Pitch" /></param>
        /// <returns>The <see cref="Pitch.Sharp" /></returns>
        public static Flat FromPitch(Pitch pitch)
        {
            return pitch.PitchClass.ToFlatPitch(pitch.Octave);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Note}{Octave.Value}";
        }

        #region IParsable Members

        /// <inheritdoc />
        public static Flat Parse(string s, IFormatProvider? provider = null)
        {
            if (!TryParse(s, provider, out var result))
            {
                throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
            }

            return result;
        }

        /// <inheritdoc />
        public static bool TryParse(string? s, IFormatProvider? provider, out Flat result)
        {
            return PitchParser.TryParse<FlatAccidental, Note.Flat, Flat>(
                s,
                (naturalNote, accidental) => new(naturalNote, accidental),
                (note, octave) => new(note, octave),
                out result);
        }

        #endregion

        #region Well-known Flat pitches

        public static Flat Default => C0;

        public static Flat C(Octave octave)
        {
            return new(Notes.Note.Flat.C, octave);
        }

        public static Flat CFlat(Octave octave)
        {
            return new(Notes.Note.Flat.CFlat, octave);
        }

        public static Flat D(Octave octave)
        {
            return new(Notes.Note.Flat.D, octave);
        }

        public static Flat DFlat(Octave octave)
        {
            return new(Notes.Note.Flat.D, octave);
        }

        public static Flat E(Octave octave)
        {
            return new(Notes.Note.Flat.E, octave);
        }

        public static Flat F(Octave octave)
        {
            return new(Notes.Note.Flat.F, octave);
        }

        public static Flat FFlat(Octave octave)
        {
            return new(Notes.Note.Flat.G, octave);
        }

        public static Flat G(Octave octave)
        {
            return new(Notes.Note.Flat.G, octave);
        }

        public static Flat GFlat(Octave octave)
        {
            return new(Notes.Note.Flat.A, octave);
        }

        public static Flat A(Octave octave)
        {
            return new(Notes.Note.Flat.A, octave);
        }

        public static Flat AFlat(Octave octave)
        {
            return new(Notes.Note.Flat.AFlat, octave);
        }

        public static Flat B(Octave octave)
        {
            return new(Notes.Note.Flat.B, octave);
        }

        public static Flat C0 => Flat0(Notes.Note.Flat.C);
        public static Flat CFlat0 => Flat0(Notes.Note.Flat.CFlat);
        public static Flat D0 => Flat0(Notes.Note.Flat.D);
        public static Flat DFlat0 => Flat0(Notes.Note.Flat.DFlat);
        public static Flat E0 => Flat0(Notes.Note.Flat.E);
        public static Flat F0 => Flat0(Notes.Note.Flat.F);
        public static Flat FFlat0 => Flat0(Notes.Note.Flat.FFlat);
        public static Flat G0 => Flat0(Notes.Note.Flat.G);
        public static Flat GFlat0 => Flat0(Notes.Note.Flat.GFlat);
        public static Flat A0 => Flat0(Notes.Note.Flat.A);
        public static Flat AFlat0 => Flat0(Notes.Note.Flat.AFlat);
        public static Flat B0 => Flat0(Notes.Note.Flat.B);

        public static Flat C1 => Flat1(Notes.Note.Flat.C);
        public static Flat CFlat1 => Flat1(Notes.Note.Flat.CFlat);
        public static Flat D1 => Flat1(Notes.Note.Flat.D);
        public static Flat DFlat1 => Flat1(Notes.Note.Flat.DFlat);
        public static Flat E1 => Flat1(Notes.Note.Flat.E);
        public static Flat F1 => Flat1(Notes.Note.Flat.F);
        public static Flat FFlat1 => Flat1(Notes.Note.Flat.FFlat);
        public static Flat G1 => Flat1(Notes.Note.Flat.G);
        public static Flat GFlat1 => Flat1(Notes.Note.Flat.GFlat);
        public static Flat A1 => Flat1(Notes.Note.Flat.A);
        public static Flat AFlat1 => Flat1(Notes.Note.Flat.AFlat);
        public static Flat B1 => Flat1(Notes.Note.Flat.B);

        public static Flat C2 => Flat2(Notes.Note.Flat.C);
        public static Flat CFlat2 => Flat2(Notes.Note.Flat.CFlat);
        public static Flat D2 => Flat2(Notes.Note.Flat.D);
        public static Flat DFlat2 => Flat2(Notes.Note.Flat.DFlat);
        public static Flat E2 => Flat2(Notes.Note.Flat.E);
        public static Flat F2 => Flat2(Notes.Note.Flat.F);
        public static Flat FFlat2 => Flat2(Notes.Note.Flat.FFlat);
        public static Flat G2 => Flat2(Notes.Note.Flat.G);
        public static Flat GFlat2 => Flat2(Notes.Note.Flat.GFlat);
        public static Flat A2 => Flat2(Notes.Note.Flat.A);
        public static Flat AFlat2 => Flat2(Notes.Note.Flat.AFlat);
        public static Flat B2 => Flat2(Notes.Note.Flat.B);

        public static Flat C3 => Flat3(Notes.Note.Flat.C);
        public static Flat CFlat3 => Flat3(Notes.Note.Flat.CFlat);
        public static Flat D3 => Flat3(Notes.Note.Flat.D);
        public static Flat DFlat3 => Flat3(Notes.Note.Flat.DFlat);
        public static Flat E3 => Flat3(Notes.Note.Flat.E);
        public static Flat F3 => Flat3(Notes.Note.Flat.F);
        public static Flat FFlat3 => Flat3(Notes.Note.Flat.FFlat);
        public static Flat G3 => Flat3(Notes.Note.Flat.G);
        public static Flat GFlat3 => Flat3(Notes.Note.Flat.GFlat);
        public static Flat A3 => Flat3(Notes.Note.Flat.A);
        public static Flat AFlat3 => Flat3(Notes.Note.Flat.AFlat);
        public static Flat B3 => Flat3(Notes.Note.Flat.B);

        public static Flat C4 => Flat4(Notes.Note.Flat.C);
        public static Flat CFlat4 => Flat4(Notes.Note.Flat.CFlat);
        public static Flat D4 => Flat4(Notes.Note.Flat.D);
        public static Flat DFlat4 => Flat4(Notes.Note.Flat.DFlat);
        public static Flat E4 => Flat4(Notes.Note.Flat.E);
        public static Flat F4 => Flat4(Notes.Note.Flat.F);
        public static Flat FFlat4 => Flat4(Notes.Note.Flat.FFlat);
        public static Flat G4 => Flat4(Notes.Note.Flat.G);
        public static Flat GFlat4 => Flat4(Notes.Note.Flat.GFlat);
        public static Flat A4 => Flat4(Notes.Note.Flat.A);
        public static Flat AFlat4 => Flat4(Notes.Note.Flat.AFlat);
        public static Flat B4 => Flat4(Notes.Note.Flat.B);

        public static Flat C5 => Flat5(Notes.Note.Flat.C);
        public static Flat CFlat5 => Flat5(Notes.Note.Flat.CFlat);
        public static Flat D5 => Flat5(Notes.Note.Flat.D);
        public static Flat DFlat5 => Flat5(Notes.Note.Flat.DFlat);
        public static Flat E5 => Flat5(Notes.Note.Flat.E);
        public static Flat F5 => Flat5(Notes.Note.Flat.F);
        public static Flat FFlat5 => Flat5(Notes.Note.Flat.FFlat);
        public static Flat G5 => Flat5(Notes.Note.Flat.G);
        public static Flat GFlat5 => Flat5(Notes.Note.Flat.GFlat);
        public static Flat A5 => Flat5(Notes.Note.Flat.A);
        public static Flat AFlat5 => Flat5(Notes.Note.Flat.AFlat);
        public static Flat B5 => Flat5(Notes.Note.Flat.B);

        private static Flat Flat0(Note.Flat note)
        {
            return new(note, 0);
        }

        private static Flat Flat1(Note.Flat note)
        {
            return new(note, 1);
        }

        private static Flat Flat2(Note.Flat note)
        {
            return new(note, 2);
        }

        private static Flat Flat3(Note.Flat note)
        {
            return new(note, 3);
        }

        private static Flat Flat4(Note.Flat note)
        {
            return new(note, 4);
        }

        private static Flat Flat5(Note.Flat note)
        {
            return new(note, 5);
        }

        #endregion
    }

    #endregion

    #region IComparable<Pitch> Members

    public int CompareTo(Pitch? other)
    {
        return other is null ? 1 : MidiNote.CompareTo(other.MidiNote);
    }

    public static bool operator <(Pitch? left, Pitch? right)
    {
        return Comparer<Pitch>.Default.Compare(left, right) < 0;
    }

    public static bool operator >(Pitch? left, Pitch? right)
    {
        return Comparer<Pitch>.Default.Compare(left, right) > 0;
    }

    public static bool operator <=(Pitch? left, Pitch? right)
    {
        return Comparer<Pitch>.Default.Compare(left, right) <= 0;
    }

    public static bool operator >=(Pitch? left, Pitch? right)
    {
        return Comparer<Pitch>.Default.Compare(left, right) >= 0;
    }

    #endregion
}
