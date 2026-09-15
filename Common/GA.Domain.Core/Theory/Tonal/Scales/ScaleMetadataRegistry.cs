namespace GA.Domain.Core.Theory.Tonal.Scales;

using System;
using System.Collections.Generic;
using Atonal;

/// <summary>
///     Encapsulates scale presentation metadata without bloating core math structs.
/// </summary>
public record ScaleMetadata(
    int BinaryScaleId,
    string Name,
    Uri? VideoUrl,
    string ForteNumber,
    IReadOnlyList<Scale> Modes);

/// <summary>
///     Unified metadata registry consolidating scale names, video URLs, and mode derivations.
/// </summary>
public static class ScaleMetadataRegistry
{
    /// <summary>
    ///     Gets the unified presentation metadata for a binary scale ID.
    /// </summary>
    public static ScaleMetadata GetMetadata(int binaryScaleId)
    {
        var pcsId = PitchClassSetId.FromValue(binaryScaleId);
        var pcs = pcsId.ToPitchClassSet();

        var name = ScaleNameById.Get(pcsId);
        if (string.IsNullOrEmpty(name))
        {
            name = $"Scale-{binaryScaleId}";
        }

        var videoUrl = ScaleVideoUrlById.Get(pcsId);
        var forteNumber = ForteCatalog.GetForteNumber(pcs)?.ToString() ?? "n/a";


        var modes = pcs.ModalFamily?.Modes
            .Select(m => Scale.FromPitchClassSetId(m.Id))
            .ToList() ?? new List<Scale>();


        return new ScaleMetadata(binaryScaleId, name, videoUrl, forteNumber, modes);
    }

    /// <summary>
    ///     Gets the unified presentation metadata for a Scale object.
    /// </summary>
    public static ScaleMetadata GetMetadata(Scale scale)
    {
        ArgumentNullException.ThrowIfNull(scale);
        return GetMetadata(scale.PitchClassSet.Id.Value);
    }
}
