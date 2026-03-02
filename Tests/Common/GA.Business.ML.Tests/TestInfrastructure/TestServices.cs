namespace GA.Business.ML.Tests.TestInfrastructure;

using Embeddings;
using Embeddings.Services;
using GA.Business.ML.Tabs;
using Musical.Analysis;
using Domain.Services.Fretboard.Analysis;

public static class TestServices
{
    public static MusicalEmbeddingGenerator CreateGenerator() =>
        new MusicalEmbeddingGenerator(
            new IdentityVectorService(),
            new TheoryVectorService(),
            new MorphologyVectorService(),
            new ContextVectorService(),
            new SymbolicVectorService(),
            new ModalVectorService(),
            new PhaseSphereService());

    public static TabAnalysisService CreateTabAnalysisService()
    {
        var tokenizer = new TabTokenizer();
        var converter = new TabToPitchConverter();
        var generator = CreateGenerator();
        var detector = new CadenceDetector();

        return new(tokenizer, converter, generator, detector);
    }

    public static FileBasedVectorIndex CreateTempIndex()
    {
        var tempFile = Path.GetTempFileName();
        return new(tempFile);
    }

    public static Retrieval.StyleProfileService CreateStyleProfileService(FileBasedVectorIndex index) => new(index);

    public static AdvancedTabSolver CreateAdvancedTabSolver(FileBasedVectorIndex index)
    {
        var tuning = Domain.Core.Instruments.Tuning.Default;
        var mapper = new FretboardPositionMapper(tuning);
        var cost = new PhysicalCostService();
        var style = CreateStyleProfileService(index);
        var generator = CreateGenerator();

        return new(mapper, cost, style, generator);
    }

    public static async Task<(MusicalEmbeddingGenerator Generator, FileBasedVectorIndex Index)> CreateAsync()
    {
        var generator = CreateGenerator();
        var index = CreateTempIndex();
        return (generator, index);
    }
}
