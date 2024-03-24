namespace GA.Business.Core.Tonal;

using GA.Business.Core.Notes.Primitives;
using GA.Business.Core.Intervals.Primitives;
using Intervals;
using static Notes.Note;

[PublicAPI]
public abstract record Key(KeySignature KeySignature)
{
    #region Inner Classes

    public class KeyByRootNaturalNote() : LazyIndexerBase<NaturalNote, Key>(GetKeyByRootNaturalNote())
    {
        public static Key Get(NaturalNote naturalNote) => _instance[naturalNote];
        private static readonly KeyByRootNaturalNote _instance = new();
        private static ImmutableDictionary<NaturalNote, Key> GetKeyByRootNaturalNote() => 
            GetItems().Where(key => !key.Root.Accidental.HasValue).ToImmutableDictionary(key => key.Root.NaturalNote);
    }    
    
    #endregion
    
    public static Key FromRootNaturalNote(NaturalNote keyRootNaturalNote) => KeyByRootNaturalNote.Get(keyRootNaturalNote);
    public static IReadOnlyCollection<Key> GetItems(KeyMode keyMode) =>
        keyMode switch
        {
            KeyMode.Major => Major.Items,
            KeyMode.Minor => Minor.Items,
            _ => throw new ArgumentOutOfRangeException(nameof(keyMode), keyMode, null)
        };

    public static IReadOnlyCollection<Key> GetItems() => [.. GetItems(KeyMode.Major), .. GetItems(KeyMode.Minor)];
    public abstract KeyMode KeyMode { get; }
    public AccidentalKind AccidentalKind => KeySignature.AccidentalKind;
    public abstract KeyNote Root { get; }

    /// <summary>
    /// Gets the 7 notes in the key.
    /// </summary>
    /// <returns>The <see cref="KeyNote"/> collection.</returns>
    public IReadOnlyCollection<KeyNote> GetNotes()
    {
        var accidentedNotes = 
            KeySignature.AccidentedNotes
                .Select(note => note.NaturalNote)
                .ToImmutableHashSet();
        var items = KeySignature.Value < 0
            ? GetFlatNotes(Root, accidentedNotes).ToImmutableList()
            : GetSharpNotes(Root, accidentedNotes).ToImmutableList();
        var result = new ReadOnlyItems<KeyNote>(items);

        return result;

        static IEnumerable<KeyNote> GetSharpNotes(
            KeyNote root,
            IReadOnlySet<NaturalNote> accidentedNotes)
        {
            yield return root;

            // Start with the root note
            var naturalNote = root.NaturalNote;
            
            // Iterate through all key notes
            for (var i = 0; i < 6; i++)
            {
                naturalNote++;
                Sharp sharpNote = accidentedNotes.Contains(naturalNote)
                    ? new(naturalNote, SharpAccidental.Sharp)
                    : new (naturalNote);
                yield return sharpNote;
            }
        }

        static IEnumerable<KeyNote> GetFlatNotes(
            KeyNote root,
            IReadOnlySet<NaturalNote> accidentedNotes)
        {
            yield return root;

            var naturalNote = root.NaturalNote;
            for (var i = 0; i < 6; i++)
            {
                naturalNote++;
                var item = accidentedNotes.Contains(naturalNote)
                    ? new(naturalNote, FlatAccidental.Flat)
                    : new Flat(naturalNote);
                yield return item;
            }
        }
    }

    public Interval.Simple GetInterval(AccidentedNote note) => note.GetInterval(Root);

    [PublicAPI]
    public sealed record Major(KeySignature KeySignature) 
        : Key(KeySignature)
    {
        #region Inner Classes

        public class MajorKeyByRoot() : LazyIndexerBase<KeyNote, Major>(GetMajorKeyByRoot())
        {
            public static readonly MajorKeyByRoot Instance = new();
            public static Key Get(KeyNote keyRoot) => keyRoot == null ? throw new ArgumentNullException(nameof(keyRoot)) : (Key)Instance[keyRoot];
            private static ImmutableDictionary<KeyNote, Major> GetMajorKeyByRoot() => Items.ToImmutableDictionary(key => key.Root);
        }

        #endregion
        
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
        public static IReadOnlyCollection<Major> Items => Enumerable.Range(-7, 15).Select(i => new Major(i)).ToImmutableList();
        public static Key FromKeyRoot(KeyNote keyRoot) => MajorKeyByRoot.Get(keyRoot);

        public static bool TryParse(string input, out Major majorKey)
        {
            if (!KeyNote.TryParse(input, out var keyRootNotes)) throw new InvalidOperationException("Failed parsing key root");

            var sharpKeyNote = keyRootNotes.FirstOrDefault(note => note is Sharp);
            var flatKeyNote = keyRootNotes.FirstOrDefault(note => note is Flat);
            var majorKeyIndexer = MajorKeyByRoot.Instance;
            if (sharpKeyNote != null)
            {
                if (majorKeyIndexer.Dictionary.TryGetValue(sharpKeyNote, out var aMajorKey))
                {
                    // Success
                    majorKey = aMajorKey;
                    return true;
                }
            }

            if (flatKeyNote != null)
            {
                if (majorKeyIndexer.Dictionary.TryGetValue(flatKeyNote, out var aMajorKey))
                {
                    // Success
                    majorKey = aMajorKey;
                    return true;
                }
                
            }

            // Failure
            majorKey = default!;
            return false;
        }
        
        public override KeyMode KeyMode => KeyMode.Major;
        public override KeyNote Root => GetRoot(KeySignature);

        public override string ToString() => GetKeyName(KeySignature);

        public static string GetKeyName(KeySignature keySignature) => keySignature.Value switch
        {
            -7 => "Key of Cb",
            -6 => "Key of Gb",
            -5 => "Key of Db",
            -4 => "Key of Abm",
            -3 => "Key of Ebm",
            -2 => "Key of Bbm",
            -1 => "Key of Fm",
            0 => "Key of Cm",
            1 => "Key of Gm",
            2 => "Key of Dm",
            3 => "Key of Am",
            4 => "Key of Em",
            5 => "Key of B",
            6 => "Key of Fm#",
            7 => "Key of Cm#",
            _ => string.Empty
        };

        private static KeyNote GetRoot(KeySignature keySignature) => keySignature.Value switch
        {
            -7 => Flat.CFlat,
            -6 => Flat.GFlat,
            -5 => Flat.DFlat,
            -4 => Flat.AFlat,
            -3 => Flat.EFlat,
            -2 => Flat.BFlat,
            -1 => Flat.F,
            0 => Sharp.C,
            1 => Sharp.G,
            2 => Sharp.D,
            3 => Sharp.A,
            4 => Sharp.E,
            5 => Sharp.B,
            6 => Sharp.FSharp,
            7 => Sharp.CSharp,
            _ => throw new InvalidOperationException()
        };
    }

    [PublicAPI]
    public sealed record Minor(KeySignature KeySignature) 
        : Key(KeySignature)
    {
        #region Inner Classes

        public class MinorKeyByRoot() : LazyIndexerBase<KeyNote, Minor>(GetMinorKeyByRoot())
        {
            public static readonly MinorKeyByRoot Instance = new();
            public static Key Get(KeyNote keyRoot) => keyRoot == null ? throw new ArgumentNullException(nameof(keyRoot)) : (Key)Instance[keyRoot];
            private static ImmutableDictionary<KeyNote, Minor> GetMinorKeyByRoot() => Items.ToImmutableDictionary(key => key.Root);
        }

        #endregion
        
        public static Minor Abm => new(-7);
        public static Minor Ebm => new(-6);
        public static Minor Bbm => new(-5);
        public static Minor Fm => new(-4);
        public static Minor Cm => new(-3);
        public static Minor Gm => new(-2);
        public static Minor Dm => new(-1);
        public static Minor Am => new(0);
        public static Minor Em => new(1);
        public static Minor B => new(2);
        public static Minor FSharpm => new(3);
        public static Minor CSharpm => new(4);
        public static Minor GSharpm => new(5);
        public static Minor DSharpm => new(6);
        public static Minor ASharpm => new(7);
        public static IReadOnlyCollection<Minor> Items => Enumerable.Range(-7, 15).Select(i => new Minor(i)).ToImmutableList();
        
        public static bool TryParse(string input, out Minor minorKey)
        {
            if (!KeyNote.TryParse(input, out var keyRootNotes)) throw new InvalidOperationException("Failed parsing key root");

            var sharpKeyNote = keyRootNotes.FirstOrDefault(note => note is Sharp);
            var flatKeyNote = keyRootNotes.FirstOrDefault(note => note is Flat);
            var minorKeyIndexer = MinorKeyByRoot.Instance;
            if (sharpKeyNote != null)
            {
                if (minorKeyIndexer.Dictionary.TryGetValue(sharpKeyNote, out var aMinorKey))
                {
                    // Success
                    minorKey = aMinorKey;
                    return true;
                }
            }

            if (flatKeyNote != null)
            {
                if (minorKeyIndexer.Dictionary.TryGetValue(flatKeyNote, out var aMinorKey))
                {
                    // Success
                    minorKey = aMinorKey;
                    return true;
                }
                
            }

            // Failure
            minorKey = default!;
            return false;
        }

        public override KeyMode KeyMode => KeyMode.Minor;
        public override KeyNote Root => GetRoot(KeySignature);

        public override string ToString() => KeySignature.Value switch
        {
            -7 => "Key of Abm",
            -6 => "Key of Ebm",
            -5 => "Key of Bbm",
            -4 => "Key of Fm",
            -3 => "Key of Cm",
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

        private static KeyNote GetRoot(KeySignature keySignature) => keySignature.Value switch
        {
            -7 => Flat.AFlat,
            -6 => Flat.EFlat,
            -5 => Flat.BFlat,
            -4 => Flat.F,
            -3 => Flat.C,
            -2 => Flat.G,
            -1 => Flat.D,
            0 => Sharp.A,
            1 => Sharp.E,
            2 => Sharp.B,
            3 => Sharp.FSharp,
            4 => Sharp.CSharp,
            5 => Sharp.GSharp,
            6 => Sharp.DSharp,
            7 => Sharp.ASharp,
            _ => throw new InvalidOperationException()
        };
    }
}