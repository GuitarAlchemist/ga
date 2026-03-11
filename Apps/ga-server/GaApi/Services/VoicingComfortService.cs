namespace GaApi.Services;

using GA.Core.Functional;
using GaApi.Models;

public sealed class VoicingComfortService(VoicingFilterService voicingService)
{
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

        List<VoicingWithAnalysis> voicings = excludeFullBarre
            ? [.. all.Where(v => !IsFullBarre(v))]
            : [.. all];

        IReadOnlyList<ComfortRankedVoicing> ranked =
        [
            .. voicings
                .Select(v => new ComfortRankedVoicing(v, ComputeStretch(v)))
                .OrderBy(r => r.Stretch),
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
}

public sealed record ComfortRankedVoicing(VoicingWithAnalysis Voicing, int Stretch);
