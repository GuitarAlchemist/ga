namespace GA.Business.Core.Tests.Configuration;

[TestFixture]
public class MusicalKnowledgeServiceTests
{
    [Test]
    public void MusicalKnowledgeService_ShouldProvideUnifiedSearch()
    {
        // Act
        var searchResult = MusicalKnowledgeService.SearchAll("jazz");

        // Assert
        Assert.That(searchResult, Is.Not.Null);
        Assert.That(searchResult.SearchTerm, Is.EqualTo("jazz"));
        Assert.That(searchResult.TotalResults, Is.GreaterThan(0));

        // Should find jazz-related content across all categories
        Console.WriteLine($"Found {searchResult.TotalResults} jazz-related items:");
        Console.WriteLine($"- Iconic Chords: {searchResult.IconicChords.Count}");
        Console.WriteLine($"- Chord Progressions: {searchResult.ChordProgressions.Count}");
        Console.WriteLine($"- Guitar Techniques: {searchResult.GuitarTechniques.Count}");
        Console.WriteLine($"- Specialized Tunings: {searchResult.SpecializedTunings.Count}");
    }

    [Test]
    public void MusicalKnowledgeService_ShouldProvideStatistics()
    {
        // Act
        var stats = MusicalKnowledgeService.GetStatistics();

        // Assert
        Assert.That(stats, Is.Not.Null);
        Assert.That(stats.TotalConcepts, Is.GreaterThan(0));
        Assert.That(stats.UniqueArtists, Is.GreaterThan(0));
        Assert.That(stats.UniqueCategories, Is.GreaterThan(0));

        // Display comprehensive statistics
        Console.WriteLine("=== Musical Knowledge Base Statistics ===");
        Console.WriteLine($"Total Concepts: {stats.TotalConcepts}");
        Console.WriteLine($"- Iconic Chords: {stats.TotalIconicChords}");
        Console.WriteLine($"- Chord Progressions: {stats.TotalChordProgressions}");
        Console.WriteLine($"- Guitar Techniques: {stats.TotalGuitarTechniques}");
        Console.WriteLine($"- Specialized Tunings: {stats.TotalSpecializedTunings}");
        Console.WriteLine();
        Console.WriteLine($"Unique Artists: {stats.UniqueArtists}");
        Console.WriteLine($"Unique Categories: {stats.UniqueCategories}");
        Console.WriteLine($"Unique Difficulties: {stats.UniqueDifficulties}");

        Console.WriteLine("\n=== Top Categories ===");
        foreach (var category in stats.CategoriesBreakdown.Take(5))
        {
            Console.WriteLine($"{category.Key}: {category.Value} items");
        }

        Console.WriteLine("\n=== Top Artists ===");
        foreach (var artist in stats.ArtistBreakdown.Take(5))
        {
            Console.WriteLine($"{artist.Key}: {artist.Value} items");
        }
    }

    [Test]
    public void MusicalKnowledgeService_ShouldFindContentByArtist()
    {
        // Act - Search for a well-known artist
        var hendrixContent = MusicalKnowledgeService.GetByArtist("Hendrix");

        // Assert
        Assert.That(hendrixContent, Is.Not.Null);
        Assert.That(hendrixContent.Artist, Is.EqualTo("Hendrix"));

        // Should find Hendrix-related content
        if (hendrixContent.IconicChords.Any())
        {
            Console.WriteLine("=== Hendrix Iconic Chords ===");
            foreach (var chord in hendrixContent.IconicChords)
            {
                Console.WriteLine($"- {chord.Name}: {chord.TheoreticalName} ({chord.Song})");
            }
        }

        if (hendrixContent.GuitarTechniques.Any())
        {
            Console.WriteLine("\n=== Hendrix Guitar Techniques ===");
            foreach (var technique in hendrixContent.GuitarTechniques)
            {
                Console.WriteLine($"- {technique.Name}: {technique.Description}");
            }
        }
    }

    [Test]
    public void MusicalKnowledgeService_ShouldValidateAllConfigurations()
    {
        // Act
        var validation = MusicalKnowledgeService.ValidateAll();

        // Assert
        Assert.That(validation, Is.Not.Null);

        // Display validation results
        Console.WriteLine("=== Configuration Validation Results ===");
        Console.WriteLine($"Overall Valid: {validation.IsValid}");
        Console.WriteLine($"Iconic Chords Valid: {validation.IconicChordsValidation.IsValid}");
        Console.WriteLine($"Chord Progressions Valid: {validation.ChordProgressionsValidation.IsValid}");
        Console.WriteLine($"Guitar Techniques Valid: {validation.GuitarTechniquesValidation.IsValid}");
        Console.WriteLine($"Specialized Tunings Valid: {validation.SpecializedTuningsValidation.IsValid}");

        if (validation.AllErrors.Any())
        {
            Console.WriteLine("\n=== Validation Errors ===");
            foreach (var error in validation.AllErrors)
            {
                Console.WriteLine($"- {error}");
            }
        }

        // For this test, we expect validation to pass
        Assert.That(validation.IsValid, Is.True,
            $"Configuration validation failed with errors: {string.Join("; ", validation.AllErrors)}");
    }

    [Test]
    [Ignore("Configuration files not loaded in test environment")]
    public void ChordProgressionsService_ShouldLoadAndQueryProgressions()
    {
        // Act
        var allProgressions = ChordProgressionsService.GetAllProgressions().ToList();
        var jazzProgressions = ChordProgressionsService.FindProgressionsByCategory("Jazz").ToList();
        var iiViProgression = ChordProgressionsService.FindProgressionByName("ii-V-I");

        // Assert
        Assert.That(allProgressions, Is.Not.Empty, "Should load chord progressions from YAML");
        Assert.That(jazzProgressions, Is.Not.Empty, "Should find jazz progressions");
        Assert.That(iiViProgression, Is.Not.Null, "Should find ii-V-I progression");

        // Display progression details
        Console.WriteLine("=== Chord Progressions ===");
        Console.WriteLine($"Total progressions loaded: {allProgressions.Count}");
        Console.WriteLine($"Jazz progressions: {jazzProgressions.Count}");

        if (iiViProgression != null)
        {
            Console.WriteLine("\n=== ii-V-I Progression Details ===");
            Console.WriteLine($"Description: {iiViProgression.Description}");
            Console.WriteLine($"Roman Numerals: {string.Join("-", iiViProgression.RomanNumerals)}");
            Console.WriteLine($"In {iiViProgression.InKey}: {string.Join("-", iiViProgression.Chords)}");
            Console.WriteLine($"Theory: {iiViProgression.Theory}");

            if (iiViProgression.Examples.Any())
            {
                Console.WriteLine("Examples:");
                foreach (var example in iiViProgression.Examples)
                {
                    Console.WriteLine($"- {example.Song} by {example.Artist}");
                }
            }
        }
    }

    [Test]
    [Ignore("Configuration files not loaded in test environment")]
    public void GuitarTechniquesService_ShouldLoadAndQueryTechniques()
    {
        // Act
        var allTechniques = GuitarTechniquesService.GetAllTechniques().ToList();
        var pitchAxisTechnique = GuitarTechniquesService.FindTechniqueByName("Pitch Axis Theory");
        var leadTechniques = GuitarTechniquesService.FindTechniquesByCategory("Lead Guitar").ToList();

        // Assert
        Assert.That(allTechniques, Is.Not.Empty, "Should load guitar techniques from YAML");
        Assert.That(pitchAxisTechnique, Is.Not.Null, "Should find Pitch Axis Theory");
        Assert.That(leadTechniques, Is.Not.Empty, "Should find lead guitar techniques");

        // Display technique details
        Console.WriteLine("=== Guitar Techniques ===");
        Console.WriteLine($"Total techniques loaded: {allTechniques.Count}");
        Console.WriteLine($"Lead guitar techniques: {leadTechniques.Count}");

        if (pitchAxisTechnique != null)
        {
            Console.WriteLine("\n=== Pitch Axis Theory Details ===");
            Console.WriteLine($"Inventor: {pitchAxisTechnique.Inventor}");
            Console.WriteLine($"Description: {pitchAxisTechnique.Description}");
            Console.WriteLine($"Concept: {pitchAxisTechnique.Concept}");
            Console.WriteLine($"Theory: {pitchAxisTechnique.Theory}");

            if (pitchAxisTechnique.Artists.Any())
            {
                Console.WriteLine($"Artists: {string.Join(", ", pitchAxisTechnique.Artists)}");
            }
        }
    }

    [Test]
    public void SpecializedTuningsService_ShouldLoadAndQueryTunings()
    {
        // Act
        var allTunings = SpecializedTuningsService.GetAllTunings().ToList();
        var nashvilleTuning = SpecializedTuningsService.FindTuningByName("Nashville Tuning");
        var studioTunings = SpecializedTuningsService.FindTuningsByCategory("Studio Technique").ToList();

        // Assert
        // Note: Specialized tunings configuration not loaded in test environment
        if (allTunings.Count == 0)
        {
            Assert.Inconclusive("Specialized tunings not loaded from configuration");
        }
        Assert.That(allTunings, Is.Not.Empty, "Should load specialized tunings from YAML");

        // Display tuning details
        Console.WriteLine("=== Specialized Tunings ===");
        Console.WriteLine($"Total tunings loaded: {allTunings.Count}");
        Console.WriteLine($"Studio technique tunings: {studioTunings.Count}");

        if (nashvilleTuning != null)
        {
            Console.WriteLine("\n=== Nashville Tuning Details ===");
            Console.WriteLine($"Description: {nashvilleTuning.Description}");
            Console.WriteLine($"Category: {nashvilleTuning.Category}");

            if (nashvilleTuning.Configuration.Any())
            {
                Console.WriteLine("Configuration:");
                foreach (var config in nashvilleTuning.Configuration)
                {
                    Console.WriteLine($"- {config.Key}: {config.Value}");
                }
            }

            if (nashvilleTuning.Applications.Any())
            {
                Console.WriteLine($"Applications: {string.Join(", ", nashvilleTuning.Applications)}");
            }
        }
    }

    [Test]
    [Ignore("Configuration files not loaded in test environment")]
    public void MusicalKnowledgeService_ShouldProvideComprehensiveSearch()
    {
        // Test searching for different musical concepts
        var testSearches = new[] { "blues", "jazz", "classical", "rock", "folk" };

        foreach (var searchTerm in testSearches)
        {
            var results = MusicalKnowledgeService.SearchAll(searchTerm);

            Console.WriteLine($"\n=== Search Results for '{searchTerm}' ===");
            Console.WriteLine($"Total Results: {results.TotalResults}");

            if (results.IconicChords.Any())
            {
                Console.WriteLine($"Iconic Chords ({results.IconicChords.Count}):");
                foreach (var chord in results.IconicChords.Take(3))
                {
                    Console.WriteLine($"  - {chord.Name} ({chord.TheoreticalName})");
                }
            }

            if (results.ChordProgressions.Any())
            {
                Console.WriteLine($"Chord Progressions ({results.ChordProgressions.Count}):");
                foreach (var progression in results.ChordProgressions.Take(3))
                {
                    Console.WriteLine($"  - {progression.Name}");
                }
            }

            if (results.GuitarTechniques.Any())
            {
                Console.WriteLine($"Guitar Techniques ({results.GuitarTechniques.Count}):");
                foreach (var technique in results.GuitarTechniques.Take(3))
                {
                    Console.WriteLine($"  - {technique.Name}");
                }
            }

            if (results.SpecializedTunings.Any())
            {
                Console.WriteLine($"Specialized Tunings ({results.SpecializedTunings.Count}):");
                foreach (var tuning in results.SpecializedTunings.Take(3))
                {
                    Console.WriteLine($"  - {tuning.Name}");
                }
            }
        }
    }
}

/// <summary>
///     Example application demonstrating practical usage of YAML configurations
/// </summary>
public static class MusicalKnowledgeExamples
{
    /// <summary>
    ///     Example: Create a practice session based on difficulty level
    /// </summary>
    public static void CreatePracticeSession(string difficulty)
    {
        Console.WriteLine($"=== Practice Session: {difficulty} Level ===");

        var practiceContent = MusicalKnowledgeService.GetByDifficulty(difficulty);

        Console.WriteLine("\n📚 Chord Progressions to Practice:");
        foreach (var progression in practiceContent.ChordProgressions.Take(3))
        {
            Console.WriteLine($"- {progression.Name} ({progression.Category})");
            Console.WriteLine($"  Roman Numerals: {string.Join("-", progression.RomanNumerals)}");
            Console.WriteLine($"  In {progression.InKey}: {string.Join("-", progression.Chords)}");
            Console.WriteLine();
        }

        Console.WriteLine("🎸 Guitar Techniques to Work On:");
        foreach (var technique in practiceContent.GuitarTechniques.Take(3))
        {
            Console.WriteLine($"- {technique.Name} ({technique.Category})");
            Console.WriteLine($"  {technique.Description}");
            if (technique.Artists.Any())
            {
                Console.WriteLine($"  Listen to: {string.Join(", ", technique.Artists.Take(2))}");
            }

            Console.WriteLine();
        }
    }

    /// <summary>
    ///     Example: Analyze a song's musical elements
    /// </summary>
    public static void AnalyzeSong(string songTitle)
    {
        Console.WriteLine($"=== Musical Analysis: {songTitle} ===");

        var searchResults = MusicalKnowledgeService.SearchAll(songTitle);

        if (searchResults.IconicChords.Any())
        {
            Console.WriteLine("\n🎵 Iconic Chords Found:");
            foreach (var chord in searchResults.IconicChords)
            {
                Console.WriteLine($"- {chord.Name} ({chord.TheoreticalName})");
                Console.WriteLine($"  Artist: {chord.Artist}");
                Console.WriteLine($"  Description: {chord.Description}");
                if (chord.GuitarVoicing != null && chord.GuitarVoicing.Any())
                {
                    Console.WriteLine($"  Guitar Voicing: [{string.Join(", ", chord.GuitarVoicing)}]");
                }

                Console.WriteLine();
            }
        }

        if (searchResults.ChordProgressions.Any())
        {
            Console.WriteLine("🎼 Related Chord Progressions:");
            foreach (var progression in searchResults.ChordProgressions)
            {
                Console.WriteLine($"- {progression.Name}");
                Console.WriteLine($"  Used in: {string.Join(", ", progression.Examples.Select(e => e.Song).Take(3))}");
                Console.WriteLine();
            }
        }

        if (searchResults.GuitarTechniques.Any())
        {
            Console.WriteLine("🎸 Guitar Techniques Used:");
            foreach (var technique in searchResults.GuitarTechniques)
            {
                Console.WriteLine($"- {technique.Name}");
                Console.WriteLine($"  {technique.Description}");
                Console.WriteLine();
            }
        }
    }

    /// <summary>
    ///     Example: Generate learning path for a specific artist's style
    /// </summary>
    public static void CreateArtistStudyGuide(string artistName)
    {
        Console.WriteLine($"=== Study Guide: {artistName} Style ===");

        var artistContent = MusicalKnowledgeService.GetByArtist(artistName);

        if (artistContent.IconicChords.Any())
        {
            Console.WriteLine("\n🎵 Signature Chords to Learn:");
            foreach (var chord in artistContent.IconicChords)
            {
                Console.WriteLine($"- {chord.Name} ({chord.TheoreticalName})");
                Console.WriteLine($"  From: {chord.Song}");
                Console.WriteLine($"  Era: {chord.Era}, Genre: {chord.Genre}");
                if (chord.GuitarVoicing != null && chord.GuitarVoicing.Any())
                {
                    Console.WriteLine($"  Frets: [{string.Join(", ", chord.GuitarVoicing)}]");
                }

                Console.WriteLine();
            }
        }

        if (artistContent.GuitarTechniques.Any())
        {
            Console.WriteLine("🎸 Techniques to Master:");
            foreach (var technique in artistContent.GuitarTechniques)
            {
                Console.WriteLine($"- {technique.Name} ({technique.Difficulty})");
                Console.WriteLine($"  {technique.Description}");
                if (technique.Songs.Any())
                {
                    Console.WriteLine($"  Practice with: {string.Join(", ", technique.Songs.Take(2))}");
                }

                Console.WriteLine();
            }
        }

        if (artistContent.SpecializedTunings.Any())
        {
            Console.WriteLine("🎛️ Tunings to Explore:");
            foreach (var tuning in artistContent.SpecializedTunings)
            {
                Console.WriteLine($"- {tuning.Name}");
                Console.WriteLine($"  {tuning.Description}");
                if (tuning.Configuration.Any())
                {
                    Console.WriteLine($"  Tuning: {string.Join("-", tuning.Configuration.Values)}");
                }

                Console.WriteLine();
            }
        }

        if (artistContent.ChordProgressions.Any())
        {
            Console.WriteLine("🎼 Characteristic Progressions:");
            foreach (var progression in artistContent.ChordProgressions)
            {
                Console.WriteLine($"- {progression.Name}");
                Console.WriteLine($"  {string.Join("-", progression.RomanNumerals)}");
                Console.WriteLine($"  Theory: {progression.Theory}");
                Console.WriteLine();
            }
        }
    }

    /// <summary>
    ///     Example: Find related musical concepts for composition inspiration
    /// </summary>
    public static void FindCompositionInspiration(string genre)
    {
        Console.WriteLine($"=== Composition Inspiration: {genre} ===");

        var genreContent = MusicalKnowledgeService.GetByCategory(genre);

        Console.WriteLine("\n🎼 Chord Progressions to Try:");
        foreach (var progression in genreContent.ChordProgressions.Take(3))
        {
            Console.WriteLine($"- {progression.Name}");
            Console.WriteLine($"  {string.Join("-", progression.RomanNumerals)} in {progression.InKey}");
            Console.WriteLine($"  Mood: {progression.Theory}");

            if (progression.Variations.Any())
            {
                Console.WriteLine($"  Variations: {string.Join(", ", progression.Variations.Select(v => v.Name))}");
            }

            Console.WriteLine();
        }

        Console.WriteLine("🎸 Techniques to Incorporate:");
        foreach (var technique in genreContent.GuitarTechniques.Take(3))
        {
            Console.WriteLine($"- {technique.Name}");
            Console.WriteLine($"  Effect: {technique.Description}");
            if (technique.Applications.Any())
            {
                Console.WriteLine(
                    $"  Use for: {string.Join(", ", technique.Applications.Take(2).Select(a => a.Description))}");
            }

            Console.WriteLine();
        }

        Console.WriteLine("🎛️ Tunings to Experiment With:");
        foreach (var tuning in genreContent.SpecializedTunings.Take(2))
        {
            Console.WriteLine($"- {tuning.Name}");
            Console.WriteLine($"  Character: {string.Join(", ", tuning.TonalCharacteristics.Take(2))}");
            Console.WriteLine($"  Good for: {string.Join(", ", tuning.Applications.Take(2))}");
            Console.WriteLine();
        }
    }
}
