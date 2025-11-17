namespace GA.Business.Core.Fretboard.Invariants;

using System.Collections.Immutable;

/// <summary>
/// Canonical identifier for a guitar chord fret pattern (6-length int array with -1 for muted strings).
/// The pattern is stored in normalized form where the minimum fretted value (ignoring -1) is translated to 0.
/// </summary>
[PublicAPI]
public readonly struct PatternId : IEquatable<PatternId>
{
    private readonly ImmutableArray<int> _normalized;

    private PatternId(ImmutableArray<int> normalized)
    {
        _normalized = normalized;
    }

    /// <summary>
    /// Creates a PatternId from a raw pattern (length 6). Values: -1 for muted, 0+ for fretted/open.
    /// The returned ID is normalized so the minimal non-muted fret becomes 0.
    /// </summary>
    public static PatternId FromPattern(int[] frets)
    {
        ArgumentNullException.ThrowIfNull(frets);
        if (frets.Length != 6)
        {
            throw new ArgumentException("Pattern must have 6 elements (one per string)", nameof(frets));
        }

        var normalized = PatternIdExtensions.NormalizePattern(frets);
        return new PatternId([.. normalized]);
    }

    /// <summary>
    /// Returns the normalized pattern as an array copy.
    /// </summary>
    public int[] ToPattern()
    {
        return [.. _normalized];
    }

    /// <summary>
    /// Returns a human-readable form like "X-3-2-0-1-0".
    /// </summary>
    public string ToPatternString()
    {
        if (_normalized.IsDefaultOrEmpty) return string.Empty;
        Span<char> buffer = stackalloc char[0];
        // simple string join for clarity
        return string.Join('-', _normalized.Select(v => v < 0 ? "X" : v.ToString()));
    }

    /// <summary>
    /// Basic validity check: pattern length is 6 and the span (max-min of non-muted frets) <= 5.
    /// </summary>
    public bool IsValidChordPattern()
    {
        if (_normalized.IsDefaultOrEmpty || _normalized.Length != 6) return false;
        var min = int.MaxValue;
        var max = int.MinValue;
        foreach (var v in _normalized)
        {
            if (v < 0) continue; // muted
            if (v < min) min = v;
            if (v > max) max = v;
        }

        if (min == int.MaxValue) return true; // all muted is technically valid edge-case
        return max - min <= 5;
    }

    /// <summary>
    /// Rough complexity score for pattern comparison.
    /// </summary>
    public int GetComplexityScore()
    {
        if (_normalized.IsDefaultOrEmpty) return 0;

        // Factors: span, count of fretted notes, barre/duplicates
        var span = GetSpan(_normalized);
        var frettedCount = _normalized.Count(v => v > 0);
        var zeros = _normalized.Count(v => v == 0);
        var duplicates = _normalized.GroupBy(v => v).Where(g => g.Key >= 0 && g.Count() > 1).Sum(g => g.Count() - 1);

        var score = span * 3 + frettedCount * 2 + duplicates;
        // open strings decrease difficulty slightly
        score -= Math.Min(zeros, 3);
        return Math.Max(0, score);
    }

    private static int GetSpan(ImmutableArray<int> values)
    {
        var min = int.MaxValue;
        var max = int.MinValue;
        foreach (var v in values)
        {
            if (v < 0) continue;
            if (v < min) min = v;
            if (v > max) max = v;
        }

        if (min == int.MaxValue) return 0; // no fretted note
        return max - min;
    }

    #region Equality

    public bool Equals(PatternId other)
    {
        if (_normalized.Length != other._normalized.Length) return false;
        for (var i = 0; i < _normalized.Length; i++)
        {
            if (_normalized[i] != other._normalized[i]) return false;
        }
        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is PatternId other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var v in _normalized)
            {
                hash = hash * 31 + v.GetHashCode();
            }
            return hash;
        }
    }

    public static bool operator ==(PatternId left, PatternId right) => left.Equals(right);
    public static bool operator !=(PatternId left, PatternId right) => !left.Equals(right);

    public override string ToString() => ToPatternString();

    #endregion
}

public static class PatternIdExtensions
{
    /// <summary>
    /// Normalize a fret pattern so the minimum non-muted fret becomes 0. Muted (-1) stays -1.
    /// </summary>
    public static int[] NormalizePattern(int[] frets)
    {
        ArgumentNullException.ThrowIfNull(frets);
        if (frets.Length != 6)
        {
            throw new ArgumentException("Pattern must have 6 elements", nameof(frets));
        }

        var min = int.MaxValue;
        foreach (var v in frets)
        {
            if (v >= 0 && v < min) min = v;
        }

        if (min == int.MaxValue)
        {
            // all muted -> return copy of input (or zeros?) Keep as-is to preserve shape semantics
            return [.. frets];
        }

        var result = new int[frets.Length];
        for (var i = 0; i < frets.Length; i++)
        {
            var v = frets[i];
            result[i] = v < 0 ? -1 : v - min;
        }
        return result;
    }

    /// <summary>
    /// Convert a raw fret array to a PatternId.
    /// </summary>
    public static PatternId ToPatternId(this int[] frets)
    {
        return PatternId.FromPattern(frets);
    }
}
