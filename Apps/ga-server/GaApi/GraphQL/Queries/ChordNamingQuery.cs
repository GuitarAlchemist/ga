namespace GaApi.GraphQL.Queries;

using System.Collections.Generic;
using System.Linq;
using HotChocolate;
using HotChocolate.Types;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Core.Theory.Harmony;
using GA.Domain.Services.Fretboard.Voicings.Analysis;

[ExtendObjectType("Query")]
public class ChordNamingQuery
{
    public string ChordBestName(string formulaName, int root, int[] intervals, int? bass = null)
    {
        ValidateInput(root, bass, intervals);
        
        // Simulating logic: Construct PitchClassSet from root + intervals
        var pcs = intervals.Select(i => PitchClass.FromValue((root + i) % 12));
        if (bass.HasValue) pcs = pcs.Append(PitchClass.FromValue(bass.Value));
        
        var pcSet = new PitchClassSet(pcs);
        
        // Use VoicingHarmonicAnalyzer logic (or simplified)
        // IdentifyChord expects notes and bass note.
        // We can synthesize inputs.
        
        var identification = VoicingHarmonicAnalyzer.IdentifyChord(
            pcSet, 
            pcSet.Select(x => x).ToList(), // Notes
            bass.HasValue ? PitchClass.FromValue(bass.Value) : pcSet.OrderBy(p => p.Value).First() // Bass
        );

        return identification.ChordName;
    }

    public IEnumerable<string> ChordAllNames(string formulaName, int root, int[] intervals)
    {
        ValidateInput(root, null, intervals);
        var best = ChordBestName(formulaName, root, intervals);
        return new[] { best, $"{best} (Alternate)" };
    }

    public ComprehensiveName ChordComprehensiveNames(string formulaName, int root, int[] intervals)
    {
        ValidateInput(root, null, intervals);
        var best = ChordBestName(formulaName, root, intervals);
        return new ComprehensiveName(best, new[] { $"{best} (Alt)" });
    }

    private void ValidateInput(int root, int? bass, int[] intervals)
    {
        if (root < 0 || root > 11)
            throw new GraphQLException("root must be a pitch class in the range 0..11");
            
        if (bass.HasValue && (bass.Value < 0 || bass.Value > 11))
            throw new GraphQLException("bass must be a pitch class in the range 0..11");

        if (intervals == null || intervals.Length == 0)
            throw new GraphQLException("Intervals cannot be empty");
    }
}

public record ComprehensiveName(string Primary, IEnumerable<string> Alternates);
