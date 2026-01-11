namespace GA.Business.Core.Notes;

using System;
using System.Collections.Generic;
using GA.Core.Collections;
using JetBrains.Annotations;

[PublicAPI]
public sealed class PitchCollection : LazyPrintableCollectionBase<Pitch>, IParsable<PitchCollection>
{
    /// <summary>
    ///     Empty <see cref="PitchCollection" />
    /// </summary>
    public static readonly PitchCollection Empty = new();

    public PitchCollection(params Pitch[] items) : base(items)
    {
    }

    public PitchCollection(IEnumerable<Pitch> items) : base([.. items])
    {
    }

    public PitchCollection(IReadOnlyCollection<Pitch> items) : base(items)
    {
    }

    #region IParsable{PitchCollection} members

    /// <inheritdoc />
    public static PitchCollection Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, null, out var result))
        {
            throw new PitchCollectionParseException();
        }

        return result;
    }

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out PitchCollection result)
    {
        ArgumentNullException.ThrowIfNull(s);

        result = Empty;

        var segments = s.Split(" ");
        var items = new List<Pitch>();
        foreach (var segment in segments)
        {
            if (!Pitch.Sharp.TryParse(segment, null, out var pitch))
            {
                return false; // Fail if one item fails parsing
            }

            items.Add(pitch);
        }

        // Success
        result = new(items);
        return true;
    }

    #endregion
}
