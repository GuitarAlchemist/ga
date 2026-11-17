namespace GaApi.Services;

using GA.Core.Functional;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Services.Chords.Parsing;
using GaApi.Models;

public sealed class VoicingComfortService(VoicingFilterService voicingService)
{
    private static readonly Fretboard Fretboard = Fretboard.Default;

    /// <summary>
    ///     Voicings of <paramref name="chordName"/>, most comfortable first: root in the bass, then no
    ///     muted string between sounding strings (hard to damp), then closest to the nut, then the
    ///     smallest fret stretch, then the fullest sound. Ranking on stretch alone put shapes like
    ///     <c>x-0-10-10-x-x</c> first: stretch ignores open strings, so an open string plus a note at
    ///     fret 10 counts as no stretch at all.
    /// </summary>
    public async Task<Result<IReadOnlyList<ComfortRankedVoicing>, VoicingFilterError>> GetComfortRankedAsync(
        string chordName,
        bool excludeFullBarre = true,
        CancellationToken ct = default)
    {
        IEnumerable<VoicingWithAnalysis> all;
        try
        {
            all = await voicingService.GetVoicingsForChordAsync(chordName);
        }
        catch (ArgumentException)
        {
            return Result<IReadOnlyList<ComfortRankedVoicing>, VoicingFilterError>.Failure(
                VoicingFilterError.InvalidChordSymbol);
        }

        var root = new ChordSymbolParser().Parse(chordName).Root.PitchClass;

        List<VoicingWithAnalysis> voicings = excludeFullBarre
            ? [.. all.Where(v => !IsFullBarre(v))]
            : [.. all];

        IReadOnlyList<ComfortRankedVoicing> ranked =
        [
            .. voicings
                .Select(v => new ComfortRankedVoicing(v, ComputeStretch(v)))
                .OrderBy(r => BassPitchClass(r.Voicing) == root ? 0 : 1)
                .ThenBy(r => HasInnerMutedString(r.Voicing) ? 1 : 0)
                .ThenBy(r => r.Voicing.Frets.Max())
                .ThenBy(r => r.Stretch)
                .ThenByDescending(r => r.Voicing.Frets.Count(f => f >= 0)),
        ];

        return Result<IReadOnlyList<ComfortRankedVoicing>, VoicingFilterError>.Success(ranked);
    }

    // Fretted strings only (exclude open = 0, muted = -1)
    private static int ComputeStretch(VoicingWithAnalysis v)
    {
        var frettedFrets = v.Frets.Where(f => f > 0).ToList();
        return frettedFrets.Count == 0 ? 0 : frettedFrets.Max() - frettedFrets.Min();
    }

    // Full barre = 4+ strings sharing the same lowest fretted position
    private static bool IsFullBarre(VoicingWithAnalysis v)
    {
        var frettedFrets = v.Frets.Where(f => f > 0).ToList();
        if (frettedFrets.Count < 4) return false;
        var minFret = frettedFrets.Min();
        return frettedFrets.Count(f => f == minFret) >= 4;
    }

    // Frets hold string 1 (high E) first, so the bass is the last sounding string.
    private static PitchClass? BassPitchClass(VoicingWithAnalysis v)
    {
        var bassIndex = Array.FindLastIndex(v.Frets, f => f >= 0);
        return bassIndex < 0 ? null : Fretboard.GetPitchClass(bassIndex, v.Frets[bassIndex]);
    }

    private static bool HasInnerMutedString(VoicingWithAnalysis v)
    {
        var first = Array.FindIndex(v.Frets, f => f >= 0);
        var last = Array.FindLastIndex(v.Frets, f => f >= 0);
        for (var i = first + 1; i < last; i++)
        {
            if (v.Frets[i] < 0) return true;
        }

        return false;
    }
}

public sealed record ComfortRankedVoicing(VoicingWithAnalysis Voicing, int Stretch);
