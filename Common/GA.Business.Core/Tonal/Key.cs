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
    public static Key FromRoot(NaturalNote naturalNote) => KeyByRoot.Get(naturalNote);
    public static IReadOnlyCollection<Key> GetAll(KeyMode keyMode) =>
        keyMode switch
        {
            KeyMode.Major => Major.GetAll(),
            KeyMode.Minor => Minor.GetAll(),
            _ => throw new ArgumentOutOfRangeException(nameof(keyMode), keyMode, null)
        };

    public static IReadOnlyCollection<Key> GetAll()
    {
        var list = new List<Key>();
        list.AddRange(GetAll(KeyMode.Major));
        list.AddRange(GetAll(KeyMode.Minor));

        return list.AsReadOnly();
    }

    public abstract KeyMode KeyMode { get; }
    public AccidentalKind AccidentalKind => KeySignature.AccidentalKind;
    public abstract Note.KeyNote Root { get; }

    /// <summary>
    /// Gets the 7 notes in the key.
    /// </summary>
    /// <returns>The <see cref="Note.KeyNote"/> collection.</returns>
    public IReadOnlyCollection<Note.KeyNote> GetNotes()
    {
        var accidentedNotes = 
            KeySignature.SignatureNotes
                .Select(note => note.NaturalNote)
                .ToImmutableHashSet();
        var items = KeySignature.Value < 0
            ? GetFlatNotes(Root, accidentedNotes).ToImmutableList()
            : GetSharpNotes(Root, accidentedNotes).ToImmutableList();
        var result = new ReadOnlyItems<Note.KeyNote>(items);

        return result;

        static IEnumerable<Note.KeyNote> GetSharpNotes(
            Note.KeyNote root,
            IReadOnlySet<NaturalNote> accidentedNotes)
        {
            yield return root;

            var naturalNote = root.NaturalNote;
            for (var i = 0; i < 6; i++)
            {
                naturalNote++;
                var item = accidentedNotes.Contains(naturalNote)
                    ? new(naturalNote, SharpAccidental.Sharp)
                    : new Note.SharpKey(naturalNote);
                yield return item;
            }
        }

        static IEnumerable<Note.KeyNote> GetFlatNotes(
            Note.KeyNote root,
            IReadOnlySet<NaturalNote> accidentedNotes)
        {
            yield return root;

            var naturalNote = root.NaturalNote;
            for (var i = 0; i < 6; i++)
            {
                naturalNote++;
                var item = accidentedNotes.Contains(naturalNote)
                    ? new(naturalNote, FlatAccidental.Flat)
                    : new Note.FlatKey(naturalNote);
                yield return item;
            }
        }
    }

    public Interval.Simple GetInterval(Note.AccidentedNote note) => note.GetInterval(Root);

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
        public new static IReadOnlyCollection<Major> GetAll() => Enumerable.Range(-7, 15).Select(i => new Major(i)).ToImmutableList();

        public override KeyMode KeyMode => KeyMode.Major;
        public override Note.KeyNote Root => GetRoot(KeySignature);

        public override string ToString() => GetKeyName(KeySignature);

        public static string GetKeyName(KeySignature keySignature) => keySignature.Value switch
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
        public new static IReadOnlyCollection<Minor> GetAll() => Enumerable.Range(-7, 15).Select(i => new Minor(i)).ToImmutableList();

        public override KeyMode KeyMode => KeyMode.Minor;
        public override Note.KeyNote Root => GetRoot(KeySignature);

        public override string ToString() => KeySignature.Value switch
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