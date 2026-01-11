namespace GA.Business.Core.AI.Embeddings;

using System.Linq;
using System.Threading.Tasks;
using GA.Business.Core.Chords;

public class ChordEmbeddingGenerator
{
    private readonly IdentityVectorService _identityService;
    private readonly TheoryVectorService _theoryService;

    public int Dimension => EmbeddingSchema.TotalDimension; 

    public ChordEmbeddingGenerator(IdentityVectorService identityService, TheoryVectorService theoryService)
    {
        _identityService = identityService;
        _theoryService = theoryService;
    }

    public double[] Generate(Chord chord)
    {
        // 1. Identity
        var identityVector = _identityService.ComputeEmbedding(IdentityVectorService.ObjectKind.Chord);

        // 2. Structure
        var structureVector = _theoryService.ComputeEmbedding(
            pitchClasses: chord.PitchClassSet.Select(p => p.Value),
            rootPitchClass: chord.Root.PitchClass.Value,
            intervalClassVector: chord.PitchClassSet.IntervalClassVector.ToString(),
            complementarity: 0.0
        );
        
        // Assemble
        var fullVector = new double[Dimension];
        Array.Copy(identityVector, 0, fullVector, EmbeddingSchema.IdentityOffset, identityVector.Length);
        Array.Copy(structureVector, 0, fullVector, EmbeddingSchema.StructureOffset, structureVector.Length);
        
        return fullVector;
    }
}
