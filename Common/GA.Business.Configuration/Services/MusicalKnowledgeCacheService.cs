using Microsoft.EntityFrameworkCore;
using GA.Business.Core.Configuration;
using GA.Data.EntityFramework;
using System.Security.Cryptography;
using System.Text;

namespace GA.Business.Core.Services;

/// <summary>
/// Service for caching YAML configurations in database for faster access
/// </summary>
public class MusicalKnowledgeCacheService(
    MusicalKnowledgeDbContext context,
    ILogger<MusicalKnowledgeCacheService> logger)
{
    /// <summary>
    /// Synchronize all YAML configurations with database cache
    /// </summary>
    public async Task SynchronizeAllAsync()
    {
        try
        {
            logger.LogInformation("Starting synchronization of all YAML configurations");

            await SynchronizeIconicChordsAsync();
            await SynchronizeChordProgressionsAsync();
            await SynchronizeGuitarTechniquesAsync();
            await SynchronizeSpecializedTuningsAsync();

            logger.LogInformation("Completed synchronization of all YAML configurations");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during YAML configuration synchronization");
            throw;
        }
    }

    /// <summary>
    /// Check if cache needs updating based on file modification times
    /// </summary>
    public async Task<bool> NeedsCacheUpdateAsync(string configurationType, string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                logger.LogWarning("Configuration file not found: {FilePath}", filePath);
                return false;
            }

            var fileInfo = new FileInfo(filePath);
            var fileHash = await ComputeFileHashAsync(filePath);

            var metadata = await context.ConfigurationMetadata
                .FirstOrDefaultAsync(m => m.ConfigurationType == configurationType);

            if (metadata == null)
            {
                // First time caching
                return true;
            }

            return metadata.LastModified < fileInfo.LastWriteTimeUtc || metadata.FileHash != fileHash;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking cache update need for {ConfigurationType}", configurationType);
            return true; // Err on the side of updating
        }
    }

    /// <summary>
    /// Synchronize iconic chords from YAML to database
    /// </summary>
    public async Task SynchronizeIconicChordsAsync()
    {
        const string configurationType = "IconicChords";
        var filePath = GetConfigurationFilePath("IconicChords.yaml");

        if (!await NeedsCacheUpdateAsync(configurationType, filePath))
        {
            logger.LogInformation("Iconic chords cache is up to date");
            return;
        }

        try
        {
            var chords = IconicChordsService.GetAllChords().ToList();

            // Clear existing cached data
            var existingChords = await context.IconicChords.ToListAsync();
            context.IconicChords.RemoveRange(existingChords);

            // Add new cached data
            var cachedChords = chords.Select(chord => new CachedIconicChord
            {
                Name = chord.Name,
                TheoreticalName = chord.TheoreticalName,
                Description = chord.Description,
                Artist = chord.Artist,
                Song = chord.Song,
                PitchClasses = chord.PitchClasses,
                GuitarVoicing = chord.GuitarVoicing,
                AlternateNames = chord.AlternateNames,
                Era = chord.Era,
                Genre = chord.Genre,
                LastUpdated = DateTime.UtcNow
            }).ToList();

            await context.IconicChords.AddRangeAsync(cachedChords);
            await UpdateConfigurationMetadataAsync(configurationType, filePath);
            await context.SaveChangesAsync();

            logger.LogInformation("Synchronized {Count} iconic chords to cache", cachedChords.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error synchronizing iconic chords");
            throw;
        }
    }

    /// <summary>
    /// Synchronize chord progressions from YAML to database
    /// </summary>
    public async Task SynchronizeChordProgressionsAsync()
    {
        const string configurationType = "ChordProgressions";
        var filePath = GetConfigurationFilePath("ChordProgressions.yaml");

        if (!await NeedsCacheUpdateAsync(configurationType, filePath))
        {
            logger.LogInformation("Chord progressions cache is up to date");
            return;
        }

        try
        {
            var progressions = ChordProgressionsService.GetAllProgressions().ToList();

            // Clear existing cached data
            var existingProgressions = await context.ChordProgressions.ToListAsync();
            context.ChordProgressions.RemoveRange(existingProgressions);

            // Add new cached data
            var cachedProgressions = progressions.Select(progression => new CachedChordProgression
            {
                Name = progression.Name,
                Description = progression.Description,
                RomanNumerals = progression.RomanNumerals,
                Category = progression.Category,
                Difficulty = progression.Difficulty,
                Function = progression.Function,
                InKey = progression.InKey,
                Chords = progression.Chords,
                VoiceLeading = progression.VoiceLeading,
                Theory = progression.Theory,
                UsedBy = progression.UsedBy,
                LastUpdated = DateTime.UtcNow
            }).ToList();

            await context.ChordProgressions.AddRangeAsync(cachedProgressions);
            await UpdateConfigurationMetadataAsync(configurationType, filePath);
            await context.SaveChangesAsync();

            logger.LogInformation("Synchronized {Count} chord progressions to cache", cachedProgressions.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error synchronizing chord progressions");
            throw;
        }
    }

    /// <summary>
    /// Synchronize guitar techniques from YAML to database
    /// </summary>
    public async Task SynchronizeGuitarTechniquesAsync()
    {
        const string configurationType = "GuitarTechniques";
        var filePath = GetConfigurationFilePath("GuitarTechniques.yaml");

        if (!await NeedsCacheUpdateAsync(configurationType, filePath))
        {
            logger.LogInformation("Guitar techniques cache is up to date");
            return;
        }

        try
        {
            var techniques = GuitarTechniquesService.GetAllTechniques().ToList();

            // Clear existing cached data
            var existingTechniques = await context.GuitarTechniques.ToListAsync();
            context.GuitarTechniques.RemoveRange(existingTechniques);

            // Add new cached data
            var cachedTechniques = techniques.Select(technique => new CachedGuitarTechnique
            {
                Name = technique.Name,
                Description = technique.Description,
                Category = technique.Category,
                Inventor = technique.Inventor,
                Difficulty = technique.Difficulty,
                Concept = technique.Concept,
                Theory = technique.Theory,
                Technique = technique.Technique,
                Artists = technique.Artists,
                Songs = technique.Songs,
                Benefits = technique.Benefits,
                LastUpdated = DateTime.UtcNow
            }).ToList();

            await context.GuitarTechniques.AddRangeAsync(cachedTechniques);
            await UpdateConfigurationMetadataAsync(configurationType, filePath);
            await context.SaveChangesAsync();

            logger.LogInformation("Synchronized {Count} guitar techniques to cache", cachedTechniques.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error synchronizing guitar techniques");
            throw;
        }
    }

    /// <summary>
    /// Synchronize specialized tunings from YAML to database
    /// </summary>
    public async Task SynchronizeSpecializedTuningsAsync()
    {
        const string configurationType = "SpecializedTunings";
        var filePath = GetConfigurationFilePath("SpecializedTunings.yaml");

        if (!await NeedsCacheUpdateAsync(configurationType, filePath))
        {
            logger.LogInformation("Specialized tunings cache is up to date");
            return;
        }

        try
        {
            var tunings = SpecializedTuningsService.GetAllTunings().ToList();

            // Clear existing cached data
            var existingTunings = await context.SpecializedTunings.ToListAsync();
            context.SpecializedTunings.RemoveRange(existingTunings);

            // Add new cached data
            var cachedTunings = tunings.Select(tuning => new CachedSpecializedTuning
            {
                Name = tuning.Name,
                Description = tuning.Description,
                Category = tuning.Category,
                PitchClasses = tuning.PitchClasses,
                TonalCharacteristics = tuning.TonalCharacteristics,
                Applications = tuning.Applications,
                Artists = tuning.Artists,
                TuningPattern = tuning.TuningPattern,
                Interval = tuning.Interval,
                LastUpdated = DateTime.UtcNow
            }).ToList();

            await context.SpecializedTunings.AddRangeAsync(cachedTunings);
            await UpdateConfigurationMetadataAsync(configurationType, filePath);
            await context.SaveChangesAsync();

            logger.LogInformation("Synchronized {Count} specialized tunings to cache", cachedTunings.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error synchronizing specialized tunings");
            throw;
        }
    }

    /// <summary>
    /// Get cached data with fallback to YAML if cache is empty
    /// </summary>
    public async Task<IEnumerable<CachedIconicChord>> GetCachedIconicChordsAsync()
    {
        var cached = await context.IconicChords.ToListAsync();

        if (!cached.Any())
        {
            logger.LogInformation("Cache empty, synchronizing iconic chords");
            await SynchronizeIconicChordsAsync();
            cached = await context.IconicChords.ToListAsync();
        }

        return cached;
    }

    /// <summary>
    /// Get cached chord progressions with fallback
    /// </summary>
    public async Task<IEnumerable<CachedChordProgression>> GetCachedChordProgressionsAsync()
    {
        var cached = await context.ChordProgressions.ToListAsync();

        if (!cached.Any())
        {
            logger.LogInformation("Cache empty, synchronizing chord progressions");
            await SynchronizeChordProgressionsAsync();
            cached = await context.ChordProgressions.ToListAsync();
        }

        return cached;
    }

    /// <summary>
    /// Get cached guitar techniques with fallback
    /// </summary>
    public async Task<IEnumerable<CachedGuitarTechnique>> GetCachedGuitarTechniquesAsync()
    {
        var cached = await context.GuitarTechniques.ToListAsync();

        if (!cached.Any())
        {
            logger.LogInformation("Cache empty, synchronizing guitar techniques");
            await SynchronizeGuitarTechniquesAsync();
            cached = await context.GuitarTechniques.ToListAsync();
        }

        return cached;
    }

    /// <summary>
    /// Get cached specialized tunings with fallback
    /// </summary>
    public async Task<IEnumerable<CachedSpecializedTuning>> GetCachedSpecializedTuningsAsync()
    {
        var cached = await context.SpecializedTunings.ToListAsync();

        if (!cached.Any())
        {
            logger.LogInformation("Cache empty, synchronizing specialized tunings");
            await SynchronizeSpecializedTuningsAsync();
            cached = await context.SpecializedTunings.ToListAsync();
        }

        return cached;
    }

    private async Task UpdateConfigurationMetadataAsync(string configurationType, string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var fileHash = await ComputeFileHashAsync(filePath);

        var metadata = await context.ConfigurationMetadata
            .FirstOrDefaultAsync(m => m.ConfigurationType == configurationType);

        if (metadata == null)
        {
            metadata = new ConfigurationMetadata
            {
                ConfigurationType = configurationType,
                FilePath = filePath
            };
            await context.ConfigurationMetadata.AddAsync(metadata);
        }

        metadata.LastModified = fileInfo.LastWriteTimeUtc;
        metadata.FileHash = fileHash;
        metadata.LastCached = DateTime.UtcNow;
    }

    private async Task<string> ComputeFileHashAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToBase64String(hash);
    }

    private string GetConfigurationFilePath(string fileName)
    {
        var possiblePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName),
            Path.Combine(Directory.GetCurrentDirectory(), fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "Common", "GA.Business.Config", fileName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Common", "GA.Business.Config", fileName)
        };

        return possiblePaths.FirstOrDefault(File.Exists) ?? possiblePaths[0];
    }
}
