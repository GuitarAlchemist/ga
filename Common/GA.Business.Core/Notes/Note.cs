namespace GA.Business.Core.Notes;

using Atonal;
using GA.Business.Core.Atonal.Primitives;
using GA.Business.Core.Intervals.Primitives;
using Intervals;
using Primitives;
using Tonal;

/// <summary>
/// An abstract note
/// </summary>
/// <see cref="Chromatic"/>
/// <see cref="Sharp"/>, <see cref="Flat"/>, <see cref="AccidentedNote"/>
[PublicAPI]
public abstract record Note : IStaticNorm<Note, IntervalClass>,
                              IComparable<Note>
{
    #region IStaticNorm<Note> Members

    /// <inheritdoc cref="IStaticNorm{TSelf,TNorm}.GetNorm"/>
    public static IntervalClass GetNorm(Note item1, Note item2) => item1.GetIntervalClass(item2);

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
    
    /// <summary>
    /// Gets the <see cref="PitchClass"/>
    /// </summary>
    public abstract PitchClass PitchClass { get; }

    /// <summary>
    /// Get the accidented note
    /// </summary>
    /// <returns>
    /// The <see cref="AccidentedNote"/>
    /// </returns>
    public abstract AccidentedNote ToAccidentedNote();

    /// <summary>
    /// Gets the unsigned interval between another note and the current note
    /// </summary>
    /// <param name="other">The other <see cref="Note"/></param>
    /// <returns>The <see cref="Interval.Simple"/></returns>
    /// <exception cref="ArgumentNullException">Thrown when <see cref="other"/> is null</exception>
    public virtual Interval.Simple GetInterval(Note other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var startNote = ToAccidentedNote();
        var endNote = other.ToAccidentedNote();

        var result =
            endNote < startNote 
                ? endNote.GetInterval(startNote) 
                : startNote.GetInterval(endNote);

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
        var semitones = GetInterval(other).ToSemitones();
        return IntervalClass.FromSemitones(semitones);
    }

    /// <summary>
    /// A chromatic note
    /// </summary>
    [PublicAPI]
    public sealed record Chromatic(PitchClass PitchClass) : Note
    {
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

        public override PitchClass PitchClass { get; } = PitchClass;
        public override AccidentedNote ToAccidentedNote() => ToSharp().ToAccidentedNote();
        public Sharp ToSharp() => PitchClass.ToSharpNote();
        public Flat ToFlat() => PitchClass.ToFlatNote();

        public static implicit operator Chromatic(NaturalNote naturalNote) => new(naturalNote.ToPitchClass());
        public static Interval.Chromatic operator -(Chromatic note1, Chromatic note2) => note1.PitchClass - note2.PitchClass;

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
    /// Am note from a musical key
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
        public override AccidentedNote ToAccidentedNote() => new(NaturalNote, Accidental);
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
        public static Sharp Parse(string s, IFormatProvider? provider)
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
        
        public static implicit operator Chromatic(Sharp sharp) => new(sharp.PitchClass);

        public static IReadOnlyCollection<Sharp> NaturalNotes => [C, D, E, F, G, A, B];
        public static ImmutableArray<Sharp> Create(params Sharp[] notes) => [.. notes];

        /// <inheritdoc />
        public override AccidentalKind AccidentalKind => AccidentalKind.Sharp;
        
        /// <inheritdoc />
        public override Accidental? Accidental => SharpAccidental;

        /// <inheritdoc />
        public override string ToString() =>
            SharpAccidental.HasValue
                ? $"{NaturalNote}{SharpAccidental.Value}"
                : $"{NaturalNote}";

        /// <inheritdoc />
        protected override PitchClass GetPitchClass()
        {
            var value = NaturalNote.ToPitchClass().Value;
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
        public static Flat Parse(string s, IFormatProvider? provider)
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
            var value = NaturalNote.ToPitchClass().Value;
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
    public sealed record AccidentedNote(NaturalNote NaturalNote, Accidental? Accidental = null) : Note, IStaticReadonlyCollection<AccidentedNote>
    {
        #region IStaticReadonlyCollection<AccidentedNote> Members
       
        public static IReadOnlyCollection<AccidentedNote> Items => AllNotes.Instance;

        #endregion

        public static implicit operator Chromatic(AccidentedNote accidentedNote) => new(accidentedNote.PitchClass);
        public static implicit operator AccidentedNote(KeyNote keyNote) => new(keyNote.NaturalNote, keyNote.Accidental);
        public static Interval.Simple operator -(AccidentedNote note1, AccidentedNote note2) => note1.GetInterval(note2);
        
        public static ImmutableArray<Sharp> Create(params Sharp[] notes) => [.. notes];

        #region Well-known accidented notes
       
        public static AccidentedNote C => new(NaturalNote.C);
        public static AccidentedNote Cb => new(NaturalNote.C, FlatAccidental.Flat);
        public static AccidentedNote CSharp => new(NaturalNote.C, SharpAccidental.Sharp);
        public static AccidentedNote D => new(NaturalNote.D);
        public static AccidentedNote Db => new(NaturalNote.D, FlatAccidental.Flat);
        public static AccidentedNote DSharp => new(NaturalNote.D, SharpAccidental.Sharp);
        public static AccidentedNote E => new(NaturalNote.E);
        public static AccidentedNote Eb => new(NaturalNote.E, FlatAccidental.Flat);
        public static AccidentedNote ESharp => new(NaturalNote.E, SharpAccidental.Sharp);
        public static AccidentedNote F => new(NaturalNote.F);
        public static AccidentedNote Fb => new(NaturalNote.F, FlatAccidental.Flat);
        public static AccidentedNote FSharp => new(NaturalNote.F, SharpAccidental.Sharp);
        public static AccidentedNote G => new(NaturalNote.G);
        public static AccidentedNote Gb => new(NaturalNote.G, FlatAccidental.Flat);
        public static AccidentedNote GSharp => new(NaturalNote.G, SharpAccidental.Sharp);
        public static AccidentedNote A => new(NaturalNote.A);
        public static AccidentedNote Ab => new(NaturalNote.A, FlatAccidental.Flat);
        public static AccidentedNote ASharp => new(NaturalNote.A, SharpAccidental.Sharp);
        public static AccidentedNote B => new(NaturalNote.B);
        public static AccidentedNote Bb => new(NaturalNote.B, FlatAccidental.Flat);
        public static AccidentedNote BSharp => new(NaturalNote.B, SharpAccidental.Sharp);
        
        #endregion

        public override PitchClass PitchClass => GetPitchClass();
        public override AccidentedNote ToAccidentedNote() => this;
        public override Interval.Simple GetInterval(Note other) => GetInterval(other.ToAccidentedNote());
        public Interval.Simple GetInterval(AccidentedNote other) => GetInterval(this, other);

        /// <inheritdoc />
        public override string ToString() => $"{NaturalNote}{Accidental}";

        private PitchClass GetPitchClass()
        {
            var value = NaturalNote.ToPitchClass().Value;
            if (Accidental.HasValue) value += Accidental.Value.Value;
            var result = new PitchClass { Value = value };

            return result;
        }

        /// <summary>
        /// Gets the interval between two accidented notes
        /// </summary>
        /// <param name="startNote"></param>
        /// <param name="endNote"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        private static Interval.Simple GetInterval(
            AccidentedNote startNote,
            AccidentedNote endNote)
        {
            if (startNote == endNote) return Interval.Simple.Unison;

            var majorKeyByNaturalNote = new Dictionary<NaturalNote, Key.Major>
            {
                [NaturalNote.C] = Key.Major.C,
                [NaturalNote.D] = Key.Major.D,
                [NaturalNote.E] = Key.Major.E,
                [NaturalNote.F] = Key.Major.F,
                [NaturalNote.G] = Key.Major.G,
                [NaturalNote.A] = Key.Major.A,
                [NaturalNote.B] = Key.Major.B
            };

            if (!majorKeyByNaturalNote.TryGetValue(startNote.NaturalNote, out var key)) throw new InvalidOperationException($"No major key found for {startNote.NaturalNote}");
            var size = endNote.NaturalNote - startNote.NaturalNote;
            var qualityIncrement = GetQualityIncrement(key, startNote, endNote);
            var quality = GetQuality(size, qualityIncrement);
            var result = new Interval.Simple
            {
                Size = size,
                Quality = quality
            };

            return result;

            static Semitones GetQualityIncrement(
                Key key, 
                AccidentedNote startNote,
                AccidentedNote endNote)
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
                IntervalSize number,
                Semitones qualityIncrement)
            {
                if (number.Consonance == IntervalSizeConsonance.Perfect)
                {
                    var result = qualityIncrement.Value switch
                    {
                        -2 => IntervalQuality.DoublyDiminished,
                        -1 => IntervalQuality.Diminished,
                        0 => IntervalQuality.Perfect,
                        1 => IntervalQuality.Augmented,
                        2 => IntervalQuality.DoublyAugmented,
                        _ => throw new NotSupportedException()
                    };

                    return result;
                }
                else
                {
                    var result = qualityIncrement.Value switch
                    {
                        -3 => IntervalQuality.DoublyDiminished,
                        -2 => IntervalQuality.Diminished,
                        -1 => IntervalQuality.Minor,
                        0 => IntervalQuality.Major,
                        1 => IntervalQuality.Augmented,
                        2 => IntervalQuality.DoublyAugmented,
                        _ => throw new NotSupportedException()
                    };

                    return result;
                }
            }
        }

        private class AllNotes() : LazyCollectionBase<AccidentedNote>(GetAll())
        {
            public static readonly AllNotes Instance = new();

            private static IEnumerable<AccidentedNote> GetAll()
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