using GA.Business.Core.Atonal;

namespace GA.Business.Core.Chords
{
    /// <summary>
    /// Simple chord definition for iconic chords
    /// </summary>
    public class IconicChordDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string IconicName => Name;  // Alias for compatibility
        public string TheoreticalName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Song { get; set; } = string.Empty;
        public string Era { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public List<int> PitchClasses { get; set; } = [];
        public List<int>? GuitarVoicing { get; set; }
        public List<string> AlternateNames { get; set; } = [];
    }

    /// <summary>
    /// Stub implementation for IconicChordRegistry
    /// </summary>
    public static class IconicChordRegistry
    {
        /// <summary>
        /// Finds iconic chord matches for a pitch class set
        /// </summary>
        public static IEnumerable<IconicChordDefinition> FindIconicMatches(PitchClassSet pitchClassSet)
        {
            // Stub implementation - returns empty for now
            // Real implementation would query IconicChordsService from GA.Business.Config
            return Enumerable.Empty<IconicChordDefinition>();
        }

        /// <summary>
        /// Finds an iconic chord by name
        /// </summary>
        public static IconicChordDefinition? FindByName(string name)
        {
            // Stub implementation - returns null for now
            return null;
        }

        /// <summary>
        /// Finds iconic chords by guitar voicing
        /// </summary>
        public static IEnumerable<IconicChordDefinition> FindByGuitarVoicing(IEnumerable<int> voicing)
        {
            // Stub implementation - returns empty for now
            return Enumerable.Empty<IconicChordDefinition>();
        }
    }
}

namespace GA.Business.Core.Tonal.Modes
{
    /// <summary>
    /// Stub implementation for ModalFamilyScaleModeFactory
    /// </summary>
    public static class ModalFamilyScaleModeFactory
    {
        /// <summary>
        /// Stub method for creating modes from family
        /// </summary>
        public static IEnumerable<ScaleMode> CreateModesFromFamily(object family)
        {
            return Enumerable.Empty<ScaleMode>();
        }
    }
}
