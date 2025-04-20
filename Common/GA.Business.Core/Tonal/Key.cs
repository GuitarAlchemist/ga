using GA.Business.Core.Notes;

namespace GA.Business.Core.Tonal;

using GA.Business.Core.Notes.Primitives;
using GA.Business.Core.Intervals.Primitives;
using Intervals;
using Atonal;
using static Note;

/// <summary>
/// A musical key (<see cref="Major"/> | <see cref="Minor"/>)
/// </summary>
/// <param name="KeySignature"></param>
[PublicAPI]
public abstract record Key(KeySignature KeySignature) : IStaticPrintableReadonlyCollection<Key>
{
    #region IStaticPrintableReadonlyCollection<Key>

    /// <summary>
    /// Gets the <see cref="PrintableReadOnlyCollection{Key}"/> keys (15 <see cref="Major"/> keys and 15 <see cref="Minor"/> keys)
    /// </summary>
    public static PrintableReadOnlyCollection<Key> Items => Major.MajorItems.Cast<Key>().Concat(Minor.MinorItems).ToImmutableList().AsPrintable();

    #endregion

    #region Static Helpers

    /// <summary>
    /// Gets the collection of keys for the key mode (Major | Minor)
    /// </summary>
    /// <param name="keyMode">The <see cref="KeyMode"/></param>
    /// <returns>The <see cref="IReadOnlyCollection{Key}"/></returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="KeyMode"/> is not supported</exception>
    public static IReadOnlyCollection<Key> GetItems(KeyMode keyMode) => keyMode switch
    {
        KeyMode.Major => Major.MajorItems,
        KeyMode.Minor => Minor.MinorItems,
        _ => throw new ArgumentOutOfRangeException(nameof(keyMode), keyMode, null)
    };

    #endregion
    
    /// <summary>
    /// Gets the <see cref="KeyMode"/> (Major | Minor)
    /// </summary>
    public abstract KeyMode KeyMode { get; }

    /// <summary>
    /// Gets the <see cref="AccidentalKind"/>
    /// </summary>
    public AccidentalKind AccidentalKind => KeySignature.AccidentalKind;

    /// <summary>
    /// Gets the <see cref="KeyNote"/> key root
    /// </summary>
    public abstract KeyNote Root { get; }

    /// <summary>
    /// Gets the 7 notes in the key
    /// </summary>
    /// <returns>The <see cref="KeyNote"/> collection.</returns>
    public PrintableReadOnlyCollection<KeyNote> Notes => GetNotes().AsPrintable();

    /// <summary>
    /// Gets the <see cref="PitchClassSet"/>
    /// </summary>
    public PitchClassSet PitchClassSet => new(GetNotes().Select(n => n.PitchClass));

    /// <summary>
    /// Gets the simple interval between the key root note and the specified note
    /// </summary>
    /// <param name="note">The <see cref="Accidented"/></param>
    /// <returns>The <see cref="Interval.Simple"/></returns>
    public Interval.Simple GetInterval(Accidented note) => note.GetInterval(Root);

    /// <summary>
    /// Gets the 7 notes in the key
    /// </summary>
    /// <returns>The <see cref="KeyNote"/> collection.</returns>
    private ReadOnlyItems<KeyNote> GetNotes()
    {
        var accidentedNotes =
            KeySignature.AccidentedNotes
                .Select(note => note.NaturalNote)
                .ToImmutableHashSet();
        return new ReadOnlyItems<KeyNote>(
            KeySignature.Value < 0
                ? GetFlatNotes(Root, accidentedNotes).ToImmutableList()
                : GetSharpNotes(Root, accidentedNotes).ToImmutableList());

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
                    : new(naturalNote);
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


    #region Major Key
  
    /// <summary>
    /// A major key
    /// </summary>
    /// <param name="KeySignature"></param>
    [PublicAPI]
    public sealed record Major(KeySignature KeySignature)
        : Key(KeySignature)
    {
        /// <summary>
        /// Gets the <see cref="PrintableReadOnlyCollection{Major}"/> keys (15 <see cref="Major"/> keys)
        /// </summary>
        public static PrintableReadOnlyCollection<Major> MajorItems => Enumerable.Range(-7, 15).Select(i => new Major(i)).ToImmutableList().AsPrintable();
        
        #region Inner Classes

        public class MajorKeyByRoot() : LazyIndexerBase<KeyNote, Major>(GetMajorKeyByRoot())
        {
            public static readonly MajorKeyByRoot Instance = new();
            public static Key Get(KeyNote keyRoot) => keyRoot == null ? throw new ArgumentNullException(nameof(keyRoot)) : (Key)Instance[keyRoot];
            private static ImmutableDictionary<KeyNote, Major> GetMajorKeyByRoot() => MajorItems.ToImmutableDictionary(key => key.Root);
        }

        #endregion

        #region Well-known major keys

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

        #endregion

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
            majorKey = null!;
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

    #endregion
    
    #region Minor Key
   
    /// <summary>
    /// A minor key
    /// </summary>
    /// <param name="KeySignature"></param>
    [PublicAPI]
    public sealed record Minor(KeySignature KeySignature)
        : Key(KeySignature)
    {
        #region Inner Classes

        public class MinorKeyByRoot() : LazyIndexerBase<KeyNote, Minor>(GetMinorKeyByRoot())
        {
            public static readonly MinorKeyByRoot Instance = new();
            public static Key Get(KeyNote keyRoot) => keyRoot == null ? throw new ArgumentNullException(nameof(keyRoot)) : (Key)Instance[keyRoot];
            private static ImmutableDictionary<KeyNote, Minor> GetMinorKeyByRoot() => MinorItems.ToImmutableDictionary(key => key.Root);
        }

        #endregion

        #region Well-known minor keys
        
        public static Minor Abm => new(-7);
        public static Minor Ebm => new(-6);
        public static Minor Bbm => new(-5);
        public static Minor Fm => new(-4);
        public static Minor Cm => new(-3);
        public static Minor Gm => new(-2);
        public static Minor Dm => new(-1);
        public static Minor Am => new(0);
        public static Minor Em => new(1);
        public static Minor Bm => new(2);
        public static Minor FSharpm => new(3);
        public static Minor CSharpm => new(4);
        public static Minor GSharpm => new(5);
        public static Minor DSharpm => new(6);
        public static Minor ASharpm => new(7);
        
        #endregion
        
        /// <summary>
        /// Gets the <see cref="PrintableReadOnlyCollection{Minor}"/> keys (15 <see cref="Minor"/> keys)
        /// </summary>
        public static PrintableReadOnlyCollection<Minor> MinorItems => Enumerable.Range(-7, 15).Select(i => new Minor(i)).ToImmutableList().AsPrintable();
        
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
            minorKey = null!;
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
    
    #endregion
}