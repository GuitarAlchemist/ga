using Microsoft.EntityFrameworkCore;
using GA.Data.EntityFramework;
using GA.Business.Core.Configuration;

namespace GA.Business.Core.Services;

/// <summary>
/// Service for managing user profiles, preferences, and personalized learning paths
/// </summary>
public class UserPersonalizationService(
    MusicalKnowledgeDbContext context,
    ILogger<UserPersonalizationService> logger,
    MusicalAnalyticsService analyticsService)
{
    /// <summary>
    /// Create or update user profile
    /// </summary>
    public async Task<UserProfile> CreateOrUpdateUserProfileAsync(string userId, UserProfileRequest request)
    {
        logger.LogInformation("Creating/updating user profile for {UserId}", userId);

        try
        {
            var profile = await context.UserProfiles
                .Include(p => p.Preferences)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                profile = new UserProfile
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                await context.UserProfiles.AddAsync(profile);
            }

            // Update profile properties
            profile.Email = request.Email;
            profile.DisplayName = request.DisplayName;
            profile.SkillLevel = request.SkillLevel;
            profile.PreferredGenres = request.PreferredGenres;
            profile.Instruments = request.Instruments;
            profile.LastActiveAt = DateTime.UtcNow;

            // Update preferences
            await UpdateUserPreferencesAsync(profile, request.Preferences);

            await context.SaveChangesAsync();

            logger.LogInformation("Successfully updated profile for user {UserId}", userId);
            return profile;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating/updating user profile for {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Get user profile with preferences and learning paths
    /// </summary>
    public async Task<UserProfile?> GetUserProfileAsync(string userId)
    {
        try
        {
            return await context.UserProfiles
                .Include(p => p.Preferences)
                .Include(p => p.LearningPaths)
                    .ThenInclude(lp => lp.Items)
                .FirstOrDefaultAsync(p => p.UserId == userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving user profile for {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Create personalized learning path for user
    /// </summary>
    public async Task<LearningPath> CreateLearningPathAsync(string userId, LearningPathRequest request)
    {
        logger.LogInformation("Creating learning path '{Name}' for user {UserId}", request.Name, userId);

        try
        {
            var profile = await GetUserProfileAsync(userId);
            if (profile == null)
            {
                throw new ArgumentException($"User profile not found for {userId}");
            }

            var learningPath = new LearningPath
            {
                UserProfileId = profile.Id,
                Name = request.Name,
                Description = request.Description,
                Difficulty = request.Difficulty,
                Category = request.Category,
                CreatedAt = DateTime.UtcNow
            };

            await context.LearningPaths.AddAsync(learningPath);
            await context.SaveChangesAsync();

            // Add learning path items
            if (request.Items.Any())
            {
                await AddLearningPathItemsAsync(learningPath.Id, request.Items);
            }
            else
            {
                // Generate items based on user preferences and difficulty
                await GenerateLearningPathItemsAsync(learningPath, profile);
            }

            logger.LogInformation("Successfully created learning path {LearningPathId} for user {UserId}",
                                 learningPath.Id, userId);
            return learningPath;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating learning path for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Get personalized recommendations for user
    /// </summary>
    public async Task<PersonalizedRecommendations> GetPersonalizedRecommendationsAsync(string userId)
    {
        logger.LogInformation("Generating personalized recommendations for user {UserId}", userId);

        try
        {
            var profile = await GetUserProfileAsync(userId);
            if (profile == null)
            {
                throw new ArgumentException($"User profile not found for {userId}");
            }

            var recommendations = await analyticsService.GenerateRecommendationsAsync(profile);

            // Enhance recommendations based on user's learning history
            await EnhanceRecommendationsWithHistory(recommendations, profile);

            logger.LogInformation("Generated {TotalRecommendations} recommendations for user {UserId}",
                                 recommendations.TotalRecommendations, userId);
            return recommendations;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating recommendations for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Update learning path progress
    /// </summary>
    public async Task<LearningPathItem> UpdateLearningProgressAsync(string userId, int learningPathItemId, bool isCompleted, string? notes = null)
    {
        logger.LogInformation("Updating learning progress for user {UserId}, item {ItemId}", userId, learningPathItemId);

        try
        {
            var item = await context.LearningPathItems
                .Include(i => i.LearningPath)
                    .ThenInclude(lp => lp.UserProfile)
                .FirstOrDefaultAsync(i => i.Id == learningPathItemId);

            if (item == null)
            {
                throw new ArgumentException($"Learning path item {learningPathItemId} not found");
            }

            if (item.LearningPath.UserProfile.UserId != userId)
            {
                throw new UnauthorizedAccessException($"User {userId} does not have access to this learning path item");
            }

            item.IsCompleted = isCompleted;
            item.Notes = notes;

            if (isCompleted && !item.CompletedAt.HasValue)
            {
                item.CompletedAt = DateTime.UtcNow;
            }
            else if (!isCompleted)
            {
                item.CompletedAt = null;
            }

            // Check if entire learning path is completed
            var allItems = await context.LearningPathItems
                .Where(i => i.LearningPathId == item.LearningPathId)
                .ToListAsync();

            if (allItems.All(i => i.IsCompleted) && !item.LearningPath.IsCompleted)
            {
                item.LearningPath.IsCompleted = true;
                item.LearningPath.CompletedAt = DateTime.UtcNow;
                logger.LogInformation("Learning path {LearningPathId} completed by user {UserId}",
                                     item.LearningPathId, userId);
            }

            await context.SaveChangesAsync();

            logger.LogInformation("Successfully updated learning progress for user {UserId}", userId);
            return item;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating learning progress for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Get user's learning statistics
    /// </summary>
    public async Task<UserLearningStatistics> GetUserLearningStatisticsAsync(string userId)
    {
        logger.LogInformation("Retrieving learning statistics for user {UserId}", userId);

        try
        {
            var profile = await GetUserProfileAsync(userId);
            if (profile == null)
            {
                throw new ArgumentException($"User profile not found for {userId}");
            }

            var stats = new UserLearningStatistics
            {
                UserId = userId,
                GeneratedAt = DateTime.UtcNow
            };

            // Calculate learning path statistics
            var learningPaths = profile.LearningPaths.ToList();
            stats.TotalLearningPaths = learningPaths.Count;
            stats.CompletedLearningPaths = learningPaths.Count(lp => lp.IsCompleted);

            // Calculate item statistics
            var allItems = learningPaths.SelectMany(lp => lp.Items).ToList();
            stats.TotalLearningItems = allItems.Count;
            stats.CompletedLearningItems = allItems.Count(i => i.IsCompleted);

            // Calculate progress percentage
            stats.OverallProgressPercentage = stats.TotalLearningItems > 0
                ? (double)stats.CompletedLearningItems / stats.TotalLearningItems * 100
                : 0;

            // Calculate streak and activity
            stats.CurrentStreak = await CalculateCurrentStreakAsync(userId);
            stats.LongestStreak = await CalculateLongestStreakAsync(userId);

            // Get recent activity
            stats.RecentActivity = await GetRecentActivityAsync(userId);

            // Get preferred categories
            stats.PreferredCategories = profile.PreferredGenres;

            logger.LogInformation("Retrieved learning statistics for user {UserId}: {CompletedItems}/{TotalItems} items completed",
                                 userId, stats.CompletedLearningItems, stats.TotalLearningItems);
            return stats;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving learning statistics for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Get suggested next steps for user
    /// </summary>
    public async Task<List<SuggestedNextStep>> GetSuggestedNextStepsAsync(string userId)
    {
        logger.LogInformation("Getting suggested next steps for user {UserId}", userId);

        try
        {
            var profile = await GetUserProfileAsync(userId);
            if (profile == null)
            {
                throw new ArgumentException($"User profile not found for {userId}");
            }

            var suggestions = new List<SuggestedNextStep>();

            // Find incomplete learning path items
            var incompleteItems = profile.LearningPaths
                .SelectMany(lp => lp.Items)
                .Where(i => !i.IsCompleted)
                .OrderBy(i => i.OrderIndex)
                .Take(5);

            foreach (var item in incompleteItems)
            {
                suggestions.Add(new SuggestedNextStep
                {
                    Type = "Continue Learning Path",
                    Title = $"Continue with {item.ItemName}",
                    Description = $"Next item in your {item.LearningPath.Name} learning path",
                    Priority = 1,
                    EstimatedTimeMinutes = 30
                });
            }

            // Suggest new concepts based on preferences
            var recommendations = await GetPersonalizedRecommendationsAsync(userId);

            foreach (var chord in recommendations.IconicChords.Take(2))
            {
                suggestions.Add(new SuggestedNextStep
                {
                    Type = "Explore New Concept",
                    Title = $"Learn {chord.Name}",
                    Description = chord.Reason,
                    Priority = 2,
                    EstimatedTimeMinutes = 45
                });
            }

            foreach (var technique in recommendations.GuitarTechniques.Take(2))
            {
                suggestions.Add(new SuggestedNextStep
                {
                    Type = "Practice Technique",
                    Title = $"Practice {technique.Name}",
                    Description = technique.Reason,
                    Priority = 2,
                    EstimatedTimeMinutes = 60
                });
            }

            logger.LogInformation("Generated {SuggestionCount} next steps for user {UserId}", suggestions.Count, userId);
            return suggestions.OrderBy(s => s.Priority).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting suggested next steps for user {UserId}", userId);
            throw;
        }
    }

    private async Task UpdateUserPreferencesAsync(UserProfile profile, Dictionary<string, string> preferences)
    {
        // Remove existing preferences
        var existingPreferences = await context.UserPreferences
            .Where(p => p.UserProfileId == profile.Id)
            .ToListAsync();
        context.UserPreferences.RemoveRange(existingPreferences);

        // Add new preferences
        foreach (var preference in preferences)
        {
            await context.UserPreferences.AddAsync(new UserPreference
            {
                UserProfileId = profile.Id,
                PreferenceKey = preference.Key,
                PreferenceValue = preference.Value,
                UpdatedAt = DateTime.UtcNow
            });
        }
    }

    private async Task AddLearningPathItemsAsync(int learningPathId, List<LearningPathItemRequest> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            await context.LearningPathItems.AddAsync(new LearningPathItem
            {
                LearningPathId = learningPathId,
                OrderIndex = i + 1,
                ItemType = item.ItemType,
                ItemName = item.ItemName,
                IsCompleted = false
            });
        }

        await context.SaveChangesAsync();
    }

    private async Task GenerateLearningPathItemsAsync(LearningPath learningPath, UserProfile profile)
    {
        var items = new List<LearningPathItem>();

        // Generate items based on difficulty and category
        if (learningPath.Category == "Jazz")
        {
            items.AddRange(GenerateJazzLearningItems(learningPath.Id, learningPath.Difficulty));
        }
        else if (learningPath.Category == "Rock")
        {
            items.AddRange(GenerateRockLearningItems(learningPath.Id, learningPath.Difficulty));
        }
        else
        {
            items.AddRange(GenerateGeneralLearningItems(learningPath.Id, learningPath.Difficulty));
        }

        await context.LearningPathItems.AddRangeAsync(items);
        await context.SaveChangesAsync();
    }

    private List<LearningPathItem> GenerateJazzLearningItems(int learningPathId, string difficulty)
    {
        var items = new List<LearningPathItem>();

        if (difficulty == "Beginner")
        {
            items.Add(new LearningPathItem { LearningPathId = learningPathId, OrderIndex = 1, ItemType = "ChordProgression", ItemName = "I-vi-IV-V" });
            items.Add(new LearningPathItem { LearningPathId = learningPathId, OrderIndex = 2, ItemType = "ChordProgression", ItemName = "ii-V-I" });
            items.Add(new LearningPathItem { LearningPathId = learningPathId, OrderIndex = 3, ItemType = "GuitarTechnique", ItemName = "Basic Jazz Comping" });
        }
        else if (difficulty == "Intermediate")
        {
            items.Add(new LearningPathItem { LearningPathId = learningPathId, OrderIndex = 1, ItemType = "ChordProgression", ItemName = "Rhythm Changes" });
            items.Add(new LearningPathItem { LearningPathId = learningPathId, OrderIndex = 2, ItemType = "GuitarTechnique", ItemName = "Chord Melody" });
            items.Add(new LearningPathItem { LearningPathId = learningPathId, OrderIndex = 3, ItemType = "GuitarTechnique", ItemName = "Walking Bass Lines" });
        }

        return items;
    }

    private List<LearningPathItem> GenerateRockLearningItems(int learningPathId, string difficulty)
    {
        var items = new List<LearningPathItem>();

        if (difficulty == "Beginner")
        {
            items.Add(new LearningPathItem { LearningPathId = learningPathId, OrderIndex = 1, ItemType = "GuitarTechnique", ItemName = "Power Chords" });
            items.Add(new LearningPathItem { LearningPathId = learningPathId, OrderIndex = 2, ItemType = "GuitarTechnique", ItemName = "Basic Strumming" });
            items.Add(new LearningPathItem { LearningPathId = learningPathId, OrderIndex = 3, ItemType = "SpecializedTuning", ItemName = "Drop D" });
        }

        return items;
    }

    private List<LearningPathItem> GenerateGeneralLearningItems(int learningPathId, string difficulty)
    {
        var items = new List<LearningPathItem>();

        items.Add(new LearningPathItem { LearningPathId = learningPathId, OrderIndex = 1, ItemType = "ChordProgression", ItemName = "I-vi-IV-V" });
        items.Add(new LearningPathItem { LearningPathId = learningPathId, OrderIndex = 2, ItemType = "GuitarTechnique", ItemName = "Alternate Picking" });

        return items;
    }

    private Task EnhanceRecommendationsWithHistory(PersonalizedRecommendations recommendations, UserProfile profile)
    {
        // Filter out concepts the user has already completed
        var completedItems = profile.LearningPaths
            .SelectMany(lp => lp.Items)
            .Where(i => i.IsCompleted)
            .Select(i => i.ItemName)
            .ToHashSet();

        recommendations.IconicChords.RemoveAll(r => completedItems.Contains(r.Name));
        recommendations.ChordProgressions.RemoveAll(r => completedItems.Contains(r.Name));
        recommendations.GuitarTechniques.RemoveAll(r => completedItems.Contains(r.Name));
        recommendations.SpecializedTunings.RemoveAll(r => completedItems.Contains(r.Name));

        return Task.CompletedTask;
    }

    private Task<int> CalculateCurrentStreakAsync(string userId)
    {
        // This would calculate consecutive days of activity
        // For now, return a sample value
        return Task.FromResult(5);
    }

    private Task<int> CalculateLongestStreakAsync(string userId)
    {
        // This would calculate the longest streak of consecutive activity
        // For now, return a sample value
        return Task.FromResult(15);
    }

    private async Task<List<string>> GetRecentActivityAsync(string userId)
    {
        var recentItems = await context.LearningPathItems
            .Include(i => i.LearningPath)
                .ThenInclude(lp => lp.UserProfile)
            .Where(i => i.LearningPath.UserProfile.UserId == userId && i.CompletedAt.HasValue)
            .OrderByDescending(i => i.CompletedAt)
            .Take(5)
            .Select(i => $"Completed {i.ItemName}")
            .ToListAsync();

        return recentItems;
    }
}

/// <summary>
/// Data models for user personalization
/// </summary>
public class UserProfileRequest
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SkillLevel { get; set; } = string.Empty;
    public List<string> PreferredGenres { get; set; } = [];
    public List<string> Instruments { get; set; } = [];
    public Dictionary<string, string> Preferences { get; set; } = [];
}

public class LearningPathRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<LearningPathItemRequest> Items { get; set; } = [];
}

public class LearningPathItemRequest
{
    public string ItemType { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
}

public class UserLearningStatistics
{
    public string UserId { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public int TotalLearningPaths { get; set; }
    public int CompletedLearningPaths { get; set; }
    public int TotalLearningItems { get; set; }
    public int CompletedLearningItems { get; set; }
    public double OverallProgressPercentage { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public List<string> RecentActivity { get; set; } = [];
    public List<string> PreferredCategories { get; set; } = [];
}

public class SuggestedNextStep
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int EstimatedTimeMinutes { get; set; }
}
