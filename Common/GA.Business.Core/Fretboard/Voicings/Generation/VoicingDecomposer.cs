namespace GA.Business.Core.Fretboard.Voicings.Generation;

using Core;
using GA.Business.Core.Fretboard.Positions;
using GA.Business.Core.Fretboard.Primitives;
using GA.Core.Combinatorics;

/// <summary>
/// Decomposes voicings into relative fret vectors for pattern analysis
/// </summary>
public static class VoicingDecomposer
{
    /// <summary>
    /// Decomposes a list of voicings into relative fret vectors
    /// </summary>
    /// <param name="voicings">The voicings to decompose</param></param>
    /// <param name="vectorCollection">The collection of relative fret vectors to match against</param>
    /// <returns>List of decomposed voicings with their relative fret vectors</returns>
    public static List<DecomposedVoicing> DecomposeVoicings(
        List<Voicing> voicings,
        RelativeFretVectorCollection vectorCollection)
    {
        // Convert vectorCollection to array for O(1) access
        var vectorArray = vectorCollection.ToArray();

        // Access the internal variations for O(1) index lookup
        // Default parameters: fretExtent=5 (Range 0-4), stringCount=6
        var variations = new VariationsWithRepetitions<RelativeFret>(
            RelativeFret.Range(0, 5),
            length: 6);

        var results = new List<DecomposedVoicing>(voicings.Count);

        foreach (var voicing in voicings)
        {
            // Extract relative fret numbers from positions
            var relativeFrets = GetRelativeFrets(voicing.Positions);
            if (relativeFrets == null) continue; // Skip invalid voicings

            // O(1) lookup: Get the index of this variation, then lookup the vector from array
            var index = variations.GetIndex(relativeFrets);
            var matchingVector = vectorArray[(int)index];

            results.Add(new DecomposedVoicing(
                voicing,
                matchingVector,
                matchingVector as RelativeFretVector.PrimeForm,
                matchingVector as RelativeFretVector.Translation));
        }

        return results;
    }

    /// <summary>
    /// Extracts relative fret numbers from positions, normalizing to the minimum fret
    /// </summary>
    /// <param name="positions">The positions to extract relative frets from</param>
    /// <returns>Array of relative frets, or null if invalid</returns>
    public static RelativeFret[]? GetRelativeFrets(Position[] positions)
    {
        // Get fret numbers for all strings (use -1 for muted)
        var frets = new int[positions.Length];
        for (var i = 0; i < positions.Length; i++)
        {
            frets[i] = positions[i] switch
            {
                Position.Muted => -1,
                Position.Played played => played.Location.Fret.Value,
                _ => -1
            };
        }

        // Find minimum non-open, non-muted fret (for normalization)
        // Open strings (fret 0) are treated separately and not used for normalization
        var playedFrets = frets.Where(f => f > 0).ToList();
        if (!playedFrets.Any())
        {
            // All strings are either muted or open - use 0 as minimum
            var relativeFrets = new RelativeFret[positions.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                if (frets[i] < 0)
                {
                    relativeFrets[i] = RelativeFret.Min; // Muted
                }
                else
                {
                    // Open string - always maps to relative fret 0
                    relativeFrets[i] = RelativeFret.FromValue(0);
                }
            }
            return relativeFrets;
        }

        // Normalize to minimum fret (excluding open strings)
        var minPlayedFret = playedFrets.Min();
        var normalizedFrets = new RelativeFret[positions.Length];
        for (var i = 0; i < positions.Length; i++)
        {
            normalizedFrets[i] = frets[i] switch
            {
                < 0 => RelativeFret.Min,
                0 => RelativeFret.FromValue(0),
                _ => RelativeFret.FromValue(frets[i] - minPlayedFret)
            };
        }

        return normalizedFrets;
    }
}

/// <summary>
/// Represents a voicing decomposed into its relative fret vector
/// </summary>
/// <param name="Voicing">The original voicing</param>
/// <param name="Vector">The relative fret vector</param>
/// <param name="PrimeForm">The prime form if this is a prime form, null otherwise</param>
/// <param name="Translation">The translation if this is a translation, null otherwise</param>
public record DecomposedVoicing(
    Voicing Voicing,
    RelativeFretVector Vector,
    RelativeFretVector.PrimeForm? PrimeForm,
    RelativeFretVector.Translation? Translation);

