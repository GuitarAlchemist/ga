namespace GA.Business.Core.Tests.Invariants;

using Core.Invariants;

[TestFixture]
public class IconicChordInvariantsTests
{
    [SetUp]
    public void SetUp()
    {
        _validChord = new IconicChordDefinition
        {
            Name = "Hendrix Chord",
            TheoreticalName = "E7#9",
            PitchClasses = [4, 8, 11, 2, 3], // E, G#, B, D, F
            GuitarVoicing = [0, 7, 6, 7, 8, 0],
            Artist = "Jimi Hendrix",
            Song = "Purple Haze",
            Genre = "Rock",
            Era = "1960s",
            AlternateNames = ["Purple Haze Chord", "Hendrix Dominant"]
        };
    }

    private IconicChordDefinition _validChord;

    [Test]
    public void NameNotEmptyInvariant_ValidName_ShouldPass()
    {
        // Arrange
        var invariant = new NameNotEmptyInvariant();

        // Act
        var result = invariant.Validate(_validChord);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.InvariantName, Is.EqualTo("NameNotEmpty"));
    }

    [Test]
    public void NameNotEmptyInvariant_EmptyName_ShouldFail()
    {
        // Arrange
        var invariant = new NameNotEmptyInvariant();
        _validChord.Name = "";

        // Act
        var result = invariant.Validate(_validChord);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("cannot be empty"));
        Assert.That(result.Severity, Is.EqualTo(InvariantSeverity.Error));
    }

    [Test]
    public void NameNotEmptyInvariant_WhitespaceName_ShouldFail()
    {
        // Arrange
        var invariant = new NameNotEmptyInvariant();
        _validChord.Name = "   ";

        // Act
        var result = invariant.Validate(_validChord);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.PropertyName, Is.EqualTo(nameof(_validChord.Name)));
    }

    [Test]
    public void TheoreticalNameValidInvariant_ValidName_ShouldPass()
    {
        // Arrange
        var invariant = new TheoreticalNameValidInvariant();

        // Act
        var result = invariant.Validate(_validChord);

        // Assert
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void TheoreticalNameValidInvariant_InvalidRoot_ShouldFail()
    {
        // Arrange
        var invariant = new TheoreticalNameValidInvariant();
        _validChord.TheoreticalName = "X7#9";

        // Act
        var result = invariant.Validate(_validChord);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("valid root note"));
    }

    [Test]
    public void PitchClassesValidInvariant_ValidPitchClasses_ShouldPass()
    {
        // Arrange
        var invariant = new PitchClassesValidInvariant();

        // Act
        var result = invariant.Validate(_validChord);

        // Assert
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void PitchClassesValidInvariant_EmptyPitchClasses_ShouldFail()
    {
        // Arrange
        var invariant = new PitchClassesValidInvariant();
        _validChord.PitchClasses = [];

        // Act
        var result = invariant.Validate(_validChord);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("cannot be null or empty"));
    }

    [Test]
    public void PitchClassesValidInvariant_InvalidPitchClass_ShouldFail()
    {
        // Arrange
        var invariant = new PitchClassesValidInvariant();
        _validChord.PitchClasses = [0, 4, 7, 12]; // 12 is invalid

        // Act
        var result = invariant.Validate(_validChord);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("Invalid pitch classes"));
        Assert.That(result.ErrorMessage, Does.Contain("12"));
    }

    [Test]
    public void PitchClassesValidInvariant_TooFewNotes_ShouldFail()
    {
        // Arrange
        var invariant = new PitchClassesValidInvariant();
        _validChord.PitchClasses = [0]; // Only one note

        // Act
        var result = invariant.Validate(_validChord);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("at least 2 different pitch classes"));
    }

    [Test]
    public void PitchClassesValidInvariant_DuplicatePitchClasses_ShouldFail()
    {
        // Arrange
        var invariant = new PitchClassesValidInvariant();
        _validChord.PitchClasses = [0, 4, 7, 4]; // Duplicate 4

        // Act
        var result = invariant.Validate(_validChord);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("Duplicate pitch classes"));
    }

    [Test]
    public void GuitarVoicingValidInvariant_ValidVoicing_ShouldPass()
    {
        // Arrange
        var invariant = new GuitarVoicingValidInvariant();

        // Act
        var result = invariant.Validate(_validChord);

        // Assert
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void GuitarVoicingValidInvariant_NullVoicing_ShouldPass()
    {
        // Arrange
        var invariant = new GuitarVoicingValidInvariant();
        _validChord.GuitarVoicing = null;

        // Act
        var result = invariant.Validate(_validChord);

        // Assert
        Assert.That(result.IsValid, Is.True); // Optional field
    }

    [Test]
    public void GuitarVoicingValidInvariant_WrongStringCount_ShouldFail()
    {
        // Arrange
        var invariant = new GuitarVoicingValidInvariant();
        _validChord.GuitarVoicing = [0, 2, 2, 1]; // Only 4 strings

        // Act
        var result = invariant.Validate(_validChord);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("exactly 6 fret values"));
    }

    [Test]
    public void GuitarVoicingValidInvariant_InvalidFretNumber_ShouldFail()
    {
        // Arrange
        var invariant = new GuitarVoicingValidInvariant();
        _validChord.GuitarVoicing = [0, 2, 2, 1, 0, 25]; // 25 is too high

        // Act
        var result = invariant.Validate(_validChord);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("Invalid fret numbers"));
    }

    [Test]
    public void GuitarVoicingValidInvariant_TooManyMutedStrings_ShouldFail()
    {
        // Arrange
        var invariant = new GuitarVoicingValidInvariant();
        _validChord.GuitarVoicing = [-1, -1, -1, -1, -1, 0]; // Only 1 string played

        // Act
        var result = invariant.Validate(_validChord);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("At least 2 strings must be played"));
    }

    [Test]
    public void GenreValidInvariant_ValidGenre_ShouldPass()
    {
        // Arrange
        var invariant = new GenreValidInvariant();

        // Act
        var result = invariant.Validate(_validChord);

        // Assert
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void GenreValidInvariant_InvalidGenre_ShouldFail()
    {
        // Arrange
        var invariant = new GenreValidInvariant();
        _validChord.Genre = "InvalidGenre";

        // Act
        var result = invariant.Validate(_validChord);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("not in the recognized list"));
        Assert.That(result.Severity, Is.EqualTo(InvariantSeverity.Warning));
    }

    [Test]
    public void AlternateNamesUniqueInvariant_ValidNames_ShouldPass()
    {
        // Arrange
        var invariant = new AlternateNamesUniqueInvariant();

        // Act
        var result = invariant.Validate(_validChord);

        // Assert
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void AlternateNamesUniqueInvariant_DuplicateNames_ShouldFail()
    {
        // Arrange
        var invariant = new AlternateNamesUniqueInvariant();
        _validChord.AlternateNames = ["Purple Haze Chord", "purple haze chord"]; // Case-insensitive duplicate

        // Act
        var result = invariant.Validate(_validChord);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("Duplicate alternate names"));
    }

    [Test]
    public void AlternateNamesUniqueInvariant_MatchesMainName_ShouldFail()
    {
        // Arrange
        var invariant = new AlternateNamesUniqueInvariant();
        _validChord.AlternateNames = ["Hendrix Chord"]; // Same as main name

        // Act
        var result = invariant.Validate(_validChord);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("should not duplicate the main chord name"));
    }

    [Test]
    public void AllInvariants_ValidChord_ShouldAllPass()
    {
        // Arrange
        var invariants = IconicChordInvariants.GetAll();

        // Act & Assert
        foreach (var invariant in invariants)
        {
            var result = invariant.Validate(_validChord);
            Assert.That(result.IsValid, Is.True, $"Invariant {invariant.InvariantName} failed: {result.ErrorMessage}");
        }
    }

    [Test]
    public void AllInvariants_ShouldHaveUniqueNames()
    {
        // Arrange
        var invariants = IconicChordInvariants.GetAll().ToList();

        // Act
        var invariantNames = invariants.Select(i => i.InvariantName).ToList();
        var uniqueNames = invariantNames.Distinct().ToList();

        // Assert
        Assert.That(uniqueNames.Count, Is.EqualTo(invariantNames.Count),
            "All invariants should have unique names");
    }

    [Test]
    public void AllInvariants_ShouldHaveDescriptions()
    {
        // Arrange
        var invariants = IconicChordInvariants.GetAll();

        // Act & Assert
        foreach (var invariant in invariants)
        {
            Assert.That(string.IsNullOrWhiteSpace(invariant.Description), Is.False,
                $"Invariant {invariant.InvariantName} should have a description");
            Assert.That(string.IsNullOrWhiteSpace(invariant.Category), Is.False,
                $"Invariant {invariant.InvariantName} should have a category");
        }
    }
}
