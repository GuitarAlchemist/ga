﻿namespace GA.Business.Core.Notes;

using Atonal;
using Atonal.Abstractions;
using GA.Business.Core.Atonal.Primitives;
using GA.Business.Core.Intervals.Primitives;
using Intervals;
using Primitives;
using Tonal;

/// <summary>
/// Note discriminated union
/// </summary>
/// <see cref="Chromatic"/> | <see cref="KeyNote"/> |  <see cref="Sharp"/> | <see cref="Flat"/> | <see cref="Accidented"/>
[PublicAPI]
public abstract record Note : IStaticPairNorm<Note, IntervalClass>,
                              IComparable<Note>,
                              IPitchClass
{
    #region IStaticPairNorm<Note> Members

    /// <inheritdoc cref="IStaticPairNorm{TSelf,TNorm}.GetPairNorm"/>
    public static IntervalClass GetPairNorm(Note item1, Note item2) => item1.GetIntervalClass(item2);

    #endregion

    #region Relational Comparers

    public int CompareTo(Note? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        return other is null ? 1 : PitchClass.CompareTo(other.PitchClass);
    }

    public static bool operator <(Note? left, Note? right) => Comparer<Note>.Default.Compare(left, right) < 0;
    public static bool operator >(Note? left, Note? right) => Comparer<Note>.Default.Compare(left, right) > 0;
    public static bool operator <=(Note? left, Note? right) => Comparer<Note>.Default.Compare(left, right) <= 0;
    public static bool operator >=(Note? left, Note? right) => Comparer<Note>.Default.Compare(left, right) >= 0;

    #endregion

    #region Equality Members

    public virtual bool Equals(Note? other)
    {
        if (other is null) return false;
        return ReferenceEquals(this, other) || PitchClass.Equals(other.PitchClass);
    }

    public override int GetHashCode() => PitchClass.GetHashCode();

    #endregion

    #region IPitchClass Members

    /// <inheritdoc cref="IPitchClass.PitchClass"/>
    public abstract PitchClass PitchClass { get; }

    #endregion

    /// <summary>
    /// Get the accidented note
    /// </summary>
    /// <returns>
    /// The <see cref="Accidented"/>
    /// </returns>
    public abstract Accidented ToAccidented();

    /// <summary>
    /// Gets the chromatic note
    /// </summary>
    /// <returns>
    /// The <see cref="Chromatic"/>
    /// </returns>
    public Chromatic ToChromatic() => new(PitchClass);

    /// <summary>
    /// Gets the unsigned interval between another note and the current note
    /// </summary>
    /// <param name="other">The other <see cref="Note"/></param>
    /// <returns>The <see cref="Interval.Simple"/></returns>
    /// <exception cref="ArgumentNullException">Thrown when <see cref="other"/> is null</exception>
    public virtual Interval.Simple GetInterval(Note other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var startNote = ToAccidented();
        var endNote = other.ToAccidented();

        var result = startNote.GetInterval(endNote);

        return result;
    }

    /// <summary>
    /// Gets the shortest distance between two notes
    /// </summary>
    /// <remarks>See https://en.wikipedia.org/wiki/Interval_class</remarks>
    /// <param name="other">The other <see cref="Note"/></param>
    /// <returns>The <see cref="Semitones"/> distance</returns>
    // ReSharper disable once InconsistentNaming
    public IntervalClass GetIntervalClass(Note other)
    {
        var semitones = GetInterval(other).Semitones;
        return IntervalClass.FromSemitones(semitones);
    }

    /// <summary>
    /// A chromatic note
    /// </summary>
    [PublicAPI]
    public sealed record Chromatic(int Value) : Note
    {
        #region Static Helpers

        public static Chromatic Get(int value) => ByValueIndexer.GetChromaticNote(value);

        public static ImmutableList<Chromatic> Items =>
            Enumerable
                .Range(0, 12)
                .Select(i => new Chromatic(i))
                .ToImmutableList();

        #endregion

        #region Well-known chromatic notes

        public static Chromatic C => new(0);
        public static Chromatic CSharpOrDFlat => new(1);
        public static Chromatic D => new(2);
        public static Chromatic DSharpOrEFlat => new(3);
        public static Chromatic E => new(4);
        public static Chromatic F => new(5);
        public static Chromatic FSharpOrGFlat => new(6);
        public static Chromatic G => new(7);
        public static Chromatic GSharpOrAFlat => new(8);
        public static Chromatic A => new(9);
        public static Chromatic ASharpOrBFlat => new(10);
        public static Chromatic B => new(11);

        #endregion

        #region Operators

        public static implicit operator Chromatic(int value) => Get(value);
        public static implicit operator Chromatic(PitchClass pitchClass) => new(pitchClass.Value);

        public static Interval.Chromatic operator -(Chromatic note1, Chromatic note2)
        {
            var normalizedPitchClass = note1.PitchClass - note2.PitchClass;
            return normalizedPitchClass.Value;
        }

        public static Pitch.Chromatic operator +(Chromatic note, Octave octave) => new (note, octave);

        #endregion

        #region Inner Classes

        private class ByValueIndexer() : LazyIndexerBase<int, Chromatic>(GetDictionary())
        {
            public static Chromatic GetChromaticNote(int value) => _instance[value];
            private static ImmutableDictionary<int, Chromatic> GetDictionary() => Items.ToImmutableDictionary(note => note.Value);

            private static readonly ByValueIndexer _instance = new();
        }

        #endregion

        public override PitchClass PitchClass { get; } = Value;
        public override Accidented ToAccidented() => ToSharp().ToAccidented();
        public Sharp ToSharp() => PitchClass.ToSharpNote();
        public Flat ToFlat() => PitchClass.ToFlatNote();


        /// <inheritdoc />
        public override string ToString()
        {
            var sharp = ToSharp();
            return !sharp.SharpAccidental.HasValue
                ? $"{sharp}" // No accidental
                : $"{sharp}/{ToFlat()}"; // With accidental
        }
    }

    /// <summary>
    /// A note from a musical key (<see cref="Note.KeyNote.Sharp"/> | <see cref="Note.KeyNote.Flat"/>  | <see cref="Note.KeyNote.Accidented"/>)
    /// </summary>
    /// <param name="NaturalNote"></param>
    [PublicAPI]
    public abstract record KeyNote(NaturalNote NaturalNote) : Note, IComparable<KeyNote>
    {
        #region Relational Members

        public int CompareTo(KeyNote? other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (other is null) return 1;
            var naturalNoteComparison = NaturalNote.CompareTo(other.NaturalNote);
            return naturalNoteComparison != 0 ? naturalNoteComparison : Nullable.Compare(Accidental, other.Accidental);
        }

        public static bool operator <(KeyNote? left, KeyNote? right) => Comparer<KeyNote>.Default.Compare(left, right) < 0;
        public static bool operator >(KeyNote? left, KeyNote? right) => Comparer<KeyNote>.Default.Compare(left, right) > 0;
        public static bool operator <=(KeyNote? left, KeyNote? right) => Comparer<KeyNote>.Default.Compare(left, right) <= 0;
        public static bool operator >=(KeyNote? left, KeyNote? right) => Comparer<KeyNote>.Default.Compare(left, right) >= 0;

        #endregion

        #region Parsing


        public static bool TryParse(string? input, out IReadOnlyCollection<KeyNote> keyNotes)
        {
            var builder = ImmutableList.CreateBuilder<KeyNote>();

            if (Sharp.TryParse(input, null, out var sharpKeyNote)) builder.Add(sharpKeyNote);
            if (Flat.TryParse(input, null, out var flatKeyNote)) builder.Add(flatKeyNote);
            keyNotes = builder.ToImmutable();
            return keyNotes.Count > 0;
        }

        #endregion

        /// <summary>
        /// Gets the <see cref="AccidentalKind"/>
        /// </summary>
        public abstract AccidentalKind AccidentalKind { get; }

        /// <summary>
        /// Gets the <see cref="Accidental"/>
        /// </summary>
        public abstract Accidental? Accidental { get; }

        /// <summary>
        /// Gets the chromatic note for the current key note
        /// </summary>
        /// <returns> The <see cref="Note.Chromatic"/> </returns>
        public Chromatic ToChromaticNote() => new(PitchClass);

        /// <summary>
        /// Gets the pitch class for the current key note
        /// </summary>
        /// <returns>The <see cref="PitchClass"/></returns>
        protected abstract PitchClass GetPitchClass();

        /// <inheritdoc />
        public override PitchClass PitchClass => GetPitchClass();

        /// <inheritdoc />
        public override Accidented ToAccidented() => new(NaturalNote, Accidental);
    }

    /// <summary>
    /// A note from a sharp musical key (C | C# | D | D# | E | F | F# | G | G# | A | A# | B)
    /// </summary>
    [PublicAPI]
    public sealed record Sharp(NaturalNote NaturalNote, SharpAccidental? SharpAccidental = null) : KeyNote(NaturalNote), IStaticReadonlyCollection<Sharp>, IParsable<Sharp>
    {
        #region IStaticReadonlyCollection<Sharp> Members

        public static IReadOnlyCollection<Sharp> Items => [C, CSharp, D, DSharp, E, F, FSharp, G, GSharp, A, ASharp, B];

        #endregion

        #region Well-known sharp notes

        public static Sharp C => new(NaturalNote.C);
        public static Sharp CSharp => new(NaturalNote.C, Primitives.SharpAccidental.Sharp);
        public static Sharp D => new(NaturalNote.D);
        public static Sharp DSharp => new(NaturalNote.D, Primitives.SharpAccidental.Sharp);
        public static Sharp E => new(NaturalNote.E);
        public static Sharp F => new(NaturalNote.F);
        public static Sharp FSharp => new(NaturalNote.F, Primitives.SharpAccidental.Sharp);
        public static Sharp G => new(NaturalNote.G);
        public static Sharp GSharp => new(NaturalNote.G, Primitives.SharpAccidental.Sharp);
        public static Sharp A => new(NaturalNote.A);
        public static Sharp ASharp => new(NaturalNote.A, Primitives.SharpAccidental.Sharp);
        public static Sharp B => new(NaturalNote.B);

        #endregion

        #region IParsable<Sharp> Members

        /// <inheritdoc />
        public static Sharp Parse(string s, IFormatProvider? provider = null)
        {
            if (!TryParse(s, provider, out var result)) throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
            return result;
        }

        /// <inheritdoc />
        public static bool TryParse(string? s, IFormatProvider? provider, out Sharp result)
        {
            result = null!;
            if (string.IsNullOrWhiteSpace(s)) return false;

            var normalizedInput = s.Trim().ToUpperInvariant().Replace("♯", "#");
            var sharpNote = normalizedInput switch
            {
                "C" => C,
                "C#" => CSharp,
                "D" => D,
                "D#" => DSharp,
                "E" => E,
                "F" => F,
                "F#" => FSharp,
                "G" => G,
                "G#" => GSharp,
                "A" => A,
                "A#" => ASharp,
                "B" => B,
                _ => null
            };

            result = sharpNote!;
            return sharpNote != null;
        }

        #endregion

        public static implicit operator Chromatic(Sharp sharp) => sharp.ToChromatic();

        public static IReadOnlyCollection<Sharp> NaturalNotes => [C, D, E, F, G, A, B];
        public static ImmutableArray<Sharp> Create(params Sharp[] notes) => [.. notes];

        /// <inheritdoc />
        public override AccidentalKind AccidentalKind => AccidentalKind.Sharp;

        /// <inheritdoc />
        public override Accidental? Accidental =>SharpAccidental;

        /// <inheritdoc />
        public override string ToString() =>
            SharpAccidental.HasValue
                ? $"{NaturalNote}{SharpAccidental.Value}"
                : $"{NaturalNote}";

        /// <inheritdoc />
        protected override PitchClass GetPitchClass()
        {
            var value = NaturalNote.PitchClass.Value;
            if (SharpAccidental.HasValue) value += SharpAccidental.Value.Value;
            var result = new PitchClass { Value = value };

            return result;
        }
    }

    /// <inheritdoc cref="Note.KeyNote"/>
    /// <summary>
    /// A flat key note
    /// </summary>
    [PublicAPI]
    public sealed record Flat(NaturalNote NaturalNote, FlatAccidental? FlatAccidental = null) : KeyNote(NaturalNote), IStaticReadonlyCollection<Flat>, IParsable<Flat>
    {
        #region IStaticReadonlyCollection<Flat> Members

        public static IReadOnlyCollection<Flat> Items => [C, DFlat, D, EFlat, E, F, GFlat, G, AFlat, A, BFlat, B];

        #endregion

        #region IParsable<Sharp> Members

        /// <inheritdoc />
        public static Flat Parse(string s, IFormatProvider? provider = null)
        {
            if (!TryParse(s, provider, out var result)) throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
            return result;
        }

        /// <inheritdoc />
        public static bool TryParse(string? s, IFormatProvider? provider, out Flat result)
        {
            result = default!;
            if (string.IsNullOrWhiteSpace(s)) return false;

            var normalizedInput = s.Trim().Replace("♭", "b").ToUpperInvariant();
            var flatNote = normalizedInput switch
            {
                "CB" => CFlat,
                "C" => C,
                "DB" => DFlat,
                "D" => D,
                "EB" => EFlat,
                "E" => E,
                "FB" => FFlat,
                "F" => F,
                "GB" => GFlat,
                "G" => G,
                "AB" => AFlat,
                "A" => A,
                "BB" => BFlat,
                "B" => B,
                _ => null
            };

            result = flatNote!;
            return flatNote != null;
        }

        #endregion

        #region Well-known flat notes

        public static Flat CFlat => new(NaturalNote.C, Primitives.FlatAccidental.Flat);
        public static Flat C => new(NaturalNote.C);
        public static Flat DFlat => new(NaturalNote.D, Primitives.FlatAccidental.Flat);
        public static Flat D => new(NaturalNote.D);
        public static Flat EFlat => new(NaturalNote.E, Primitives.FlatAccidental.Flat);
        public static Flat E => new(NaturalNote.E);
        public static Flat FFlat => new(NaturalNote.F, Primitives.FlatAccidental.Flat);
        public static Flat F => new(NaturalNote.F);
        public static Flat GFlat => new(NaturalNote.G, Primitives.FlatAccidental.Flat);
        public static Flat G => new(NaturalNote.G);
        public static Flat AFlat => new(NaturalNote.A, Primitives.FlatAccidental.Flat);
        public static Flat A => new(NaturalNote.A);
        public static Flat BFlat => new(NaturalNote.B, Primitives.FlatAccidental.Flat);
        public static Flat B => new(NaturalNote.B);

        #endregion

        public static ImmutableArray<Sharp> Create(params Sharp[] notes) => [.. notes];

        public static implicit operator Chromatic(Flat flatNote) => new(flatNote.PitchClass);

        /// <inheritdoc />
        public override PitchClass PitchClass => GetPitchClass();

        /// <inheritdoc />
        public override AccidentalKind AccidentalKind => AccidentalKind.Flat;

        /// <inheritdoc />
        public override Accidental? Accidental => FlatAccidental;

        /// <inheritdoc />
        public override string ToString() =>
            FlatAccidental.HasValue
                ? $"{NaturalNote}{FlatAccidental.Value}"
                : $"{NaturalNote}";

        /// <inheritdoc />
        protected override PitchClass GetPitchClass()
        {
            var value = NaturalNote.PitchClass.Value;
            if (FlatAccidental.HasValue) value += FlatAccidental.Value.Value;
            var result = new PitchClass { Value = value };

            return result;
        }
    }

    /// <inheritdoc cref="Note"/>
    /// <summary>
    /// A note with an optional accidental
    /// </summary>
    [PublicAPI]
    public sealed record Accidented(NaturalNote NaturalNote, Accidental? Accidental = null) : Note, IStaticReadonlyCollection<Accidented>, IParsable<Accidented>
    {
        #region IStaticReadonlyCollection<Accidented> Members

        public static IReadOnlyCollection<Accidented> Items => AllNotes.Instance;

        #endregion

        #region IParsable<Accidented> Members

        public static Accidented Parse(string s, IFormatProvider? provider)
        {
            if (!TryParse(s, provider, out var result)) throw new FormatException($"Invalid accidental string format: '{s}'.");
            return result;
        }

        public static bool TryParse(string? s, IFormatProvider? provider, out Accidented result)
        {
            result = null!;

            // Ensure valid string
            if (string.IsNullOrWhiteSpace(s)) return false;

            // Assume the string format generally follows "Note Accidental" (e.g., "C#", "Db", "E")
            // Split the input string into note and accidental components
            s = s.Trim();
            var lengthOfNote = s.Length;
            NaturalNote? naturalNote = null;
            Accidental? accidental = null;

            // Try parsing the longest possible NaturalNote first
            for (var i = Math.Min(2, lengthOfNote); i > 0; i--)
            {
                // Ensure natural part can be parsed
                var notePart = s[..i];
                if (!NaturalNote.TryParse(notePart, null, out var parsedNote)) continue;

                naturalNote = parsedNote;
                var accidentalPart = s.Substring(i).Trim();
                if (!string.IsNullOrEmpty(accidentalPart))
                {
                    // Ensure accidental part can be parsed
                    if (!Intervals.Primitives.Accidental.TryParse(accidentalPart, null, out var parsedAccidental)) return false; // If there's an accidental part but it can't be parsed
                    accidental = parsedAccidental;
                }
                break;
            }

            // Ensure valid nature note
            if (!naturalNote.HasValue) return false;

            result = new(naturalNote.Value, accidental);
            return true;
        }

        #endregion

        public static implicit operator Chromatic(Accidented accidented) => new(accidented.PitchClass);
        public static implicit operator Accidented(KeyNote keyNote) => new(keyNote.NaturalNote, keyNote.Accidental);
        public static Interval.Simple operator -(Accidented note1, Accidented note2) => note1.GetInterval(note2);

        public static ImmutableArray<Sharp> Create(params Sharp[] notes) => [.. notes];

        #region Well-known accidented notes

        public static Accidented C => new(NaturalNote.C);
        public static Accidented Cb => new(NaturalNote.C, FlatAccidental.Flat);
        public static Accidented CSharp => new(NaturalNote.C, SharpAccidental.Sharp);
        public static Accidented D => new(NaturalNote.D);
        public static Accidented Db => new(NaturalNote.D, FlatAccidental.Flat);
        public static Accidented DSharp => new(NaturalNote.D, SharpAccidental.Sharp);
        public static Accidented E => new(NaturalNote.E);
        public static Accidented Eb => new(NaturalNote.E, FlatAccidental.Flat);
        public static Accidented ESharp => new(NaturalNote.E, SharpAccidental.Sharp);
        public static Accidented F => new(NaturalNote.F);
        public static Accidented Fb => new(NaturalNote.F, FlatAccidental.Flat);
        public static Accidented FSharp => new(NaturalNote.F, SharpAccidental.Sharp);
        public static Accidented G => new(NaturalNote.G);
        public static Accidented Gb => new(NaturalNote.G, FlatAccidental.Flat);
        public static Accidented GSharp => new(NaturalNote.G, SharpAccidental.Sharp);
        public static Accidented A => new(NaturalNote.A);
        public static Accidented Ab => new(NaturalNote.A, FlatAccidental.Flat);
        public static Accidented ASharp => new(NaturalNote.A, SharpAccidental.Sharp);
        public static Accidented B => new(NaturalNote.B);
        public static Accidented Bb => new(NaturalNote.B, FlatAccidental.Flat);
        public static Accidented BSharp => new(NaturalNote.B, SharpAccidental.Sharp);

        #endregion

        public override PitchClass PitchClass => GetPitchClass();
        public override Accidented ToAccidented() => this;
        public override Interval.Simple GetInterval(Note other) => GetInterval(other.ToAccidented());
        public Interval.Simple GetInterval(Accidented other) => GetInterval(this, other);

        /// <inheritdoc />
        public override string ToString() => $"{NaturalNote}{Accidental}";

        private PitchClass GetPitchClass()
        {
            var value = NaturalNote.PitchClass.Value;
            if (Accidental.HasValue) value += Accidental.Value.Value;
            var result = new PitchClass { Value = value };

            return result;
        }

        private static readonly Lazy<Dictionary<NaturalNote, Key.Major>> _lazyMajorKeyByNaturalNote = new(
            () => new()
            {
                [NaturalNote.C] = Key.Major.C,
                [NaturalNote.D] = Key.Major.D,
                [NaturalNote.E] = Key.Major.E,
                [NaturalNote.F] = Key.Major.F,
                [NaturalNote.G] = Key.Major.G,
                [NaturalNote.A] = Key.Major.A,
                [NaturalNote.B] = Key.Major.B
            });

        /// <summary>
        /// Gets the interval between two accidented notes
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        private static Interval.Simple GetInterval(
            Accidented start,
            Accidented end)
        {
            if (start == end) return Interval.Simple.Unison;

            var majorKeyByNaturalNote = _lazyMajorKeyByNaturalNote.Value;
            if (!majorKeyByNaturalNote.TryGetValue(start.NaturalNote, out var key)) throw new InvalidOperationException($"No major key found for {start.NaturalNote}");
            var size = end.NaturalNote - start.NaturalNote;
            var qualityIncrement = GetQualityIncrement(key, start, end);
            var quality = GetQuality(size, qualityIncrement);
            var result = new Interval.Simple
            {
                Size = size,
                Quality = quality
            };

            return result;

            static Semitones GetQualityIncrement(
                Key key,
                Accidented startNote,
                Accidented endNote)
            {
                var result = Semitones.None;

                // Quality - Start note
                if (startNote.Accidental.HasValue) result -= startNote.Accidental.Value.ToSemitones();

                // Quality - End note
                var (endNaturalNote, endNoteAccidental) = endNote;
                if (key.KeySignature.IsNoteAccidented(endNaturalNote))
                {
                    var expectedEndNoteAccidental =
                        key.AccidentalKind == AccidentalKind.Flat
                            ? Intervals.Primitives.Accidental.Flat
                            : Intervals.Primitives.Accidental.Sharp;

                    if (endNoteAccidental == expectedEndNoteAccidental) return result;

                    var actualEndNoteAccidentalValue = endNoteAccidental?.Value ?? 0;
                    var endNoteAccidentalDelta = actualEndNoteAccidentalValue - expectedEndNoteAccidental.Value;
                    result += endNoteAccidentalDelta;
                }
                else if (endNoteAccidental.HasValue) result += endNoteAccidental.Value.ToSemitones();

                return result;
            }

            static IntervalQuality GetQuality(
                SimpleIntervalSize number,
                Semitones qualityIncrement)
            {
                if (number.Consonance == IntervalConsonance.Perfect)
                {
                    // Handle perfect intervals (unison, fourth, fifth, octave)
                    if (qualityIncrement.Value <= -2) return IntervalQuality.DoublyDiminished;
                    if (qualityIncrement.Value == -1) return IntervalQuality.Diminished;
                    if (qualityIncrement.Value == 0) return IntervalQuality.Perfect;
                    if (qualityIncrement.Value == 1) return IntervalQuality.Augmented;
                    if (qualityIncrement.Value >= 2) return IntervalQuality.DoublyAugmented;

                    // Default fallback (should never reach here)
                    return IntervalQuality.Perfect;
                }
                else
                {
                    // Handle imperfect intervals (seconds, thirds, sixths, sevenths)
                    if (qualityIncrement.Value <= -3) return IntervalQuality.DoublyDiminished;
                    if (qualityIncrement.Value == -2) return IntervalQuality.Diminished;
                    if (qualityIncrement.Value == -1) return IntervalQuality.Minor;
                    if (qualityIncrement.Value == 0) return IntervalQuality.Major;
                    if (qualityIncrement.Value == 1) return IntervalQuality.Augmented;
                    if (qualityIncrement.Value >= 2) return IntervalQuality.DoublyAugmented;

                    // Default fallback (should never reach here)
                    return IntervalQuality.Major;
                }
            }
        }

        private class AllNotes() : LazyCollectionBase<Accidented>(GetAll())
        {
            public static readonly AllNotes Instance = new();

            private static IEnumerable<Accidented> GetAll()
            {
                var accidentals = new[]
                {
                    Intervals.Primitives.Accidental.DoubleFlat,
                    Intervals.Primitives.Accidental.Flat,
                    Intervals.Primitives.Accidental.Natural,
                    Intervals.Primitives.Accidental.Sharp,
                    Intervals.Primitives.Accidental.DoubleSharp,
                };

                foreach (var naturalNote in NaturalNote.Items)
                    foreach (var accidental in accidentals)
                        yield return new(naturalNote, accidental);
            }
        }
    }
}