namespace GA.Business.Core.Tests.Integration;

// using GA.Business.Core.Services; // Namespace does not exist
// using GA.Business.Core.Spatial; // Namespace does not exist

#if false // TODO: Fix missing types: BSPEnhancedChordAnalysisService, BSPEnhancedProgressionAnalysisService, Position, Fretboard
[TestFixture]
public class BSPIntegrationTests
{
    private ServiceProvider _serviceProvider;
    private BSPEnhancedChordAnalysisService _chordAnalysisService;
    private BSPEnhancedProgressionAnalysisService _progressionAnalysisService;
    private TonalBSPService _bspService;
    private TonalBSPAnalyzer _bspAnalyzer;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
        
        // Add memory cache
        services.AddMemoryCache();
        
        // Add configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TonalBSP:MaxTreeDepth"] = "6",
                ["TonalBSP:MinElementsPerLeaf"] = "5",
                ["TonalBSP:EnableCaching"] = "true",
                ["TonalBSP:EnableDetailedLogging"] = "true"
            })
            .Build();
        
        services.AddSingleton<IConfiguration>(configuration);
        
        // Add Tonal BSP services with configuration
        services.AddTonalBSP(configuration);
        
        _serviceProvider = services.BuildServiceProvider();
        
        // Get services
        _chordAnalysisService = _serviceProvider.GetRequiredService<BSPEnhancedChordAnalysisService>();
        _progressionAnalysisService = _serviceProvider.GetRequiredService<BSPEnhancedProgressionAnalysisService>();
        _bspService = _serviceProvider.GetRequiredService<TonalBSPService>();
        _bspAnalyzer = _serviceProvider.GetRequiredService<TonalBSPAnalyzer>();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider?.Dispose();
    }

    [Test]
    public async Task BSPEnhancedChordAnalysis_WithMajorChord_ShouldProvideComprehensiveAnalysis()
    {
        // Arrange
        var chord = CreateChord("Major", [0, 4, 7]);
        var root = PitchClass.C;

        // Act
        var result = await _chordAnalysisService.AnalyzeChordAsync(chord, root);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Chord, Is.EqualTo(chord));
        Assert.That(result.Root, Is.EqualTo(root));
        Assert.That(result.SpatialContext, Is.Not.Null);
        Assert.That(result.SpatialContext.Confidence, Is.GreaterThan(0.0));
        Assert.That(result.HarmonicFunction, Is.Not.Null);
        Assert.That(result.SpatialMetrics, Is.Not.Null);
        Assert.That(result.TonalNeighbors, Is.Not.Null);
    }

    [Test]
    public async Task BSPEnhancedChordAnalysis_WithComplexChord_ShouldHandleGracefully()
    {
        // Arrange
        var chord = CreateChord("Maj7#11", [0, 4, 7, 11, 6]); // Complex jazz chord
        var root = PitchClass.C;

        // Act
        var result = await _chordAnalysisService.AnalyzeChordAsync(chord, root);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.SpatialContext.Region, Is.Not.Null);
        Assert.That(result.Substitutions, Is.Not.Null);
        Assert.That(result.HarmonicFunction.Function, Is.Not.Empty);
    }

    [Test]
    public async Task BSPEnhancedProgressionAnalysis_WithJazzProgression_ShouldAnalyzeTonalJourney()
    {
        // Arrange
        var progression = CreateJazzProgression();

        // Act
        var result = await _progressionAnalysisService.AnalyzeProgressionAsync(progression);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Progression.Count, Is.EqualTo(progression.Count));
        Assert.That(result.SpatialAnalysis, Is.Not.Null);
        Assert.That(result.SpatialAnalysis.TonalJourney.Count, Is.EqualTo(progression.Count));
        Assert.That(result.SpatialMetrics, Is.Not.Null);
        Assert.That(result.SpatialMetrics.OverallCoherence, Is.InRange(0.0, 1.0));
        Assert.That(result.ChordAnalyses.Count, Is.EqualTo(progression.Count));
        Assert.That(result.KeyAnalysis, Is.Not.Null);
    }

    [Test]
    public async Task BSPEnhancedProgressionAnalysis_WithPopProgression_ShouldIdentifyCommonPatterns()
    {
        // Arrange
        var progression = CreatePopProgression(); // I-V-vi-IV

        // Act
        var result = await _progressionAnalysisService.AnalyzeProgressionAsync(progression);

        // Assert
        Assert.That(result.SpatialAnalysis.OverallCoherence, Is.GreaterThan(0.6)); // Should be coherent
        Assert.That(result.KeyAnalysis.OverallTonalStrength, Is.GreaterThan(0.7)); // Should be strongly tonal
        Assert.That(result.SpatialMetrics.MovementPattern, Is.Not.Empty);
        Assert.That(result.Recommendations, Is.Not.Null);
    }

    [Test]
    public async Task BSPVoicingAnalysis_WithMultipleVoicings_ShouldRankByQuality()
    {
        // Arrange
        var chord = CreateChord("Major", [0, 4, 7]);
        var root = PitchClass.C;
        var voicings = CreateTestVoicings();
        var fretboard = CreateTestFretboard();

        // Act
        var result = await _chordAnalysisService.AnalyzeVoicingsAsync(chord, root, voicings, fretboard);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.AnalyzedVoicings.Count, Is.EqualTo(voicings.Count()));
        Assert.That(result.OptimalVoicing, Is.Not.Null);
        Assert.That(result.SpatialOptimizationSuggestions, Is.Not.Null);
        
        // Voicings should be ranked by overall score
        var scores = result.AnalyzedVoicings.Select(v => v.OverallScore).ToList();
        Assert.That(scores, Is.Ordered.Descending);
    }

    [Test]
    public async Task BSPRelatedChords_WithMajorChord_ShouldFindHarmonicallyRelated()
    {
        // Arrange
        var chord = CreateChord("Major", [0, 4, 7]);
        var root = PitchClass.C;

        // Act
        var result = await _chordAnalysisService.FindRelatedChordsAsync(chord, root);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.RelatedChords.Count, Is.GreaterThan(0));
        Assert.That(result.Relationships.Count, Is.EqualTo(result.RelatedChords.Count));
        Assert.That(result.SpatialQuery.QueryTime, Is.GreaterThan(TimeSpan.Zero));
        
        // Should find common related chords like Am, F, G
        var relatedNames = result.RelatedChords.Select(c => c.Name).ToList();
        Assert.That(relatedNames, Is.Not.Empty);
    }

    [Test]
    public async Task BSPProgressionSuggestions_ForKey_ShouldGenerateViableProgressions()
    {
        // Arrange
        var targetKey = new TonalRegion("C Major", TonalityType.Major, 
                                       new PitchClassSet([0, 2, 4, 5, 7, 9, 11]), 0);

        // Act
        var result = await _progressionAnalysisService.SuggestProgressionsAsync(targetKey, 4, "pop");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.TargetKey, Is.EqualTo(targetKey));
        Assert.That(result.Suggestions.Count, Is.GreaterThan(0));
        
        // All suggestions should be analyzed
        foreach (var suggestion in result.Suggestions)
        {
            Assert.That(suggestion.Progression.Count, Is.EqualTo(4));
            Assert.That(suggestion.Analysis, Is.Not.Null);
            Assert.That(suggestion.Score, Is.GreaterThan(0.0));
        }
        
        // Suggestions should be ranked by score
        var scores = result.Suggestions.Select(s => s.Score).ToList();
        Assert.That(scores, Is.Ordered.Descending);
    }

    [Test]
    public async Task BSPProgressionOptimization_WithWeakProgression_ShouldImprove()
    {
        // Arrange
        var weakProgression = CreateWeakProgression(); // Intentionally poor progression
        var goals = new OptimizationGoals
        {
            Priorities = ["coherence", "smoothness"],
            Weights = new Dictionary<string, double> { ["coherence"] = 0.6, ["smoothness"] = 0.4 }
        };

        // Act
        var result = await _progressionAnalysisService.OptimizeProgressionAsync(weakProgression, goals);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.OriginalProgression, Is.EqualTo(weakProgression));
        Assert.That(result.Optimizations.Count, Is.GreaterThan(0));
        
        if (result.RecommendedOptimization != null)
        {
            Assert.That(result.RecommendedOptimization.ImprovementScore, Is.GreaterThan(0.0));
            Assert.That(result.RecommendedOptimization.OptimizedProgression.Count, 
                       Is.EqualTo(weakProgression.Count));
        }
    }

    [Test]
    public async Task BSPProgressionComparison_WithMultipleProgressions_ShouldRankAccurately()
    {
        // Arrange
        var progressions = new List<List<(ChordTemplate chord, PitchClass root)>>
        {
            CreateJazzProgression(),
            CreatePopProgression(),
            CreateWeakProgression()
        };
        
        var criteria = new ComparisonCriteria
        {
            Factors = ["coherence", "complexity", "interest"],
            Weights = new Dictionary<string, double> 
            { 
                ["coherence"] = 0.4, 
                ["complexity"] = 0.3, 
                ["interest"] = 0.3 
            }
        };

        // Act
        var result = await _progressionAnalysisService.CompareProgressionsAsync(progressions, criteria);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Progressions.Count, Is.EqualTo(3));
        Assert.That(result.Analyses.Count, Is.EqualTo(3));
        Assert.That(result.Rankings.Count, Is.EqualTo(3));
        
        // Rankings should be ordered
        var ranks = result.Rankings.Select(r => r.Rank).ToList();
        Assert.That(ranks, Is.Ordered);
        
        // Each analysis should have valid scores
        foreach (var analysis in result.Analyses)
        {
            Assert.That(analysis.SpatialMetrics.OverallCoherence, Is.InRange(0.0, 1.0));
        }
    }

    [Test]
    public void BSPServiceIntegration_AllServicesResolved_ShouldNotThrow()
    {
        // Act & Assert - All services should resolve without throwing
        Assert.DoesNotThrow(() => _serviceProvider.GetRequiredService<TonalBSPService>());
        Assert.DoesNotThrow(() => _serviceProvider.GetRequiredService<TonalBSPAnalyzer>());
        Assert.DoesNotThrow(() => _serviceProvider.GetRequiredService<BSPEnhancedChordAnalysisService>());
        Assert.DoesNotThrow(() => _serviceProvider.GetRequiredService<BSPEnhancedProgressionAnalysisService>());
        Assert.DoesNotThrow(() => _serviceProvider.GetRequiredService<TonalBSPIntegratedFactory>());
    }

    [Test]
    public async Task BSPPerformanceTest_ManyAnalyses_ShouldCompleteInReasonableTime()
    {
        // Arrange
        var chords = new[]
        {
            (CreateChord("Major", [0, 4, 7]), PitchClass.C),
            (CreateChord("Minor", [0, 3, 7]), PitchClass.A),
            (CreateChord("7", [0, 4, 7, 10]), PitchClass.G),
            (CreateChord("m7", [0, 3, 7, 10]), PitchClass.D)
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Perform many analyses
        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            var (chord, root) = chords[i % chords.Length];
            tasks.Add(_chordAnalysisService.AnalyzeChordAsync(chord, root));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert - Should complete in reasonable time (less than 10 seconds for 50 analyses)
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(10000), 
                   $"50 chord analyses took {stopwatch.ElapsedMilliseconds}ms, which is too slow");
    }

    private ChordTemplate CreateChord(string name, int[] intervals)
    {
        return ChordTemplate.Atonal(new ChordFormula(name, intervals));
    }

    private List<(ChordTemplate chord, PitchClass root)> CreateJazzProgression()
    {
        return new List<(ChordTemplate, PitchClass)>
        {
            (CreateChord("Maj7", [0, 4, 7, 11]), PitchClass.C),   // Cmaj7
            (CreateChord("m7", [0, 3, 7, 10]), PitchClass.A),     // Am7
            (CreateChord("m7", [0, 3, 7, 10]), PitchClass.D),     // Dm7
            (CreateChord("7", [0, 4, 7, 10]), PitchClass.G),      // G7
            (CreateChord("Maj7", [0, 4, 7, 11]), PitchClass.C)    // Cmaj7
        };
    }

    private List<(ChordTemplate chord, PitchClass root)> CreatePopProgression()
    {
        return new List<(ChordTemplate, PitchClass)>
        {
            (CreateChord("Major", [0, 4, 7]), PitchClass.C),      // C
            (CreateChord("Major", [0, 4, 7]), PitchClass.G),      // G
            (CreateChord("Minor", [0, 3, 7]), PitchClass.A),      // Am
            (CreateChord("Major", [0, 4, 7]), PitchClass.F)       // F
        };
    }

    private List<(ChordTemplate chord, PitchClass root)> CreateWeakProgression()
    {
        return new List<(ChordTemplate, PitchClass)>
        {
            (CreateChord("Major", [0, 4, 7]), PitchClass.C),      // C
            (CreateChord("Major", [0, 4, 7]), PitchClass.FSharp), // F#
            (CreateChord("Minor", [0, 3, 7]), PitchClass.BFlat),  // Bbm
            (CreateChord("Dim", [0, 3, 6]), PitchClass.E)         // Edim
        };
    }

    private IEnumerable<ImmutableList<Position>> CreateTestVoicings()
    {
        return new[]
        {
            ImmutableList.Create(
                new Position(0, -1), new Position(1, 3), new Position(2, 2), 
                new Position(3, 0), new Position(4, 1), new Position(5, 0)
            ),
            ImmutableList.Create(
                new Position(0, 8), new Position(1, 8), new Position(2, 9), 
                new Position(3, 10), new Position(4, 8), new Position(5, -1)
            )
        };
    }

    private Fretboard.Fretboard CreateTestFretboard()
    {
        return new Fretboard.Fretboard(
            new Tuning("Standard", [PitchClass.E, PitchClass.A, PitchClass.D, PitchClass.G, PitchClass.B, PitchClass.E]),
            24
        );
    }
}
#endif
