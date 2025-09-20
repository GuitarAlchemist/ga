namespace GA.Business.Core.Chords;

/// <summary>
/// Represents the type of chord stacking pattern
/// </summary>
public enum ChordStackingType
{
    /// <summary>
    /// Traditional tertian harmony (stacked thirds)
    /// </summary>
    Tertian,
    
    /// <summary>
    /// Quartal harmony (stacked fourths)
    /// </summary>
    Quartal,
    
    /// <summary>
    /// Quintal harmony (stacked fifths)
    /// </summary>
    Quintal,
    
    /// <summary>
    /// Secundal harmony (stacked seconds)
    /// </summary>
    Secundal,
    
    /// <summary>
    /// Mixed interval stacking
    /// </summary>
    Mixed,
    
    /// <summary>
    /// Custom interval pattern
    /// </summary>
    Custom
}
