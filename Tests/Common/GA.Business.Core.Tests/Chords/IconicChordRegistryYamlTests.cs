namespace GA.Business.Core.Tests.Chords;

[TestFixture]
public class IconicChordRegistryYamlTests
{
    [Test]
    public void IconicChordRegistry_ShouldLoadFromYamlConfiguration()
    {
        // Act
        var allChords = IconicChordsService.GetAllChords().ToList();

        // Assert
        Assert.That(allChords, Is.Not.Empty, "Should load chords from YAML configuration");
        Assert.That(allChords.Count, Is.GreaterThan(0), "Should have at least one chord loaded");
    }

    [Test]
    public void IconicChordRegistry_ShouldFindHendrixChord()
    {
        // Act
        var hendrixChord = IconicChordRegistry.FindByName("Hendrix Chord");

        // Assert
        Assert.That(hendrixChord, Is.Not.Null, "Should find Hendrix Chord");
        Assert.That(hendrixChord!.IconicName, Is.EqualTo("Hendrix Chord"));
        Assert.That(hendrixChord.TheoreticalName, Is.EqualTo("E7#9"));
        Assert.That(hendrixChord.Artist, Is.EqualTo("Jimi Hendrix"));
        Assert.That(hendrixChord.Song, Is.EqualTo("Purple Haze"));
    }

    [Test]
    public void IconicChordRegistry_ShouldFindJamesBondChord()
    {
        // Act
        var bondChord = IconicChordRegistry.FindByName("James Bond Chord");

        // Assert
        Assert.That(bondChord, Is.Not.Null, "Should find James Bond Chord");
        Assert.That(bondChord!.IconicName, Is.EqualTo("James Bond Chord"));
        Assert.That(bondChord.TheoreticalName, Is.EqualTo("Em(maj7)"));
        Assert.That(bondChord.Artist, Is.EqualTo("John Barry"));
    }

    [Test]
    public void IconicChordRegistry_ShouldFindChordsByAlternateName()
    {
        // Act
        var purpleHazeChord = IconicChordRegistry.FindByName("Purple Haze Chord");
        var e7Sharp9Chord = IconicChordRegistry.FindByName("E7#9");

        // Assert
        Assert.That(purpleHazeChord, Is.Not.Null, "Should find chord by alternate name 'Purple Haze Chord'");
        Assert.That(e7Sharp9Chord, Is.Not.Null, "Should find chord by alternate name 'E7#9'");
        Assert.That(purpleHazeChord!.IconicName, Is.EqualTo("Hendrix Chord"));
        Assert.That(e7Sharp9Chord!.IconicName, Is.EqualTo("Hendrix Chord"));
    }

    [Test]
    public void IconicChordRegistry_ShouldFindChordsByArtist()
    {
        // Act
        var hendrixChords = IconicChordsService.FindChordsByArtist("Jimi Hendrix").ToList();

        // Assert
        Assert.That(hendrixChords, Is.Not.Empty, "Should find chords by Jimi Hendrix");
        Assert.That(hendrixChords.Any(c => c.Name == "Hendrix Chord"), Is.True);
    }

    [Test]
    public void IconicChordRegistry_ShouldFindChordsByEra()
    {
        // Act
        var sixties = IconicChordsService.FindChordsByEra("1960s").ToList();

        // Assert
        Assert.That(sixties, Is.Not.Empty, "Should find chords from 1960s");
        Assert.That(sixties.Any(c => c.Name == "Hendrix Chord"), Is.True);
        Assert.That(sixties.Any(c => c.Name == "James Bond Chord"), Is.True);
    }

    [Test]
    public void IconicChordRegistry_ShouldFindChordsByGenre()
    {
        // Act
        var rockBlues = IconicChordsService.FindChordsByGenre("Rock/Blues").ToList();

        // Assert
        Assert.That(rockBlues, Is.Not.Empty, "Should find Rock/Blues chords");
        Assert.That(rockBlues.Any(c => c.Name == "Hendrix Chord"), Is.True);
    }

    [Test]
    public void IconicChordRegistry_ShouldFindChordsByPitchClassSet()
    {
        // Arrange - E7#9 pitch classes: E, G#, B, D, G (4, 8, 11, 2, 7)
        var hendrixPitchClasses = new[] { 4, 8, 11, 2, 7 };

        // Act
        var matchingChords = IconicChordsService.FindChordsByPitchClasses(hendrixPitchClasses).ToList();

        // Assert
        Assert.That(matchingChords, Is.Not.Empty, "Should find chords by pitch class set");
        Assert.That(matchingChords.Any(c => c.Name == "Hendrix Chord"), Is.True);
    }

    [Test]
    public void IconicChordRegistry_ShouldFindChordsByGuitarVoicing()
    {
        // Arrange - Hendrix chord voicing: [0, 7, 6, 7, 8, 0]
        var hendrixVoicing = new[] { 0, 7, 6, 7, 8, 0 };

        // Act
        var matchingChords = IconicChordsService.FindChordsByGuitarVoicing(hendrixVoicing).ToList();

        // Assert
        Assert.That(matchingChords, Is.Not.Empty, "Should find chords by guitar voicing");
        Assert.That(matchingChords.Any(c => c.Name == "Hendrix Chord"), Is.True);
    }

    [Test]
    public void IconicChordsService_ShouldValidateConfiguration()
    {
        // Act
        var (isValid, errors) = IconicChordRegistry.ValidateConfiguration();

        // Assert
        Assert.That(isValid, Is.True, $"Configuration should be valid. Errors: {string.Join("; ", errors)}");
        Assert.That(errors, Is.Empty, "Should have no validation errors");
    }

    [Test]
    public void IconicChordsService_ShouldProvideAllChordNames()
    {
        // Act
        var chordNames = IconicChordsService.GetAllChordNames().ToList();

        // Assert
        Assert.That(chordNames, Is.Not.Empty, "Should provide chord names");
        Assert.That(chordNames, Contains.Item("Hendrix Chord"));
        Assert.That(chordNames, Contains.Item("James Bond Chord"));
    }

    [Test]
    public void IconicChordsService_ShouldProvideAllArtists()
    {
        // Act
        var artists = IconicChordsService.GetAllArtists().ToList();

        // Assert
        Assert.That(artists, Is.Not.Empty, "Should provide artists");
        Assert.That(artists, Contains.Item("Jimi Hendrix"));
        Assert.That(artists, Contains.Item("John Barry"));
    }

    [Test]
    public void IconicChordsService_ShouldProvideAllEras()
    {
        // Act
        var eras = IconicChordsService.GetAllEras().ToList();

        // Assert
        Assert.That(eras, Is.Not.Empty, "Should provide eras");
        Assert.That(eras, Contains.Item("1960s"));
    }

    [Test]
    public void IconicChordsService_ShouldProvideAllGenres()
    {
        // Act
        var genres = IconicChordsService.GetAllGenres().ToList();

        // Assert
        Assert.That(genres, Is.Not.Empty, "Should provide genres");
        Assert.That(genres, Contains.Item("Rock/Blues"));
        Assert.That(genres, Contains.Item("Film Score"));
    }
}
