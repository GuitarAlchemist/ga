using System.Collections.Immutable;
using GA.Business.Core.Notes;
using GA.Business.Core.Notes.Primitives;

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

        public Note.KeyNote GetRoot() => KeySignature.Value switch
        {
            -7 => Note.FlatKeyNote.CFlat,
            -6 => Note.FlatKeyNote.GFlat,
            -5 => Note.FlatKeyNote.DFlat,
            -4 => Note.FlatKeyNote.AFlat,
            -3 => Note.FlatKeyNote.EFlat,
            -2 => Note.FlatKeyNote.BFlat,
            -1 => Note.FlatKeyNote.FFlat,
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

        /// <summary>
        /// Gets all notes in the key (e.g. C D E F G A B)
        /// </summary>
        /// <returns></returns>
        public IImmutableList<Note.KeyNote> GetNotes()
        {
            return KeySignature.Value < 0 
                ? GetFlatNodes().ToImmutableList() 
                : GetSharpNotes().ToImmutableList();

            IEnumerable<Note.KeyNote> GetSharpNotes()
            {
                var root = GetRoot();
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

            IEnumerable<Note.KeyNote> GetFlatNodes()
            {
                var root = GetRoot();
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
                            : new Note.FlatKeyNote(naturalNote);
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
            -7 => Note.FlatKeyNote.AFlat,
            -6 => Note.FlatKeyNote.EFlat,
            -5 => Note.FlatKeyNote.BFlat,
            -4 => Note.FlatKeyNote.FFlat,
            -3 => Note.FlatKeyNote.C,
            -2 => Note.FlatKeyNote.G,
            -1 => Note.FlatKeyNote.D,
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