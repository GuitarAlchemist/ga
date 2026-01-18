using System;
using Microsoft.Extensions.DependencyInjection;
using GaChatbot.Services;
using GaChatbot.Models;
using GA.Business.ML.Tabs;
using GA.Business.ML.Retrieval;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Extensions;
using GA.Business.ML.Musical.Explanation;
using GA.Business.ML.Naturalness;
using GA.Business.ML.Abstractions;
using GA.Business.Core.Fretboard.Analysis;
using GA.Business.Core.Fretboard.Voicings.Search;
using GaChatbot.Abstractions;
using Moq;

public class DiDiagnostics
{
    public static void Main()
    {
        var services = new ServiceCollection();
        
        // 1. Core AI Services
        services.AddGuitarAlchemistAI();
        
        // 2. Data Persistence (Files/Indexes)
        services.AddSingleton<IVectorIndex>(new Mock<IVectorIndex>().Object);
        
        // 3. Domain Services
        services.AddSingleton<GA.Business.ML.Musical.Enrichment.ModalFlavorService>();
        services.AddSingleton<VoicingExplanationService>();
        services.AddSingleton<TabPresentationService>();
        services.AddSingleton<AdvancedTabSolver>();
        services.AddSingleton<SpectralRetrievalService>();
        
        // 4. Orchestration
        services.AddSingleton<SpectralRagOrchestrator>();
        services.AddSingleton<GA.Business.ML.Tabs.TabTokenizer>();
        services.AddSingleton<GA.Business.ML.Tabs.TabAnalysisService>();
        services.AddSingleton<GA.Business.ML.Tabs.AlternativeFingeringService>();
        services.AddSingleton<TabAwareOrchestrator>();
        services.AddSingleton<ProductionOrchestrator>();
        
        // 5. Mocks
        services.AddSingleton<GA.Business.Core.Fretboard.Analysis.PhysicalCostService>();
        services.AddSingleton<GroundedPromptBuilder>();
        services.AddSingleton<ResponseValidator>();
        
        // 5b. Tab Solver Dependencies
        services.AddSingleton(GA.Business.Core.Fretboard.Tuning.Default);
        services.AddSingleton<GA.Business.Core.Fretboard.Analysis.FretboardPositionMapper>();
        services.AddSingleton<StyleProfileService>();
        services.AddSingleton<IMlNaturalnessRanker, MlNaturalnessRanker>();
        services.AddSingleton<ITextEmbeddingService>(new Mock<ITextEmbeddingService>().Object);
        services.AddSingleton<MusicalEmbeddingGenerator>();
        services.AddSingleton<IGroundedNarrator, Mock<IGroundedNarrator>().Object>();

        try
        {
            var provider = services.BuildServiceProvider();
            var orchestrator = provider.GetRequiredService<ProductionOrchestrator>();
            Console.WriteLine("Success: ProductionOrchestrator resolved.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("DI Error:");
            PrintException(ex);
        }
    }

    private static void PrintException(Exception ex, int indent = 0)
    {
        string space = new string(' ', indent * 2);
        Console.WriteLine($"{space}{ex.GetType().Name}: {ex.Message}");
        if (ex.InnerException != null)
        {
            PrintException(ex.InnerException, indent + 1);
        }
    }
}
