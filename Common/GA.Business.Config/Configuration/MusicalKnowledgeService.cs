namespace GA.Business.Core;

/// <summary>
///     Unified service providing access to all musical knowledge from YAML configurations
/// </summary>
public static class MusicalKnowledgeService
{
    /// <summary>
    ///     Search across all musical concepts for a given term
    /// </summary>
    public static MusicalKnowledgeSearchResult SearchAll(string searchTerm)
    {
        var result = new MusicalKnowledgeSearchResult
        {
            SearchTerm = searchTerm,
            IconicChords = IconicChordsService.GetAllChords()
                .Where(c => ContainsSearchTerm(c, searchTerm))
                .ToList(),
            ChordProgressions = ChordProgressionsService.GetAllProgressions()
                .Where(p => ContainsSearchTerm(p, searchTerm))
                .ToList(),
            GuitarTechniques = GuitarTechniquesService.GetAllTechniques()
                .Where(t => ContainsSearchTerm(t, searchTerm))
                .ToList(),
            SpecializedTunings = SpecializedTuningsService.GetAllTunings()
                .Where(t => ContainsSearchTerm(t, searchTerm))
                .ToList()
        };

        return result;
    }

    /// <summary>
    ///     Get all musical concepts by category
    /// </summary>
    public static MusicalKnowledgeByCategory GetByCategory(string category)
    {
        return new MusicalKnowledgeByCategory
        {
            Category = category,
            IconicChords = IconicChordsService.GetAllChords()
                .Where(c => string.Equals(c.Genre, category, StringComparison.OrdinalIgnoreCase))
                .ToList(),
            ChordProgressions = ChordProgressionsService.FindProgressionsByCategory(category).ToList(),
            GuitarTechniques = GuitarTechniquesService.FindTechniquesByCategory(category).ToList(),
            SpecializedTunings = SpecializedTuningsService.FindTuningsByCategory(category).ToList()
        };
    }

    /// <summary>
    ///     Get all musical concepts by difficulty level
    /// </summary>
    public static MusicalKnowledgeByDifficulty GetByDifficulty(string difficulty)
    {
        return new MusicalKnowledgeByDifficulty
        {
            Difficulty = difficulty,
            ChordProgressions = ChordProgressionsService.FindProgressionsByDifficulty(difficulty).ToList(),
            GuitarTechniques = GuitarTechniquesService.FindTechniquesByDifficulty(difficulty).ToList()
        };
    }

    /// <summary>
    ///     Get all musical concepts associated with an artist
    /// </summary>
    public static MusicalKnowledgeByArtist GetByArtist(string artist)
    {
        return new MusicalKnowledgeByArtist
        {
            Artist = artist,
            IconicChords = IconicChordsService.FindChordsByArtist(artist).ToList(),
            ChordProgressions = ChordProgressionsService.FindProgressionsByArtist(artist).ToList(),
            GuitarTechniques = GuitarTechniquesService.FindTechniquesByArtist(artist).ToList(),
            SpecializedTunings = SpecializedTuningsService.FindTuningsByArtist(artist).ToList()
        };
    }

    /// <summary>
    ///     Get comprehensive statistics about the musical knowledge base
    /// </summary>
    public static MusicalKnowledgeStatistics GetStatistics()
    {
        return new MusicalKnowledgeStatistics
        {
            TotalIconicChords = IconicChordsService.GetAllChords().Count(),
            TotalChordProgressions = ChordProgressionsService.GetAllProgressions().Count(),
            TotalGuitarTechniques = GuitarTechniquesService.GetAllTechniques().Count(),
            TotalSpecializedTunings = SpecializedTuningsService.GetAllTunings().Count(),

            UniqueArtists = GetAllArtists().Count(),
            UniqueCategories = GetAllCategories().Count(),
            UniqueDifficulties = GetAllDifficulties().Count(),

            CategoriesBreakdown = GetCategoriesBreakdown(),
            DifficultyBreakdown = GetDifficultyBreakdown(),
            ArtistBreakdown = GetArtistBreakdown()
        };
    }

    /// <summary>
    ///     Validate all configurations and return comprehensive validation results
    /// </summary>
    public static MusicalKnowledgeValidationResult ValidateAll()
    {
        var iconicChordsValidation = IconicChordsService.ValidateConfiguration();
        var progressionsValidation = ChordProgressionsService.ValidateConfiguration();
        var techniquesValidation = GuitarTechniquesService.ValidateConfiguration();
        var tuningsValidation = SpecializedTuningsService.ValidateConfiguration();

        return new MusicalKnowledgeValidationResult
        {
            IsValid = iconicChordsValidation.IsValid && progressionsValidation.IsValid &&
                      techniquesValidation.IsValid && tuningsValidation.IsValid,
            IconicChordsValidation = iconicChordsValidation,
            ChordProgressionsValidation = progressionsValidation,
            GuitarTechniquesValidation = techniquesValidation,
            SpecializedTuningsValidation = tuningsValidation,
            AllErrors = iconicChordsValidation.Errors
                .Concat(progressionsValidation.Errors)
                .Concat(techniquesValidation.Errors)
                .Concat(tuningsValidation.Errors)
                .ToList()
        };
    }

    /// <summary>
    ///     Get all unique artists across all configurations
    /// </summary>
    public static IEnumerable<string> GetAllArtists()
    {
        return IconicChordsService.GetAllArtists()
            .Concat(ChordProgressionsService.GetAllArtists())
            .Concat(GuitarTechniquesService.GetAllArtists())
            .Concat(SpecializedTuningsService.GetAllArtists())
            .Where(a => !string.IsNullOrEmpty(a))
            .Distinct()
            .OrderBy(a => a);
    }

    /// <summary>
    ///     Get all unique categories across all configurations
    /// </summary>
    public static IEnumerable<string> GetAllCategories()
    {
        return ChordProgressionsService.GetAllCategories()
            .Concat(GuitarTechniquesService.GetAllCategories())
            .Concat(SpecializedTuningsService.GetAllCategories())
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .OrderBy(c => c);
    }

    /// <summary>
    ///     Get all unique difficulty levels across all configurations
    /// </summary>
    public static IEnumerable<string> GetAllDifficulties()
    {
        return ChordProgressionsService.GetAllDifficulties()
            .Concat(GuitarTechniquesService.GetAllDifficulties())
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct()
            .OrderBy(d => d);
    }

    /// <summary>
    ///     Reload all configurations
    /// </summary>
    public static void ReloadAllConfigurations()
    {
        IconicChordsConfigLoader.ReloadConfiguration();
        ChordProgressionsConfigLoader.ReloadConfiguration();
        GuitarTechniquesConfigLoader.ReloadConfiguration();
        SpecializedTuningsConfigLoader.ReloadConfiguration();
    }

    // Private helper methods
    private static bool ContainsSearchTerm(IconicChordDefinition chord, string searchTerm)
    {
        return chord.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
               chord.TheoreticalName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
               chord.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
               chord.Artist.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
               chord.Song.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
               chord.AlternateNames.Any(n => n.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsSearchTerm(ChordProgressionDefinition progression, string searchTerm)
    {
        return progression.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
               progression.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
               progression.Category.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
               progression.Theory.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
               progression.UsedBy.Any(u => u.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
               progression.Examples.Any(e => e.Song.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                             e.Artist.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsSearchTerm(GuitarTechniqueDefinition technique, string searchTerm)
    {
        return technique.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
               technique.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
               technique.Category.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
               technique.Inventor.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
               technique.Theory.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
               technique.Artists.Any(a => a.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
               technique.Songs.Any(s => s.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsSearchTerm(SpecializedTuningDefinition tuning, string searchTerm)
    {
        return tuning.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
               tuning.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
               tuning.Category.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
               tuning.Applications.Any(a => a.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
               tuning.Artists.Any(a => a.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, int> GetCategoriesBreakdown()
    {
        var breakdown = new Dictionary<string, int>();

        foreach (var category in GetAllCategories())
        {
            var count = ChordProgressionsService.FindProgressionsByCategory(category).Count() +
                        GuitarTechniquesService.FindTechniquesByCategory(category).Count() +
                        SpecializedTuningsService.FindTuningsByCategory(category).Count();
            breakdown[category] = count;
        }

        return breakdown;
    }

    private static Dictionary<string, int> GetDifficultyBreakdown()
    {
        var breakdown = new Dictionary<string, int>();

        foreach (var difficulty in GetAllDifficulties())
        {
            var count = ChordProgressionsService.FindProgressionsByDifficulty(difficulty).Count() +
                        GuitarTechniquesService.FindTechniquesByDifficulty(difficulty).Count();
            breakdown[difficulty] = count;
        }

        return breakdown;
    }

    private static Dictionary<string, int> GetArtistBreakdown()
    {
        var breakdown = new Dictionary<string, int>();

        foreach (var artist in GetAllArtists().Take(20)) // Top 20 artists
        {
            var count = IconicChordsService.FindChordsByArtist(artist).Count() +
                        ChordProgressionsService.FindProgressionsByArtist(artist).Count() +
                        GuitarTechniquesService.FindTechniquesByArtist(artist).Count() +
                        SpecializedTuningsService.FindTuningsByArtist(artist).Count();
            breakdown[artist] = count;
        }

        return breakdown.OrderByDescending(kvp => kvp.Value).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
}

/// <summary>
///     Data transfer objects for Musical Knowledge Service
/// </summary>
public class MusicalKnowledgeSearchResult
{
    public string SearchTerm { get; set; } = string.Empty;
    public List<IconicChordDefinition> IconicChords { get; set; } = [];
    public List<ChordProgressionDefinition> ChordProgressions { get; set; } = [];
    public List<GuitarTechniqueDefinition> GuitarTechniques { get; set; } = [];
    public List<SpecializedTuningDefinition> SpecializedTunings { get; set; } = [];

    public int TotalResults => IconicChords.Count + ChordProgressions.Count +
                               GuitarTechniques.Count + SpecializedTunings.Count;
}

public class MusicalKnowledgeByCategory
{
    public string Category { get; set; } = string.Empty;
    public List<IconicChordDefinition> IconicChords { get; set; } = [];
    public List<ChordProgressionDefinition> ChordProgressions { get; set; } = [];
    public List<GuitarTechniqueDefinition> GuitarTechniques { get; set; } = [];
    public List<SpecializedTuningDefinition> SpecializedTunings { get; set; } = [];
}

public class MusicalKnowledgeByDifficulty
{
    public string Difficulty { get; set; } = string.Empty;
    public List<ChordProgressionDefinition> ChordProgressions { get; set; } = [];
    public List<GuitarTechniqueDefinition> GuitarTechniques { get; set; } = [];
}

public class MusicalKnowledgeByArtist
{
    public string Artist { get; set; } = string.Empty;
    public List<IconicChordDefinition> IconicChords { get; set; } = [];
    public List<ChordProgressionDefinition> ChordProgressions { get; set; } = [];
    public List<GuitarTechniqueDefinition> GuitarTechniques { get; set; } = [];
    public List<SpecializedTuningDefinition> SpecializedTunings { get; set; } = [];
}

public class MusicalKnowledgeStatistics
{
    public int TotalIconicChords { get; set; }
    public int TotalChordProgressions { get; set; }
    public int TotalGuitarTechniques { get; set; }
    public int TotalSpecializedTunings { get; set; }

    public int TotalConcepts => TotalIconicChords + TotalChordProgressions +
                                TotalGuitarTechniques + TotalSpecializedTunings;

    public int UniqueArtists { get; set; }
    public int UniqueCategories { get; set; }
    public int UniqueDifficulties { get; set; }

    public Dictionary<string, int> CategoriesBreakdown { get; set; } = [];
    public Dictionary<string, int> DifficultyBreakdown { get; set; } = [];
    public Dictionary<string, int> ArtistBreakdown { get; set; } = [];
}

public class MusicalKnowledgeValidationResult
{
    public bool IsValid { get; set; }
    public (bool IsValid, List<string> Errors) IconicChordsValidation { get; set; }
    public (bool IsValid, List<string> Errors) ChordProgressionsValidation { get; set; }
    public (bool IsValid, List<string> Errors) GuitarTechniquesValidation { get; set; }
    public (bool IsValid, List<string> Errors) SpecializedTuningsValidation { get; set; }
    public List<string> AllErrors { get; set; } = [];
}
