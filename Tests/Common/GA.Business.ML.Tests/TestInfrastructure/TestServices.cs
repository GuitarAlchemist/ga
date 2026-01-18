namespace GA.Business.ML.Tests.TestInfrastructure;

using GA.Business.ML.Embeddings;
using GA.Business.ML.Embeddings.Services;
using GA.Business.ML.Tabs;
using GA.Business.ML.Musical.Analysis;
using GA.Business.Core.Fretboard.Analysis;

public static class TestServices
{
    public static MusicalEmbeddingGenerator CreateGenerator()
    {
        return new MusicalEmbeddingGenerator(
            new IdentityVectorService(),
            new TheoryVectorService(),
            new MorphologyVectorService(),
            new ContextVectorService(),
            new SymbolicVectorService(),
            new PhaseSphereService());
    }

    public static TabAnalysisService CreateTabAnalysisService()
    {
        var tokenizer = new TabTokenizer();
        var converter = new TabToPitchConverter();
        var generator = CreateGenerator();
        var detector = new CadenceDetector();

        return new TabAnalysisService(tokenizer, converter, generator, detector);
    }

    public static FileBasedVectorIndex CreateTempIndex()
    {
        var tempFile = System.IO.Path.GetTempFileName();
        return new FileBasedVectorIndex(tempFile);
    }

    public static GA.Business.ML.Retrieval.StyleProfileService CreateStyleProfileService(FileBasedVectorIndex index)
    {
        return new GA.Business.ML.Retrieval.StyleProfileService(index);
    }

    public static AdvancedTabSolver CreateAdvancedTabSolver(FileBasedVectorIndex index)
    {
        var tuning = GA.Business.Core.Fretboard.Tuning.Default;
        var mapper = new FretboardPositionMapper(tuning);
        var cost = new PhysicalCostService();
        var style = CreateStyleProfileService(index);
        var generator = CreateGenerator();

        return new AdvancedTabSolver(mapper, cost, style, generator);
    }

    public static async Task<(MusicalEmbeddingGenerator Generator, FileBasedVectorIndex Index)> CreateAsync()
    {
        var generator = CreateGenerator();
        var index = CreateTempIndex();
        return (generator, index);
    }
}
