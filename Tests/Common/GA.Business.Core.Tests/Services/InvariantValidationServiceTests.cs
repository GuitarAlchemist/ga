namespace GA.Business.Core.Tests.Services;

using Core.Invariants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
// using GA.Business.Core.Services; // Namespace does not exist

[TestFixture]
public class InvariantValidationServiceTests
{
    [SetUp]
    public void SetUp()
    {
        _mockLogger = new Mock<ILogger<InvariantValidationService>>();
        _mockConfigLoader = new Mock<InvariantConfigurationLoader>(Mock.Of<ILogger<InvariantConfigurationLoader>>());
        _mockFactory = new Mock<ConfigurableInvariantFactory>();
        _mockSettings = new Mock<IOptions<InvariantValidationSettings>>();
        _mockSettings.Setup(s => s.Value).Returns(new InvariantValidationSettings());

        _validationService = new InvariantValidationService(
            _mockLogger.Object,
            _mockConfigLoader.Object,
            _mockFactory.Object,
            _mockSettings.Object);
    }

    private InvariantValidationService _validationService;
    private Mock<ILogger<InvariantValidationService>> _mockLogger;
    private Mock<InvariantConfigurationLoader> _mockConfigLoader;
    private Mock<ConfigurableInvariantFactory> _mockFactory;
    private Mock<IOptions<InvariantValidationSettings>> _mockSettings;

    [Test]
    public void ValidateIconicChord_ValidChord_ShouldReturnSuccess()
    {
        // Arrange
        var validChord = new IconicChordDefinition
        {
            Name = "C Major",
            TheoreticalName = "Cmaj",
            PitchClasses = [0, 4, 7], // C, E, G
            GuitarVoicing = [-1, 3, 2, 0, 1, 0],
            Artist = "Various",
            Song = "Many Songs",
            Genre = "Classical",
            Era = "Classical Period"
        };

        // Act
        var result = _validationService.ValidateIconicChord(validChord);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Results.Count, Is.GreaterThan(0));
        Assert.That(result.Successes.Count(), Is.EqualTo(result.Results.Count));
    }

    [Test]
    public void ValidateIconicChord_InvalidChord_ShouldReturnFailures()
    {
        // Arrange
        var invalidChord = new IconicChordDefinition
        {
            Name = "", // Invalid: empty name
            TheoreticalName = "Cmaj",
            PitchClasses = [0], // Invalid: too few notes
            GuitarVoicing = [0, 1, 2], // Invalid: wrong number of strings
            Artist = "", // Invalid: empty artist
            Song = "", // Invalid: empty song
            Genre = "InvalidGenre", // Invalid: unrecognized genre
            Era = "1960s"
        };

        // Act
        var result = _validationService.ValidateIconicChord(invalidChord);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Failures.Count(), Is.GreaterThan(0));
        Assert.That(result.HasErrors, Is.True);
    }

    [Test]
    public void ValidateChordProgression_ValidProgression_ShouldReturnSuccess()
    {
        // Arrange
        var validProgression = new ChordProgressionDefinition
        {
            Name = "ii-V-I",
            RomanNumerals = ["ii", "V", "I"],
            Category = "Jazz",
            Difficulty = "Intermediate",
            Function = ["Predominant", "Dominant", "Tonic"],
            InKey = "C major",
            Chords = ["Dm", "G", "C"],
            UsedBy = ["Many Jazz Standards"]
        };

        // Act
        var result = _validationService.ValidateChordProgression(validProgression);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Results.Count, Is.GreaterThan(0));
    }

    [Test]
    public void ValidateChordProgression_InvalidProgression_ShouldReturnFailures()
    {
        // Arrange
        var invalidProgression = new ChordProgressionDefinition
        {
            Name = "", // Invalid: empty name
            RomanNumerals = ["XX", "YY"], // Invalid: unrecognized Roman numerals
            Category = "InvalidCategory", // Invalid: unrecognized category
            Difficulty = "InvalidDifficulty", // Invalid: unrecognized difficulty
            Function = [], // Invalid: empty function
            InKey = "InvalidKey", // Invalid: unrecognized key
            Chords = [], // Invalid: empty chords
            UsedBy = [] // Invalid: empty usage examples
        };

        // Act
        var result = _validationService.ValidateChordProgression(invalidProgression);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Failures.Count(), Is.GreaterThan(0));
    }

    [Test]
    public void ValidateGuitarTechnique_ValidTechnique_ShouldReturnSuccess()
    {
        // Arrange
        var validTechnique = new GuitarTechniqueDefinition
        {
            Name = "Alternate Picking",
            Category = "Picking",
            Difficulty = "Intermediate",
            Description = "A fundamental picking technique that alternates between downstrokes and upstrokes",
            Concept = "Efficient picking motion for speed and accuracy",
            Theory = "Based on mechanical efficiency and string attack consistency",
            Technique = "Start with slow, deliberate motions. Focus on consistent pick angle and depth",
            Artists = ["Paul Gilbert", "John Petrucci"],
            Songs = ["Technical Difficulties", "Glasgow Kiss"],
            Benefits = ["Increased speed", "Better accuracy", "Reduced fatigue"],
            Inventor = "Traditional"
        };

        // Act
        var result = _validationService.ValidateGuitarTechnique(validTechnique);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Results.Count, Is.GreaterThan(0));
    }

    [Test]
    public void ValidateSpecializedTuning_ValidTuning_ShouldReturnSuccess()
    {
        // Arrange
        var validTuning = new SpecializedTuningDefinition
        {
            Name = "Drop D",
            Category = "Drop Tunings",
            PitchClasses = [2, 9, 2, 7, 11, 4], // D, A, D, G, B, E
            TuningPattern = "D-A-D-G-B-E",
            Interval = "Perfect fourth intervals with dropped sixth string",
            Description = "A popular alternate tuning that lowers the sixth string by a whole step",
            TonalCharacteristics = ["Darker tone", "Easier power chords", "Open string resonance"],
            Applications = ["Rock music", "Metal", "Power chord progressions"],
            Artists = ["Foo Fighters", "Soundgarden", "Tool"]
        };

        // Act
        var result = _validationService.ValidateSpecializedTuning(validTuning);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Results.Count, Is.GreaterThan(0));
    }

    [Test]
    public void ValidateConcept_UnknownConceptType_ShouldThrowException()
    {
        // Arrange
        var conceptName = "Test Concept";
        var conceptType = "UnknownType";

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _validationService.ValidateConcept(conceptName, conceptType));
    }

    [Test]
    public void ValidateConcept_NonExistentConcept_ShouldReturnFailure()
    {
        // Arrange
        var conceptName = "NonExistentChord";
        var conceptType = "IconicChord";

        // Act
        var result = _validationService.ValidateConcept(conceptName, conceptType);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Failures.Any(f => f.InvariantName == "ConceptExists"), Is.True);
    }

    [Test]
    public async Task ValidateAllAsync_ShouldValidateAllConceptTypes()
    {
        // Act
        var result = await _validationService.ValidateAllAsync();

        // Assert
        Assert.That(result.IsCompleted, Is.True);
        Assert.That(result.CompletedAt, Is.Not.Null);
        Assert.That(result.Duration, Is.GreaterThan(TimeSpan.Zero));

        // Should have results for all concept types
        Assert.That(result.IconicChordResults.Count, Is.GreaterThanOrEqualTo(0));
        Assert.That(result.ChordProgressionResults.Count, Is.GreaterThanOrEqualTo(0));
        Assert.That(result.GuitarTechniqueResults.Count, Is.GreaterThanOrEqualTo(0));
        Assert.That(result.SpecializedTuningResults.Count, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public async Task GetValidationStatisticsAsync_ShouldReturnStatistics()
    {
        // Act
        var statistics = await _validationService.GetValidationStatisticsAsync();

        // Assert
        Assert.That(statistics.GeneratedAt, Is.Not.EqualTo(default(DateTime)));
        Assert.That(statistics.ConceptStatistics.Count, Is.EqualTo(4)); // Four concept types
        Assert.That(statistics.ConceptStatistics.ContainsKey("IconicChords"), Is.True);
        Assert.That(statistics.ConceptStatistics.ContainsKey("ChordProgressions"), Is.True);
        Assert.That(statistics.ConceptStatistics.ContainsKey("GuitarTechniques"), Is.True);
        Assert.That(statistics.ConceptStatistics.ContainsKey("SpecializedTunings"), Is.True);
    }

    [Test]
    public void CompositeInvariantValidationResult_GetSummary_ShouldReturnCorrectSummary()
    {
        // Arrange
        var results = new List<InvariantValidationResult>
        {
            new() { IsValid = true, InvariantName = "Test1", Severity = InvariantSeverity.Info },
            new() { IsValid = false, InvariantName = "Test2", Severity = InvariantSeverity.Warning },
            new() { IsValid = false, InvariantName = "Test3", Severity = InvariantSeverity.Error },
            new() { IsValid = false, InvariantName = "Test4", Severity = InvariantSeverity.Critical }
        };

        var composite = new CompositeInvariantValidationResult { Results = results };

        // Act
        var summary = composite.GetSummary();

        // Assert
        Assert.That(summary.TotalInvariants, Is.EqualTo(4));
        Assert.That(summary.PassedInvariants, Is.EqualTo(1));
        Assert.That(summary.FailedInvariants, Is.EqualTo(3));
        Assert.That(summary.Warnings, Is.EqualTo(1));
        Assert.That(summary.Errors, Is.EqualTo(1));
        Assert.That(summary.CriticalFailures, Is.EqualTo(1));
        Assert.That(summary.IsValid, Is.False);
        Assert.That(summary.SuccessRate, Is.EqualTo(0.25));
    }

    [Test]
    public void CompositeInvariantValidationResult_Properties_ShouldWorkCorrectly()
    {
        // Arrange
        var results = new List<InvariantValidationResult>
        {
            new() { IsValid = true, InvariantName = "Test1", Severity = InvariantSeverity.Info },
            new() { IsValid = false, InvariantName = "Test2", Severity = InvariantSeverity.Critical },
            new() { IsValid = false, InvariantName = "Test3", Severity = InvariantSeverity.Error }
        };

        var composite = new CompositeInvariantValidationResult { Results = results };

        // Act & Assert
        Assert.That(composite.IsValid, Is.False);
        Assert.That(composite.HasCriticalFailures, Is.True);
        Assert.That(composite.HasErrors, Is.True);
        Assert.That(composite.HasWarnings, Is.False);
        Assert.That(composite.Failures.Count(), Is.EqualTo(2));
        Assert.That(composite.Successes.Count(), Is.EqualTo(1));
    }

    [Test]
    public void CompositeInvariantValidationResult_GetFailuresBySeverity_ShouldFilterCorrectly()
    {
        // Arrange
        var results = new List<InvariantValidationResult>
        {
            new() { IsValid = false, InvariantName = "Test1", Severity = InvariantSeverity.Warning },
            new() { IsValid = false, InvariantName = "Test2", Severity = InvariantSeverity.Error },
            new() { IsValid = false, InvariantName = "Test3", Severity = InvariantSeverity.Critical }
        };

        var composite = new CompositeInvariantValidationResult { Results = results };

        // Act
        var criticalFailures = composite.GetFailuresBySeverity(InvariantSeverity.Critical).ToList();
        var errorFailures = composite.GetFailuresBySeverity(InvariantSeverity.Error).ToList();

        // Assert
        Assert.That(criticalFailures.Count, Is.EqualTo(1));
        Assert.That(criticalFailures[0].InvariantName, Is.EqualTo("Test3"));
        Assert.That(errorFailures.Count, Is.EqualTo(1));
        Assert.That(errorFailures[0].InvariantName, Is.EqualTo("Test2"));
    }

    [Test]
    public void InvariantValidationResult_AllErrorMessages_ShouldCombineMessages()
    {
        // Arrange
        var result = new InvariantValidationResult
        {
            ErrorMessage = "Primary error",
            ErrorMessages = ["Secondary error 1", "Secondary error 2"]
        };

        // Act
        var allMessages = result.AllErrorMessages.ToList();

        // Assert
        Assert.That(allMessages.Count, Is.EqualTo(3));
        Assert.That(allMessages[0], Is.EqualTo("Primary error"));
        Assert.That(allMessages[1], Is.EqualTo("Secondary error 1"));
        Assert.That(allMessages[2], Is.EqualTo("Secondary error 2"));
    }
}
