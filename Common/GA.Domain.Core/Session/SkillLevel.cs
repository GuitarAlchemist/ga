namespace GA.Domain.Core.Session;

/// <summary>
/// User skill level for filtering and suggesting appropriate content
/// </summary>
[PublicAPI]
public enum SkillLevel
{
    /// <summary>New to the instrument</summary>
    Beginner,
    
    /// <summary>Comfortable with basic techniques</summary>
    Intermediate,
    
    /// <summary>Proficient with advanced techniques</summary>
    Advanced,
    
    /// <summary>Professional-level mastery</summary>
    Expert
}