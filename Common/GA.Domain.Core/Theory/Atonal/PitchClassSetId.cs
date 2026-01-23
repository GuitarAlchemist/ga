namespace GA.Domain.Core.Theory.Atonal;

using Extensions;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Extensions;
using GA.Core.Collections;
using GA.Core.Collections.Abstractions;
using JetBrains.Annotations;
using Primitives;

[PublicAPI]
public readonly record struct PitchClassSetId : IStaticReadonlyCollectionFromValues<PitchClassSetId>, IComparable<PitchClassSetId>
{
    private const int _minValue = 0;
    private const int _maxValue = 4095;
    private const int Mask12 = 0xFFF;

    public int Value { get; }

    public PitchClassSetId(int value)
    {
        Value = ValueObjectUtils<PitchClassSetId>.EnsureValueRange(value, _minValue, _maxValue);
    }

    public static PitchClassSetId FromValue(int value) => new(value);
    public static implicit operator PitchClassSetId(int value) => new(value);
    public static implicit operator int(PitchClassSetId id) => id.Value;

    public bool IsScale => (Value & 1) == 1;

    public int Cardinality => BitOperations.PopCount((uint)(Value & Mask12));

    public ChromaticNoteSet Notes => GetNotes(Value);

    public PitchClassSetId Complement => new(Value ^ Mask12);

    public PitchClassSetId Inverse => new(MirrorValue(Value));
    
    public string BinaryValue => Convert.ToString(Value, 2).PadLeft(12, '0');

    public PitchClassSetId Transpose(int semitones)
    {
        var n = (semitones % 12 + 12) % 12;
        var v = (uint)Value & Mask12;
        var rot = (v << n | v >> 12 - n) & Mask12;
        return new((int)rot);
    }

    public PitchClassSetId Rotate(int count)
    {
        return Transpose(count); // Rotation of PC set is transposition
    }

    public IEnumerable<PitchClassSetId> GetRotations()
    {
        for (var i = 0; i < 12; i++)
        {
            yield return Rotate(i);
        }
    }

    public bool IsClusterFree
    {
        get
        {
            for(int i=0; i<12; i++)
            {
                var extended = Value | (Value << 12);
                if (((extended >> i) & 7) == 7) return false;
            }
            return true;
        }
    }

    private static int MirrorValue(int value)
    {
        var result = 0;
        for (var i = 0; i < 12; i++)
        {
            var bitPosition = (12 - i) % 12;
            if ((value & 1 << i) != 0) result |= 1 << bitPosition;
        }
        return result;
    }

    private static ChromaticNoteSet GetNotes(int value)
    {
        var notes = new List<Note.Chromatic>();
        for(int i=0; i<12; i++)
        {
            if ((value & (1 << i)) != 0) notes.Add(new Note.Chromatic(i));
        }
        return new ChromaticNoteSet(notes);
    }

    public static PitchClassSetId FromPitchClasses(IEnumerable<PitchClass> pitchClasses)
    {
        int val = 0;
        foreach(var pc in pitchClasses)
        {
            val |= 1 << pc.Value;
        }
        return new PitchClassSetId(val);
    }

    private static readonly IReadOnlyCollection<PitchClassSetId> _items = 
        Enumerable.Range(_minValue, _maxValue - _minValue + 1).Select(i => new PitchClassSetId(i)).ToImmutableList();

    public static IReadOnlyCollection<PitchClassSetId> Items => _items;
    public static ReadOnlySpan<PitchClassSetId> ItemsSpan => _items is List<PitchClassSetId> l ? System.Runtime.InteropServices.CollectionsMarshal.AsSpan(l) : _items.ToArray(); 
    public static ReadOnlySpan<int> ValuesSpan => Enumerable.Range(_minValue, _maxValue + 1).ToArray();

    public static PitchClassSetId Min => new(_minValue);
    public static PitchClassSetId Max => new(_maxValue);

    public int CompareTo(PitchClassSetId other) => Value.CompareTo(other.Value);

    /// <summary>
    /// Custom PrintMembers to avoid stack overflow from nested Notes.ToString() calls.
    /// </summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append($"Value = {Value}, BinaryValue = {BinaryValue}, Cardinality = {Cardinality}");
        return true;
    }
    
    public PitchClassSet ToPitchClassSet() => Notes.ToPitchClassSet();

    public static IEqualityComparer<PitchClassSetId> ComplementComparer { get; } = new ComplementEqualityComparer();

    private class ComplementEqualityComparer : IEqualityComparer<PitchClassSetId>
    {
        public bool Equals(PitchClassSetId x, PitchClassSetId y) => x.Value == y.Value || x.Complement.Value == y.Value;
        public int GetHashCode(PitchClassSetId obj) => Math.Min(obj.Value, obj.Complement.Value).GetHashCode();
    }
}