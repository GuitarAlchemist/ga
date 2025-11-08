namespace GA.Data.EntityFramework.Data;

using Microsoft.EntityFrameworkCore;

/// <summary>
///     Entity Framework DbContext for musical knowledge caching
/// </summary>
public class MusicalKnowledgeDbContext(DbContextOptions<MusicalKnowledgeDbContext> options) : DbContext(options)
{
    public DbSet<CachedIconicChord> IconicChords { get; set; }
    public DbSet<CachedChordProgression> ChordProgressions { get; set; }
    public DbSet<CachedGuitarTechnique> GuitarTechniques { get; set; }
    public DbSet<CachedSpecializedTuning> SpecializedTunings { get; set; }
    public DbSet<ConfigurationMetadata> ConfigurationMetadata { get; set; }
    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<UserPreference> UserPreferences { get; set; }
    public DbSet<LearningPath> LearningPaths { get; set; }
    public DbSet<LearningPathItem> LearningPathItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure CachedIconicChord
        modelBuilder.Entity<CachedIconicChord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Artist);
            entity.HasIndex(e => e.Genre);
            entity.HasIndex(e => e.Era);
            entity.Property(e => e.PitchClasses).HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList()
            );
            entity.Property(e => e.GuitarVoicing).HasConversion(
                v => v != null ? string.Join(',', v) : null,
                v => !string.IsNullOrEmpty(v)
                    ? v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList()
                    : null
            );
            entity.Property(e => e.AlternateNames).HasConversion(
                v => string.Join('|', v),
                v => v.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()
            );
        });

        // Configure CachedChordProgression
        modelBuilder.Entity<CachedChordProgression>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.Difficulty);
            entity.HasIndex(e => e.InKey);
            entity.Property(e => e.RomanNumerals).HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
            );
            entity.Property(e => e.Chords).HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
            );
            entity.Property(e => e.Function).HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
            );
            entity.Property(e => e.UsedBy).HasConversion(
                v => string.Join('|', v),
                v => v.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()
            );
        });

        // Configure CachedGuitarTechnique
        modelBuilder.Entity<CachedGuitarTechnique>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.Difficulty);
            entity.HasIndex(e => e.Inventor);
            entity.Property(e => e.Artists).HasConversion(
                v => string.Join('|', v),
                v => v.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()
            );
            entity.Property(e => e.Songs).HasConversion(
                v => string.Join('|', v),
                v => v.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()
            );
            entity.Property(e => e.Benefits).HasConversion(
                v => string.Join('|', v),
                v => v.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()
            );
        });

        // Configure CachedSpecializedTuning
        modelBuilder.Entity<CachedSpecializedTuning>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Category);
            entity.Property(e => e.PitchClasses).HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList()
            );
            entity.Property(e => e.Applications).HasConversion(
                v => string.Join('|', v),
                v => v.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()
            );
            entity.Property(e => e.Artists).HasConversion(
                v => string.Join('|', v),
                v => v.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()
            );
            entity.Property(e => e.TonalCharacteristics).HasConversion(
                v => string.Join('|', v),
                v => v.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()
            );
        });

        // Configure UserProfile
        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasMany(e => e.Preferences)
                .WithOne(e => e.UserProfile)
                .HasForeignKey(e => e.UserProfileId);
            entity.HasMany(e => e.LearningPaths)
                .WithOne(e => e.UserProfile)
                .HasForeignKey(e => e.UserProfileId);
        });

        // Configure UserPreference
        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserProfileId, e.PreferenceKey }).IsUnique();
        });

        // Configure LearningPath
        modelBuilder.Entity<LearningPath>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserProfileId);
            entity.HasMany(e => e.Items)
                .WithOne(e => e.LearningPath)
                .HasForeignKey(e => e.LearningPathId);
        });

        // Configure LearningPathItem
        modelBuilder.Entity<LearningPathItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.LearningPathId);
            entity.HasIndex(e => new { e.LearningPathId, e.OrderIndex });
        });

        // Configure ConfigurationMetadata
        modelBuilder.Entity<ConfigurationMetadata>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ConfigurationType).IsUnique();
        });
    }
}

/// <summary>
///     Cached iconic chord entity
/// </summary>
public class CachedIconicChord
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TheoreticalName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Song { get; set; } = string.Empty;
    public List<int> PitchClasses { get; set; } = [];
    public List<int>? GuitarVoicing { get; set; }
    public List<string> AlternateNames { get; set; } = [];
    public string Era { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}

/// <summary>
///     Cached chord progression entity
/// </summary>
public class CachedChordProgression
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> RomanNumerals { get; set; } = [];
    public string Category { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public List<string> Function { get; set; } = [];
    public string InKey { get; set; } = string.Empty;
    public List<string> Chords { get; set; } = [];
    public string VoiceLeading { get; set; } = string.Empty;
    public string Theory { get; set; } = string.Empty;
    public List<string> UsedBy { get; set; } = [];
    public DateTime LastUpdated { get; set; }
}

/// <summary>
///     Cached guitar technique entity
/// </summary>
public class CachedGuitarTechnique
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Inventor { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string Concept { get; set; } = string.Empty;
    public string Theory { get; set; } = string.Empty;
    public string Technique { get; set; } = string.Empty;
    public List<string> Artists { get; set; } = [];
    public List<string> Songs { get; set; } = [];
    public List<string> Benefits { get; set; } = [];
    public DateTime LastUpdated { get; set; }
}

/// <summary>
///     Cached specialized tuning entity
/// </summary>
public class CachedSpecializedTuning
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<int> PitchClasses { get; set; } = [];
    public List<string> TonalCharacteristics { get; set; } = [];
    public List<string> Applications { get; set; } = [];
    public List<string> Artists { get; set; } = [];
    public string TuningPattern { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}

/// <summary>
///     Configuration metadata for tracking YAML file changes
/// </summary>
public class ConfigurationMetadata
{
    public int Id { get; set; }
    public string ConfigurationType { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public string FileHash { get; set; } = string.Empty;
    public DateTime LastCached { get; set; }
}

/// <summary>
///     User profile for personalization
/// </summary>
public class UserProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SkillLevel { get; set; } = string.Empty;
    public List<string> PreferredGenres { get; set; } = [];
    public List<string> Instruments { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime LastActiveAt { get; set; }

    public virtual ICollection<UserPreference> Preferences { get; set; } = [];
    public virtual ICollection<LearningPath> LearningPaths { get; set; } = [];
}

/// <summary>
///     User preferences for customization
/// </summary>
public class UserPreference
{
    public int Id { get; set; }
    public int UserProfileId { get; set; }
    public string PreferenceKey { get; set; } = string.Empty;
    public string PreferenceValue { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }

    public virtual UserProfile UserProfile { get; set; } = null!;
}

/// <summary>
///     Learning path for structured education
/// </summary>
public class LearningPath
{
    public int Id { get; set; }
    public int UserProfileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public virtual UserProfile UserProfile { get; set; } = null!;
    public virtual ICollection<LearningPathItem> Items { get; set; } = [];
}

/// <summary>
///     Individual items in a learning path
/// </summary>
public class LearningPathItem
{
    public int Id { get; set; }
    public int LearningPathId { get; set; }
    public int OrderIndex { get; set; }

    public string ItemType { get; set; } =
        string.Empty; // IconicChord, ChordProgression, GuitarTechnique, SpecializedTuning

    public string ItemName { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }

    public virtual LearningPath LearningPath { get; set; } = null!;
}
