namespace GA.Domain.Core.Theory.Tonal;

using Atonal;
using Core.Primitives.Extensions;
using Core.Primitives.Notes;
using GA.Core.Collections;
using GA.Core.Collections.Abstractions;
using Interval = Core.Primitives.Intervals.Interval;

/// <summary>
///     A musical key (<see cref="Major" /> | <see cref="Minor" />) (<see href="https://en.wikipedia.org/wiki/Key_(music)" />).
/// </summary>
/// <param name="KeySignature"></param>
[PublicAPI]
public abstract record Key(KeySignature KeySignature) : IStaticPrintableReadonlyCollection<Key>
{
    /// <summary>
    ///     Gets the <see cref="KeyMode" /> (Major | Minor)
    /// </summary>
    public abstract KeyMode KeyMode { get; }

    /// <summary>
    ///     Gets the <see cref="AccidentalKind" />
    /// </summary>
    public AccidentalKind AccidentalKind => KeySignature.AccidentalKind;

    /// <summary>
    ///     Gets the <see cref="Note.KeyNote" /> key root
    /// </summary>
    public abstract Note.KeyNote Root { get; }

    /// <summary>
    ///     Gets the 7 notes in the key
    /// </summary>
    /// <returns>The <see cref="Note.KeyNote" /> collection.</returns>
    public PrintableReadOnlyCollection<Note.KeyNote> Notes => GetNotes().AsPrintable();

    /// <summary>
    ///     Gets the <see cref="PitchClassSet" />
    /// </summary>
    public PitchClassSet PitchClassSet => new(GetNotes().Select(n => n.PitchClass));

    #region IStaticPrintableReadonlyCollection<Key>

    /// <summary>
    ///     Gets the <see cref="PrintableReadOnlyCollection{Key}" /> keys (15 <see cref="Major" /> keys and 15
    ///     <see cref="Minor" /> keys)
    /// </summary>
    public static PrintableReadOnlyCollection<Key> Items =>
        Major.MajorItems.Cast<Key>().Concat(Minor.MinorItems).ToImmutableList().AsPrintable();

    #endregion

    #region Static Helpers

    /// <summary>
    ///     Gets the collection of keys for the key mode (Major | Minor)
    /// </summary>
    /// <param name="keyMode">The <see cref="KeyMode" /></param>
    /// <returns>The <see cref="IReadOnlyCollection{T}" /></returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="KeyMode" /> is not supported</exception>
    public static IReadOnlyCollection<Key> GetItems(KeyMode keyMode) => keyMode switch
    {
        KeyMode.Major => Major.MajorItems,
        KeyMode.Minor => Minor.MinorItems,
        _ => throw new ArgumentOutOfRangeException(nameof(keyMode), keyMode, null)
    };

    #endregion

    /// <summary>
    ///     Gets the simple interval between the key root note and the specified note
    /// </summary>
    /// <param name="note">The <see cref="Note.Accidented" /></param>
    /// <returns>The <see cref="Interval.Simple" /></returns>
    public Interval.Simple GetInterval(Note.Accidented note) => note.GetInterval(Root);

    /// <summary>
    ///     Gets the 7 notes in the key
    /// </summary>
    /// <returns>The <see cref="Note.KeyNote" /> collection.</returns>
    private ReadOnlyItems<Note.KeyNote> GetNotes()
    {
        var accidentedNotes =
            KeySignature.AccidentedNotes
                .Select(note => note.NaturalNote)
                .ToImmutableHashSet();
        return new(
            KeySignature.Value < 0
                ? GetFlatNotes(Root, accidentedNotes).ToImmutableList()
                : [.. GetSharpNotes(Root, accidentedNotes)]);

        static IEnumerable<Note.KeyNote> GetSharpNotes(
            Note.KeyNote root,
            IReadOnlySet<NaturalNote> accidentedNotes)
        {
            yield return root;

            // Start with the root note
            var naturalNote = root.NaturalNote;

            // Iterate through all key notes
            for (var i = 0; i < 6; i++)
            {
                naturalNote++;
                var sharpNote = accidentedNotes.Contains(naturalNote)
                    ? new(naturalNote, SharpAccidental.Sharp)
                    : new Note.Sharp(naturalNote);
                yield return sharpNote;
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
                    : new Note.Flat(naturalNote);
                yield return item;
            }
        }
    }


    #region Major Key

    /// <summary>
    ///     A major key
    /// </summary>
    /// <param name="KeySignature"></param>
    [PublicAPI]
    public sealed record Major(KeySignature KeySignature)
        : Key(KeySignature)
    {
        /// <summary>
        ///     Gets the <see cref="PrintableReadOnlyCollection{Major}" /> keys (15 <see cref="DiatonicScale.Major" /> keys)
        /// </summary>
        public static PrintableReadOnlyCollection<Major> MajorItems =>
            Enumerable.Range(-7, 15).Select(i => new Major(i)).ToImmutableList().AsPrintable();

        public override KeyMode KeyMode => KeyMode.Major;
        public override Note.KeyNote Root => GetRoot(KeySignature);

        public static Key FromKeyRoot(Note.KeyNote keyRoot) => MajorKeyByRoot.Get(keyRoot);

        public static bool TryParse(string input, out Major majorKey)
        {
            if (!Note.KeyNote.TryParse(input, out var keyRootNotes))
            {
                throw new InvalidOperationException("Failed parsing key root");
            }

            var sharpKeyNote = keyRootNotes.FirstOrDefault(note => note is Note.Sharp);
            var flatKeyNote = keyRootNotes.FirstOrDefault(note => note is Note.Flat);
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
            -7 => Note.Flat.CFlat,
            -6 => Note.Flat.GFlat,
            -5 => Note.Flat.DFlat,
            -4 => Note.Flat.AFlat,
            -3 => Note.Flat.EFlat,
            -2 => Note.Flat.BFlat,
            -1 => Note.Flat.F,
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

        #region Inner Classes

        public class MajorKeyByRoot() : LazyIndexerBase<Note.KeyNote, Major>(GetMajorKeyByRoot())
        {
            public static readonly MajorKeyByRoot Instance = new();

            public static Key Get(Note.KeyNote keyRoot) => keyRoot == null
                ? throw new ArgumentNullException(nameof(keyRoot))
                : (Key)Instance[keyRoot];

            private static ImmutableDictionary<Note.KeyNote, Major> GetMajorKeyByRoot() =>
                MajorItems.ToImmutableDictionary(key => key.Root);
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
    }

    #endregion

    #region Minor Key

    /// <summary>
    ///     A minor key
    /// </summary>
    /// <param name="KeySignature"></param>
    [PublicAPI]
    public sealed record Minor(KeySignature KeySignature)
        : Key(KeySignature)
    {
        /// <summary>
        ///     Gets the <see cref="PrintableReadOnlyCollection{Minor}" /> keys (15 <see cref="Minor" /> keys)
        /// </summary>
        public static PrintableReadOnlyCollection<Minor> MinorItems =>
            Enumerable.Range(-7, 15).Select(i => new Minor(i)).ToImmutableList().AsPrintable();

        public override KeyMode KeyMode => KeyMode.Minor;
        public override Note.KeyNote Root => GetRoot(KeySignature);

        public static bool TryParse(string input, out Minor minorKey)
        {
            if (!Note.KeyNote.TryParse(input, out var keyRootNotes))
            {
                throw new InvalidOperationException("Failed parsing key root");
            }

            var sharpKeyNote = keyRootNotes.FirstOrDefault(note => note is Note.Sharp);
            var flatKeyNote = keyRootNotes.FirstOrDefault(note => note is Note.Flat);
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

        private static Note.KeyNote GetRoot(KeySignature keySignature) => keySignature.Value switch
        {
            -7 => Note.Flat.AFlat,
            -6 => Note.Flat.EFlat,
            -5 => Note.Flat.BFlat,
            -4 => Note.Flat.F,
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

        #region Inner Classes

        public class MinorKeyByRoot() : LazyIndexerBase<Note.KeyNote, Minor>(GetMinorKeyByRoot())
        {
            public static readonly MinorKeyByRoot Instance = new();

            public static Key Get(Note.KeyNote keyRoot) => keyRoot == null
                ? throw new ArgumentNullException(nameof(keyRoot))
                : (Key)Instance[keyRoot];

            private static ImmutableDictionary<Note.KeyNote, Minor> GetMinorKeyByRoot() =>
                MinorItems.ToImmutableDictionary(key => key.Root);
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
    }

    #endregion
}
