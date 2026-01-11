namespace GA.Business.AI.Interpretation;

using GA.Business.Config;

/// <summary>
/// Nomenclature for semantic interpretation tags.
/// Ties hardcoded identifiers to extensible F# YAML configuration.
/// </summary>
public static class InterpretationTags
{
    public static class Mood
    {
        public const string Dreamy = "dreamy";
        public const string Tense = "tense";
        public const string Sad = "sad";
        public const string Happy = "happy";
        public const string Melancholy = "melancholy";
        public const string Aggressive = "aggressive";
        public const string Soulful = "soulful";
        public const string Tragic = "tragic";
        public const string Bright = "bright";
        public const string Floating = "floating";
        public const string Stable = "stable";
        public const string Unsettled = "unsettled";
    }

    public static class Genre
    {
        public const string Jazz = "jazz";
        public const string NeoSoul = "neo-soul";
        public const string Rock = "rock-guitar";
        public const string Funk = "funk";
        public const string Blues = "bluesy";
        public const string Folk = "folk-guitar";
        public const string Flamenco = "flamenco";
        public const string ModernJazz = "modern-jazz";
        public const string SpyNoir = "noir";
    }

    public static class Structure
    {
        public const string Shell = "shell-voicing";
        public const string Rootless = "rootless";
        public const string Quartal = "quartal-harmony";
        public const string Drop2 = "drop-2";
        public const string Drop3 = "drop-3";
        public const string OpenVoicing = "open-voicing";
        public const string ClosedVoicing = "closed-voicing";
    }

    public static class Famous
    {
        public const string Hendrix = "hendrix-chord";
        public const string JamesBond = "james-bond-chord";
        public const string MuMajor = "mu-major";
        public const string SoWhat = "so-what-chord";
        public const string Tristan = "tristan-chord";
    }

    public static class Playability
    {
        public const string Beginner = "beginner-friendly";
        public const string Campfire = "campfire-chord";
        public const string WideStretch = "wide-stretch";
        public const string Shred = "shred-guitar";
        public const string Barre = "barre-chord";
    }

    /// <summary>
    /// Retrieves full metadata for any tag id from the configuration.
    /// Handles F# interoperability by returning null for missing tags.
    /// </summary>
    public static SemanticConfig.SemanticTag? GetMetadata(string id)
    {
        var tag = SemanticConfig.TryGetTagByIdManaged(id);
        // In F#, a missing record via defaultof might still be null or have null fields 
        // since it's a reference type in the CLR for records.
        if (tag == null || string.IsNullOrWhiteSpace(tag.Id)) return null;
        return tag;
    }

    /// <summary>
    /// Gets the category name for a given tag id.
    /// </summary>
    public static string? GetCategory(string id) => SemanticConfig.TryGetCategoryByTagIdManaged(id);

    /// <summary>
    /// Checks if a tag belongs to a specific category (e.g., "Mood", "Genre").
    /// </summary>
    public static bool IsInCategory(string id, string category)
    {
        var actual = GetCategory(id);
        return string.Equals(actual, category, StringComparison.OrdinalIgnoreCase);
    }
}
