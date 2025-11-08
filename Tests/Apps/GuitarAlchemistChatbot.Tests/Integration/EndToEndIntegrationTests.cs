namespace GuitarAlchemistChatbot.Tests.Integration;

using System.Diagnostics;
using GuitarAlchemistChatbot.Plugins;
using GuitarAlchemistChatbot.Services;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

/// <summary>
///     End-to-end integration tests for complete workflows
///     Tests full integration across all plugins and services
///     NOTE: These tests require GaApi to be running at https://localhost:7001
///     Start services with: .\Scripts\start-all.ps1 -Dashboard
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("EndToEnd")]
[Category("RequiresRunningServices")]
public class EndToEndIntegrationTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Skip SSL certificate validation for local testing
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(GaApiBaseUrl)
        };
    }

    [SetUp]
    public void Setup()
    {
        // Create services
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        _gaApiClient = new GaApiClient(
            _httpClient!,
            loggerFactory.CreateLogger<GaApiClient>());

        // Create plugins
        _chordProgressionPlugin = new ChordProgressionPlugin(
            _gaApiClient,
            loggerFactory.CreateLogger<ChordProgressionPlugin>());

        _practicePathPlugin = new PracticePathPlugin(
            _gaApiClient,
            loggerFactory.CreateLogger<PracticePathPlugin>());

        _shapeGraphPlugin = new ShapeGraphPlugin(
            _gaApiClient,
            loggerFactory.CreateLogger<ShapeGraphPlugin>());

        _bspDungeonPlugin = new BSPDungeonPlugin(
            _gaApiClient,
            loggerFactory.CreateLogger<BSPDungeonPlugin>());
    }

    [TearDown]
    public void TearDown()
    {
        // Don't dispose HttpClient here - it's shared across tests
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _httpClient?.Dispose();
    }

    private const string GaApiBaseUrl = "https://localhost:7001";
    private HttpClient? _httpClient;
    private GaApiClient? _gaApiClient;
    private ChordProgressionPlugin? _chordProgressionPlugin;
    private PracticePathPlugin? _practicePathPlugin;
    private ShapeGraphPlugin? _shapeGraphPlugin;
    private BSPDungeonPlugin? _bspDungeonPlugin;

    [Test]
    public async Task CompleteWorkflow_AnalyzeProgressionThenGeneratePracticePath_Success()
    {
        // Arrange
        var pitchClassSets = "0,4,7 | 5,9,0 | 7,11,2 | 0,4,7"; // I-IV-V-I
        var tuning = "E2 A2 D3 G3 B3 E4";

        // Act - Step 1: Analyze progression
        TestContext.WriteLine("Step 1: Analyzing chord progression...");
        var analysisResult = await _chordProgressionPlugin!.AnalyzeProgressionAsync(pitchClassSets);

        Assert.That(analysisResult, Is.Not.Null);
        Assert.That(analysisResult, Does.Contain("Chord Progression Analysis"));
        TestContext.WriteLine(analysisResult);
        TestContext.WriteLine();

        // Act - Step 2: Generate practice path for first chord
        TestContext.WriteLine("Step 2: Generating practice path...");
        var practicePathResult = await _practicePathPlugin!.GeneratePracticePathAsync(
            "0,4,7",
            tuning,
            10);

        Assert.That(practicePathResult, Is.Not.Null);
        Assert.That(practicePathResult, Does.Contain("Optimal Practice Path"));
        TestContext.WriteLine(practicePathResult);
    }

    [Test]
    public async Task CompleteWorkflow_AnalyzeShapeGraphThenGenerateDungeon_Success()
    {
        // Arrange
        var pitchClasses = new[] { 0, 4, 7 }; // C major triad
        var tuning = "E2 A2 D3 G3 B3 E4";

        // Act - Step 1: Analyze shape graph
        TestContext.WriteLine("Step 1: Analyzing shape graph...");
        var shapeGraphResult = await _shapeGraphPlugin!.AnalyzeShapeGraphAsync(
            "0,4,7",
            tuning);

        Assert.That(shapeGraphResult, Is.Not.Null);
        Assert.That(shapeGraphResult, Does.Contain("Shape Graph Analysis"));
        TestContext.WriteLine(shapeGraphResult);
        TestContext.WriteLine();

        // Act - Step 2: Generate intelligent dungeon
        TestContext.WriteLine("Step 2: Generating intelligent dungeon...");
        var dungeonResult = await _bspDungeonPlugin!.GenerateIntelligentDungeonAsync(
            "0,4,7",
            tuning);

        Assert.That(dungeonResult, Is.Not.Null);
        Assert.That(dungeonResult, Does.Contain("Intelligent Musical Dungeon"));
        TestContext.WriteLine(dungeonResult);
    }

    [Test]
    public async Task CompleteWorkflow_JazzProgressionAnalysisAndPractice_Success()
    {
        // Arrange - ii-V-I progression
        var jazzProgression = "2,5,9,0 | 7,11,2,5 | 0,4,7,11";
        var tuning = "E2 A2 D3 G3 B3 E4";

        // Act - Step 1: Analyze jazz progression
        TestContext.WriteLine("Step 1: Analyzing jazz progression...");
        var analysisResult = await _chordProgressionPlugin!.AnalyzeProgressionAsync(jazzProgression);

        Assert.That(analysisResult, Does.Contain("Chord Progression Analysis"));
        TestContext.WriteLine(analysisResult);
        TestContext.WriteLine();

        // Act - Step 2: Generate practice path for Dm9
        TestContext.WriteLine("Step 2: Generating practice path for Dm9...");
        var dm9Path = await _practicePathPlugin!.GeneratePracticePathAsync(
            "2,5,9,0",
            tuning,
            8,
            "MinimizeVoiceLeading");

        Assert.That(dm9Path, Does.Contain("Optimal Practice Path"));
        TestContext.WriteLine(dm9Path);
        TestContext.WriteLine();

        // Act - Step 3: Analyze shape graph for Cmaj7
        TestContext.WriteLine("Step 3: Analyzing shape graph for Cmaj7...");
        var cmaj7Analysis = await _shapeGraphPlugin!.AnalyzeShapeGraphAsync(
            "0,4,7,11",
            tuning);

        Assert.That(cmaj7Analysis, Does.Contain("Shape Graph Analysis"));
        TestContext.WriteLine(cmaj7Analysis);
    }

    [Test]
    public async Task MultiChordWorkflow_AnalyzeMultipleChordsSequentially_Success()
    {
        // Arrange
        var chords = new[]
        {
            ("C major", "0,4,7"),
            ("D minor", "2,5,9"),
            ("G7", "7,11,2,5")
        };
        var tuning = "E2 A2 D3 G3 B3 E4";

        // Act & Assert
        foreach (var (name, pitchClasses) in chords)
        {
            TestContext.WriteLine($"Analyzing {name}...");

            var shapeGraphResult = await _shapeGraphPlugin!.AnalyzeShapeGraphAsync(
                pitchClasses,
                tuning);

            Assert.That(shapeGraphResult, Does.Contain("Shape Graph Analysis"));
            TestContext.WriteLine(shapeGraphResult);
            TestContext.WriteLine();
        }
    }

    [Test]
    public async Task MultiChordWorkflow_GeneratePracticePathsForProgression_Success()
    {
        // Arrange
        var progression = new[]
        {
            ("I - C major", "0,4,7"),
            ("IV - F major", "5,9,0"),
            ("V - G major", "7,11,2"),
            ("I - C major", "0,4,7")
        };
        var tuning = "E2 A2 D3 G3 B3 E4";

        // Act & Assert
        foreach (var (name, pitchClasses) in progression)
        {
            TestContext.WriteLine($"Generating practice path for {name}...");

            var pathResult = await _practicePathPlugin!.GeneratePracticePathAsync(
                pitchClasses,
                tuning,
                5);

            Assert.That(pathResult, Does.Contain("Optimal Practice Path"));
            TestContext.WriteLine(pathResult);
            TestContext.WriteLine();
        }
    }

    [Test]
    public async Task BSPWorkflow_GenerateBasicAndIntelligentDungeons_Success()
    {
        // Arrange
        var pitchClasses = "0,4,7";
        var tuning = "E2 A2 D3 G3 B3 E4";

        // Act - Step 1: Generate basic dungeon
        TestContext.WriteLine("Step 1: Generating basic BSP dungeon...");
        var basicDungeon = await _bspDungeonPlugin!.GenerateDungeonAsync();

        Assert.That(basicDungeon, Does.Contain("BSP Dungeon Generated"));
        TestContext.WriteLine(basicDungeon);
        TestContext.WriteLine();

        // Act - Step 2: Generate intelligent dungeon
        TestContext.WriteLine("Step 2: Generating intelligent musical dungeon...");
        var intelligentDungeon = await _bspDungeonPlugin!.GenerateIntelligentDungeonAsync(
            pitchClasses,
            tuning);

        Assert.That(intelligentDungeon, Does.Contain("Intelligent Musical Dungeon"));
        TestContext.WriteLine(intelligentDungeon);
    }

    [Test]
    public async Task BSPWorkflow_GenerateMultipleDungeonsWithDifferentSeeds_Success()
    {
        // Arrange
        var seeds = new[] { 12345, 67890, 11111 };

        // Act & Assert
        foreach (var seed in seeds)
        {
            TestContext.WriteLine($"Generating dungeon with seed {seed}...");

            var dungeonResult = await _bspDungeonPlugin!.GenerateDungeonAsync(
                80,
                60,
                4,
                seed);

            Assert.That(dungeonResult, Does.Contain("BSP Dungeon Generated"));
            Assert.That(dungeonResult, Does.Contain(seed.ToString()));
            TestContext.WriteLine(dungeonResult);
            TestContext.WriteLine();
        }
    }

    [Test]
    public async Task Performance_AnalyzeMultipleProgressionsInParallel_Success()
    {
        // Arrange
        var progressions = new[]
        {
            "0,4,7 | 5,9,0 | 7,11,2 | 0,4,7",
            "0,3,7 | 5,8,0 | 7,10,2 | 0,3,7",
            "0,4,7,11 | 2,5,9,0 | 7,11,2,5 | 0,4,7,11"
        };

        // Act
        var stopwatch = Stopwatch.StartNew();

        var tasks = progressions.Select(p =>
            _chordProgressionPlugin!.AnalyzeProgressionAsync(p));

        var results = await Task.WhenAll(tasks);

        stopwatch.Stop();

        // Assert
        Assert.That(results.Length, Is.EqualTo(progressions.Length));
        foreach (var result in results)
        {
            Assert.That(result, Does.Contain("Chord Progression Analysis"));
        }

        TestContext.WriteLine($"Analyzed {progressions.Length} progressions in {stopwatch.ElapsedMilliseconds}ms");
        TestContext.WriteLine($"Average: {stopwatch.ElapsedMilliseconds / progressions.Length}ms per progression");
    }

    [Test]
    public async Task ErrorRecovery_InvalidInputThenValidInput_Success()
    {
        // Arrange
        var invalidPitchClasses = "invalid,data";
        var validPitchClasses = "0,4,7";
        var tuning = "E2 A2 D3 G3 B3 E4";

        // Act - Step 1: Try invalid input
        TestContext.WriteLine("Step 1: Trying invalid input...");
        var invalidResult = await _practicePathPlugin!.GeneratePracticePathAsync(
            invalidPitchClasses,
            tuning);

        Assert.That(invalidResult, Does.Contain("Error"));
        TestContext.WriteLine(invalidResult);
        TestContext.WriteLine();

        // Act - Step 2: Try valid input
        TestContext.WriteLine("Step 2: Trying valid input...");
        var validResult = await _practicePathPlugin!.GeneratePracticePathAsync(
            validPitchClasses,
            tuning);

        Assert.That(validResult, Does.Contain("Optimal Practice Path"));
        TestContext.WriteLine(validResult);
    }
}
