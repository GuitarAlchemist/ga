namespace GA.Business.Core.Tonal;

using System.Collections.Immutable;


using Intervals;
using GA.Business.Core.Notes.Primitives;
using GA.Core;
using Notes;

[PublicAPI]
[DiscriminatedUnion]
public abstract partial record Key(KeySignature KeySignature)
{
    public abstract KeyMode KeyMode { get; }
    public AccidentalKind AccidentalKind => KeySignature.AccidentalKind;
    public bool IsNoteAccidental(NaturalNote note) => KeySignature.Contains(note);

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
        public Note.KeyNote Root => GetRoot(KeySignature);

        /// <summary>
        /// Gets all notes in the key (e.g. C D E F G A B)
        /// </summary>
        /// <returns></returns>
        public IReadOnlyCollection<Note.KeyNote> GetNotes()
        {
            var items = KeySignature.Value < 0
                ? GetFlatNotes().ToImmutableList()
                : GetSharpNotes().ToImmutableList();
            var result = new ReadOnlyItems<Note.KeyNote>(items);

            return result;

            IEnumerable<Note.KeyNote> GetSharpNotes()
            {
                var root = GetRoot(KeySignature);
                yield return root;

                var naturalNote = root.NaturalNote;
                var sharpNotes = KeySignature.AccidentedNotes.ToImmutableHashSet();
                bool HasSharp(NaturalNote note) => sharpNotes.Contains(note);
                for (var i = 0; i < 6; i++)
                {
                    naturalNote = naturalNote.ToDegree(1);

                    yield return
                        HasSharp(naturalNote)
                            ? new(naturalNote, SharpAccidental.Sharp)
                            : new Note.SharpKey(naturalNote);
                }
            }

            IEnumerable<Note.KeyNote> GetFlatNotes()
            {
                var root = GetRoot(KeySignature);
                yield return root;

                var naturalNote = root.NaturalNote;
                var flatNotes = KeySignature.AccidentedNotes.ToImmutableHashSet();
                bool HasFlat(NaturalNote note) => flatNotes.Contains(note);
                for (var i = 0; i < 6; i++)
                {
                    naturalNote = naturalNote.ToDegree(1);

                    yield return
                        HasFlat(naturalNote)
                            ? new(naturalNote, FlatAccidental.Flat)
                            : new Note.FlatKey(naturalNote);
                }
            }
        }

        public override string ToString()
        {
            return KeySignature.Value switch
            {
                -7 => "Key of Cb",
                -6 => "Key of Gb",
                -5 => "Key of Db",
                -4 => "Key of Ab",
                -3 => "Key of Eb",
                -2 => "Key of Bb",
                -1 => "Key of F",
                0 => "Key of C",
                1 => "Key of G",
                2 => "Key of D",
                3 => "Key of A",
                4 => "Key of E",
                5 => "Key of B",
                6 => "Key of F#",
                7 => "Key of C#",
                _ => string.Empty
            };
        }

        private static Note.KeyNote GetRoot(KeySignature keySignature) => keySignature.Value switch
        {
            -7 => Note.FlatKey.CFlat,
            -6 => Note.FlatKey.GFlat,
            -5 => Note.FlatKey.DFlat,
            -4 => Note.FlatKey.AFlat,
            -3 => Note.FlatKey.EFlat,
            -2 => Note.FlatKey.BFlat,
            -1 => Note.FlatKey.F,
            0 => Note.SharpKey.C,
            1 => Note.SharpKey.G,
            2 => Note.SharpKey.D,
            3 => Note.SharpKey.A,
            4 => Note.SharpKey.E,
            5 => Note.SharpKey.B,
            6 => Note.SharpKey.FSharp,
            7 => Note.SharpKey.CSharp,
            _ => throw new InvalidOperationException()
        };

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
        public Note.KeyNote Root => GetRoot(KeySignature);

        public override string ToString()
        {
            return KeySignature.Value switch
            {
                -7 => "Key of Abm",
                -6 => "Key of Ebm",
                -5 => "Key of Bbm",
                -4 => "Key of Fm",
                -3 => "Key of Cb",
                -2 => "Key of Gm",
                -1 => "Key of Dm",
                0 => "Key of Am",
                1 => "Key of Em",
                2 => "Key of Bm",
                3 => "Key of F#m",
                4 => "Key of C#m",
                5 => "Key of G#m",
                6 => "Key of D#m",
                7 => "Key of A#m",
                _ => string.Empty
            };
        }

        private static Note.KeyNote GetRoot(KeySignature keySignature) => keySignature.Value switch
        {
            -7 => Note.FlatKey.AFlat,
            -6 => Note.FlatKey.EFlat,
            -5 => Note.FlatKey.BFlat,
            -4 => Note.FlatKey.FFlat,
            -3 => Note.FlatKey.C,
            -2 => Note.FlatKey.G,
            -1 => Note.FlatKey.D,
            0 => Note.SharpKey.A,
            1 => Note.SharpKey.E,
            2 => Note.SharpKey.B,
            3 => Note.SharpKey.FSharp,
            4 => Note.SharpKey.CSharp,
            5 => Note.SharpKey.GSharp,
            6 => Note.SharpKey.DSharp,
            7 => Note.SharpKey.ASharp,
            _ => throw new InvalidOperationException()
        };
    }
}