namespace GA.Business.Core.Notes;

using System.Collections.Immutable;
using Intervals;
using Primitives;
using GA.Business.Core.Intervals.Primitives;
using static Primitives.SharpAccidental;
using static Primitives.FlatAccidental;


[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Note
{
    /// <summary>
    /// Gets the <see cref="PitchClass"/>.
    /// </summary>
    public abstract PitchClass PitchClass { get; }

    /// <inheritdoc cref="Note"/>
    /// <summary>
    /// A chromatic note.
    /// </summary>
    [PublicAPI]
    public sealed partial record Chromatic(PitchClass PitchClass) : Note
    {
        public static Chromatic C => new(0);
        public static Chromatic CSharpDb => new(1);
        public static Chromatic D => new(2);
        public static Chromatic DSharpEb => new(3);
        public static Chromatic E => new(4);
        public static Chromatic F => new(5);
        public static Chromatic FSharpGb => new(6);
        public static Chromatic G => new(7);
        public static Chromatic GSharpAb => new(8);
        public static Chromatic A => new(9);
        public static Chromatic ASharpBb => new(10);
        public static Chromatic B => new(11);

        public override PitchClass PitchClass { get; } = PitchClass;
        public SharpKey ToSharp() => PitchClass.ToSharpNote();
        public FlatKeyNote ToFlat() => PitchClass.ToFlatNote();

        public static implicit operator Chromatic(NaturalNote naturalNote) => new(naturalNote.ToPitchClass());
        public static Interval.Chromatic operator -(Chromatic note1, Chromatic note2) => note1.PitchClass - note2.PitchClass;

        public override string ToString()
        {
            var sharp = ToSharp();
            var flat = ToFlat();
            return sharp.SharpAccidental.HasValue ? $"{sharp}/{flat}" : $"{sharp}";
        }
    }

    /// <inheritdoc cref="Note"/>
    /// <summary>
    /// A note from a musical key (Sharp or flat key).
    /// </summary>
    /// <param name="NaturalNote"></param>
    [PublicAPI]
    public abstract record KeyNote(NaturalNote NaturalNote) : Note
    {
        public abstract AccidentalKind AccidentalKind { get; }
        public abstract Accidental? Accidental { get; }
    }

    /// <inheritdoc cref="Note"/>
    /// <summary>
    /// A note from a sharp musical key.
    /// </summary>
    [PublicAPI]
    public sealed partial record SharpKey(NaturalNote NaturalNote, SharpAccidental? SharpAccidental = null) : KeyNote(NaturalNote)
    {
        public static SharpKey C => new(NaturalNote.C);
        public static SharpKey CSharp => new(NaturalNote.C, Sharp);
        public static SharpKey D => new(NaturalNote.D);
        public static SharpKey DSharp => new(NaturalNote.D, Sharp);
        public static SharpKey E => new(NaturalNote.E);
        public static SharpKey F => new(NaturalNote.F);
        public static SharpKey FSharp => new(NaturalNote.F, Sharp);
        public static SharpKey G => new(NaturalNote.G);
        public static SharpKey GSharp => new(NaturalNote.G, Sharp);
        public static SharpKey A => new(NaturalNote.A);
        public static SharpKey ASharp => new(NaturalNote.A, Sharp);
        public static SharpKey B => new(NaturalNote.B);

        public static IReadOnlyCollection<SharpKey> All => new[] { C, D, E, F, G, A, B }.ToImmutableList();

        public static implicit operator Chromatic(SharpKey sharpKey) => new(sharpKey.PitchClass);

        public override PitchClass PitchClass => new() { Value = NaturalNote.ToPitchClass().Value + (SharpAccidental?.Value ?? 0) };
        public override AccidentalKind AccidentalKind => AccidentalKind.Sharp;
        public override Accidental? Accidental => SharpAccidental;

        public override string ToString() =>
            SharpAccidental.HasValue
                ? $"{NaturalNote}{SharpAccidental.Value}"
                : $"{NaturalNote}";
    }

    /// <inheritdoc cref="Note"/>
    /// <summary>
    /// A note from a flat musical key.
    /// </summary>
    [PublicAPI]
    public sealed partial record FlatKeyNote(NaturalNote NaturalNote, FlatAccidental? FlatAccidental = null) : KeyNote(NaturalNote)
    {
        public static FlatKeyNote CFlat => new(NaturalNote.C, Flat);
        public static FlatKeyNote C => new(NaturalNote.C);
        public static FlatKeyNote DFlat => new(NaturalNote.D, Flat);
        public static FlatKeyNote D => new(NaturalNote.D);
        public static FlatKeyNote EFlat => new(NaturalNote.E, Flat);
        public static FlatKeyNote E => new(NaturalNote.E);
        public static FlatKeyNote FFlat => new(NaturalNote.F, Flat);
        public static FlatKeyNote F => new(NaturalNote.F);
        public static FlatKeyNote GFlat => new(NaturalNote.G, Flat);
        public static FlatKeyNote G => new(NaturalNote.G);
        public static FlatKeyNote AFlat => new(NaturalNote.A, Flat);
        public static FlatKeyNote A => new(NaturalNote.A);
        public static FlatKeyNote BFlat => new(NaturalNote.B, Flat);
        public static FlatKeyNote B => new(NaturalNote.B);

        public static implicit operator Chromatic(FlatKeyNote flatKeyNote) => new(flatKeyNote.PitchClass);

        public override PitchClass PitchClass => new() { Value = NaturalNote.ToPitchClass().Value + FlatAccidental?.Value ?? 0 };
        public override AccidentalKind AccidentalKind => AccidentalKind.Flat;
        public override Accidental? Accidental => FlatAccidental;

        public override string ToString() =>
            FlatAccidental.HasValue
                ? $"{NaturalNote}{FlatAccidental.Value}"
                : $"{NaturalNote}";
    }

    /// <inheritdoc cref="Note"/>
    /// <summary>
    /// A note with an optional accidental
    /// </summary>
    [PublicAPI]
    public sealed partial record AccidentedNote(NaturalNote NaturalNote, Accidental? Accidental = null) : Note
    {
        public static implicit operator Chromatic(AccidentedNote accidentedNote) => new(accidentedNote.PitchClass);

        public override PitchClass PitchClass => new() { Value = NaturalNote.ToPitchClass().Value + (Accidental?.Value ?? 0) };

        public static implicit operator AccidentedNote(KeyNote keyNote) => new(keyNote.NaturalNote, keyNote.Accidental);
        public static Interval.Simple operator -(AccidentedNote note1, AccidentedNote note2) => note1.GetInterval(note2);

        public Interval.Simple GetInterval(AccidentedNote other)
        {
            // See https://www.omnicalculator.com/other/music-interval#musical-intervals-chart

            // Quantity
            var otherNaturalNote = other.NaturalNote;
            var quantity = NaturalNote.GetSimpleInterval(otherNaturalNote);

            // Quality
            var otherAccidental = other.Accidental ?? Intervals.Accidental.Natural;
            var accidental = other.Accidental ?? Intervals.Accidental.Natural;
            Quality quality = (otherAccidental.Value - accidental.Value);

            return new()
            {
                Quantity = quantity,
                Quality = quality
            };
        }
    }
}