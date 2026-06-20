namespace GA.Domain.Core.Theory.Atonal;

using Extensions;
using GA.Core.Collections.Abstractions;
using Primitives.Notes;

[PublicAPI]
public readonly record struct PitchClassSetId : IStaticReadonlyCollectionFromValues<PitchClassSetId>,
    IComparable<PitchClassSetId>
{
    private const int _minValue = 0;
    private const int _maxValue = 4095;
    private const int Mask12 = 0xFFF;

    private static readonly int[] _valuesArray = [.. Enumerable.Range(_minValue, _maxValue + 1)];

    public PitchClassSetId(int value) =>
        Value = ValueObjectUtils<PitchClassSetId>.EnsureValueRange(value, _minValue, _maxValue);

    public bool IsScale => (Value & 1) == 1;

    public int Cardinality => BitOperations.PopCount((uint)(Value & Mask12));

    public ChromaticNoteSet Notes => GetNotes(Value);

    public PitchClassSetId Complement => new(Value ^ Mask12);

    public PitchClassSetId Inverse => new(MirrorValue(Value));

    /// <summary>
    ///     Gets the M5 transform — multiply every pitch class by 5 (mod 12), the circle-of-fourths transform.
    /// </summary>
    public PitchClassSetId M5 => new(MultiplyValue(Value, 5));

    /// <summary>
    ///     Gets the M7 transform — multiply every pitch class by 7 (mod 12). Equal to M5 of the inversion.
    /// </summary>
    public PitchClassSetId M7 => new(MultiplyValue(Value, 7));

    public string BinaryValue => Convert.ToString(Value, 2).PadLeft(12, '0');

    public bool IsClusterFree
    {
        get
        {
            for (var i = 0; i < 12; i++)
            {
                var extended = Value | (Value << 12);
                if (((extended >> i) & 7) == 7)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public static ReadOnlySpan<PitchClassSetId> ItemsSpan => Items is PitchClassSetId[] arr ? arr : [.. Items];
    public static ReadOnlySpan<int> ValuesSpan => _valuesArray;

    public static IEqualityComparer<PitchClassSetId> ComplementComparer { get; } = new ComplementEqualityComparer();

    public int Value { get; }

    public static PitchClassSetId FromValue(int value) => new(value);
    public static implicit operator PitchClassSetId(int value) => new(value);
    public static implicit operator int(PitchClassSetId id) => id.Value;

    public static IReadOnlyCollection<PitchClassSetId> Items { get; } =
        [.. Enumerable.Range(_minValue, _maxValue - _minValue + 1).Select(i => new PitchClassSetId(i))];

    public static PitchClassSetId Min => new(_minValue);
    public static PitchClassSetId Max => new(_maxValue);

    public int CompareTo(PitchClassSetId other) => Value.CompareTo(other.Value);

    public PitchClassSetId Transpose(int semitones)
    {
        var n = (semitones % 12 + 12) % 12;
        var v = (uint)Value & Mask12;
        var rot = ((v << n) | (v >> (12 - n))) & Mask12;
        return new((int)rot);
    }

    public PitchClassSetId Rotate(int count) => Transpose(count); // Rotation of PC set is transposition

    public IEnumerable<PitchClassSetId> GetRotations()
    {
        for (var i = 0; i < 12; i++)
        {
            yield return Rotate(i);
        }
    }

    /// <summary>
    ///     The transposition-only prime form (the OPTC / "Tn-type" representative): the smallest id among
    ///     the twelve transpositions. Folds in transposition only, so a major triad and a minor triad have
    ///     <em>different</em> transposition prime forms but the same <see cref="PrimeForm" />.
    /// </summary>
    /// <remarks>
    ///     The canonicalization authority for <see cref="TranspositionClass" />. Pure id arithmetic — no
    ///     <see cref="PitchClassSet" /> allocation.
    /// </remarks>
    public PitchClassSetId TranspositionPrimeForm
    {
        get
        {
            var min = Value;
            for (var i = 1; i < 12; i++)
            {
                var t = Transpose(i).Value;
                if (t < min)
                {
                    min = t;
                }
            }

            return new(min);
        }
    }

    /// <summary>
    ///     The transposition/inversion prime form (the OPTIC / set-class representative): the smallest id
    ///     among the twenty-four transposition and inversion forms.
    /// </summary>
    /// <remarks>
    ///     The canonicalization authority that <see cref="PitchClassSet.PrimeForm" /> and
    ///     <see cref="SetClass" /> are built on. Defined by <b>minimal packed id</b> — this is distinct
    ///     from <see cref="PitchClassSet.ToNormalForm" />, which canonicalises by interval-span
    ///     <em>compactness</em>, not minimal id, and can pick a different rotation.
    /// </remarks>
    public PitchClassSetId PrimeForm
    {
        get
        {
            var min = Value;
            var inverse = Inverse;
            for (var i = 0; i < 12; i++)
            {
                var t = Transpose(i).Value;
                if (t < min)
                {
                    min = t;
                }

                var ti = inverse.Transpose(i).Value;
                if (ti < min)
                {
                    min = ti;
                }
            }

            return new(min);
        }
    }

    /// <summary>
    ///     Multiplies every pitch class by <paramref name="multiplier" /> (mod 12). A bijection only when the
    ///     multiplier is coprime to 12 (1, 5, 7, 11): 5 and 7 give the M5 / M7 transforms and 11 is the inversion.
    /// </summary>
    public PitchClassSetId Multiply(int multiplier) => new(MultiplyValue(Value, multiplier));

    private static int MultiplyValue(int value, int multiplier)
    {
        var result = 0;
        for (var pc = 0; pc < 12; pc++)
        {
            if ((value & (1 << pc)) != 0)
            {
                result |= 1 << ((pc * multiplier % 12 + 12) % 12);
            }
        }

        return result;
    }

    private static int MirrorValue(int value)
    {
        var result = 0;
        for (var i = 0; i < 12; i++)
        {
            var bitPosition = (12 - i) % 12;
            if ((value & (1 << i)) != 0)
            {
                result |= 1 << bitPosition;
            }
        }

        return result;
    }

    private static ChromaticNoteSet GetNotes(int value)
    {
        Span<Note.Chromatic> notes = new Note.Chromatic[12];
        var count = 0;
        for (var i = 0; i < 12; i++)
        {
            if ((value & (1 << i)) != 0)
            {
                notes[count++] = new(i);
            }
        }

        return ChromaticNoteSet.Create(notes[..count]);
    }

    public static PitchClassSetId FromPitchClasses(ReadOnlySpan<PitchClass> pitchClasses)
    {
        var val = 0;
        foreach (var pc in pitchClasses)
        {
            val |= 1 << pc.Value;
        }

        return new(val);
    }

    public static PitchClassSetId FromPitchClasses(IEnumerable<PitchClass> pitchClasses) =>
        FromPitchClasses([.. pitchClasses]);

    /// <summary>
    ///     Custom PrintMembers to avoid stack overflow from nested Notes.ToString() calls.
    /// </summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Value = {Value}, BinaryValue = {BinaryValue}, Cardinality = {Cardinality}");
        return true;
    }

    public PitchClassSet ToPitchClassSet() => Notes.ToPitchClassSet();

    private class ComplementEqualityComparer : IEqualityComparer<PitchClassSetId>
    {
        public bool Equals(PitchClassSetId x, PitchClassSetId y) => x.Value == y.Value || x.Complement.Value == y.Value;
        public int GetHashCode(PitchClassSetId obj) => Math.Min(obj.Value, obj.Complement.Value).GetHashCode();
    }
}
