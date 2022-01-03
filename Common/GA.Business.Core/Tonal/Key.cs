using System.Collections.Immutable;
using GA.Business.Core.Notes;

namespace GA.Business.Core.Tonal;

[PublicAPI]
[DiscriminatedUnion]
public abstract partial record Key(KeySignature KeySignature)
{
    public abstract KeyMode KeyMode { get; }

    [PublicAPI]
    public sealed partial record Major(KeySignature KeySignature) 
        : Key(KeySignature)
    {
        public static Major Cb => new(-7);
        public static Major Gb => new(-6);
        public static Major Db => new(-5);
        public static Major Ab => new(-4);
        public static Major Eb => new(-3);
        public static Major Bb => new(-2);
        public static Major F => new(-1);
        public static Major C => new(0);
        public static Major G => new(1);
        public static Major D => new(2);
        public static Major A => new(3);
        public static Major E => new(4);
        public static Major B => new(5);
        public static Major FSharp => new(6);
        public static Major CSharp => new(7);

        public override KeyMode KeyMode => KeyMode.Major;

        public Note GetRoot() => KeySignature.Value switch
        {
            -7 => Note.Flat.CFlat,
            -6 => Note.Flat.GFlat,
            -5 => Note.Flat.DFlat,
            -4 => Note.Flat.AFlat,
            -3 => Note.Flat.EFlat,
            -2 => Note.Flat.BFlat,
            -1 => Note.Flat.FFlat,
            0 => Note.Sharp.C,
            1 => Note.Sharp.G,
            2 => Note.Sharp.D,
            3 => Note.Sharp.A,
            4 => Note.Sharp.E,
            5 => Note.Sharp.B,
            6 => Note.Sharp.FSharp,
            7 => Note.Sharp.CSharp,
            _ => throw new InvalidOperationException()
        };

        public IImmutableList<Note> GetNotes()
        {
            return KeySignature.Value < 0 
                ? GetFlatNodes().ToImmutableList() 
                : GetSharpNotes().ToImmutableList();

            IEnumerable<Note> GetSharpNotes()
            {
                var root = GetRoot();
                yield return root;

                var sharpNotes = KeySignature.SharpNotes.ToImmutableHashSet();
                var naturalNote = root.NaturalNote;
                bool HasSharp(NaturalNote note) => sharpNotes.Contains(note);
                for (var i = 0; i < 6; i++)
                {
                    naturalNote = naturalNote.ToDegree(1);

                    yield return
                        HasSharp(naturalNote)
                            ? new(naturalNote, SharpAccidental.Sharp)
                            : new Note.Sharp(naturalNote);
                }
            }

            IEnumerable<Note> GetFlatNodes()
            {
                var root = GetRoot();
                yield return root;

                var flatNotes = KeySignature.FlatNotes.ToImmutableHashSet();
                var naturalNote = root.NaturalNote;
                bool HasFlat(NaturalNote note) => flatNotes.Contains(note);
                for (var i = 0; i < 6; i++)
                {
                    naturalNote = naturalNote.ToDegree(1);

                    yield return
                        HasFlat(naturalNote)
                            ? new(naturalNote, FlatAccidental.Flat)
                            : new Note.Flat(naturalNote);
                }
            }
        }
    }

    [PublicAPI]
    public sealed partial record Minor(KeySignature KeySignature) 
        : Key(KeySignature)
    {
        public static Minor Ab => new(-7);
        public static Minor Eb => new(-6);
        public static Minor Bb => new(-5);
        public static Minor F => new(-4);
        public static Minor C => new(-3);
        public static Minor G => new(-2);
        public static Minor D => new(-1);
        public static Minor A => new(0);
        public static Minor E => new(1);
        public static Minor B => new(2);
        public static Minor FSharp => new(3);
        public static Minor CSharp => new(4);
        public static Minor GSharp => new(5);
        public static Minor DSharp => new(6);
        public static Minor ASharp => new(7);

        public override KeyMode KeyMode => KeyMode.Minor;

        public Note GetRoot() => KeySignature.Value switch
        {
            -7 => Note.Flat.AFlat,
            -6 => Note.Flat.EFlat,
            -5 => Note.Flat.BFlat,
            -4 => Note.Flat.FFlat,
            -3 => Note.Flat.C,
            -2 => Note.Flat.G,
            -1 => Note.Flat.D,
            0 => Note.Sharp.A,
            1 => Note.Sharp.E,
            2 => Note.Sharp.B,
            3 => Note.Sharp.FSharp,
            4 => Note.Sharp.CSharp,
            5 => Note.Sharp.GSharp,
            6 => Note.Sharp.DSharp,
            7 => Note.Sharp.ASharp,
            _ => throw new InvalidOperationException()
        };
    }
}