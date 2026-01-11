namespace GA.Business.Core.AI.Embeddings;

using System.Linq;
using System.Threading.Tasks;
using GA.Business.Core.Scales;

public class ScaleEmbeddingGenerator
{
    private readonly IdentityVectorService _identityService;
    private readonly TheoryVectorService _theoryService;

    public int Dimension => EmbeddingSchema.TotalDimension;

    public ScaleEmbeddingGenerator(IdentityVectorService identityService, TheoryVectorService theoryService)
    {
        _identityService = identityService;
        _theoryService = theoryService;
    }

    public double[] Generate(Scale scale)
    {
        // Scales don't always have a single "Root" in the PitchClassSet sense, 
        // but the Scale object usually implies a tonic if it's "C Major".
        // We'll check the first note (Tonic).
        var rootPc = scale.Count > 0 ? scale.First().PitchClass.Value : (int?)null;

        // 1. Identity
        var identityVector = _identityService.ComputeEmbedding(IdentityVectorService.ObjectKind.Scale);

        // 2. Structure
        var structureVector = _theoryService.ComputeEmbedding(
            pitchClasses: scale.PitchClassSet.Select(p => p.Value),
            rootPitchClass: rootPc,
            intervalClassVector: scale.PitchClassSet.IntervalClassVector.ToString(),
            complementarity: 0.0
        );

        // Assemble
        var fullVector = new double[Dimension];
        Array.Copy(identityVector, 0, fullVector, EmbeddingSchema.IdentityOffset, identityVector.Length);
        Array.Copy(structureVector, 0, fullVector, EmbeddingSchema.StructureOffset, structureVector.Length);
        
        return fullVector;
    }
}
