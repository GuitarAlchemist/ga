namespace GA.Business.Core.Tonal.Modes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Collections.Immutable;
    using System.Collections.Concurrent;
    using System.Text;
    using Atonal;
    using Config;
    using Notes;
    using Scales;

    /// <summary>
    /// Factory class for creating and accessing modal family scale modes.
    /// </summary>
    [PublicAPI]
    public static class ModalFamilyScaleModeFactory
    {
        // Collection to track missing modes that could be added to Modes.yaml
        private static readonly ConcurrentDictionary<string, MissingModeInfo> MissingModes = new();

        /// <summary>
        /// Represents information about a mode that's missing from the Modes.yaml configuration
        /// </summary>
        public class MissingModeInfo
        {
            /// <summary>
            /// Gets or sets the interval class vector of the mode
            /// </summary>
            public string IntervalClassVector { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the number of notes in the mode
            /// </summary>
            public int NoteCount { get; set; }

            /// <summary>
            /// Gets or sets the number of modes in the family
            /// </summary>
            public int ModeCount { get; set; }

            /// <summary>
            /// Gets or sets the scale used as a fallback
            /// </summary>
            public Scale FallbackScale { get; set; } = null!;

            /// <summary>
            /// Gets the notes of the scale as a string
            /// </summary>
            public string ScaleNotes => FallbackScale.ToString();
        }

        /// <summary>
        /// Creates modes from all available modal families.
        /// </summary>
        /// <returns>A collection of modal family scale modes.</returns>
        public static ImmutableList<ModalFamilyScaleMode> CreateModesFromAllFamilies() => ModalFamily.Items
            .SelectMany(CreateModesFromFamily)
            .ToImmutableList();

        /// <summary>
        /// Creates modes from a specific modal family.
        /// </summary>
        /// <param name="modalFamily">The modal family to create modes from.</param>
        /// <returns>A collection of modal family scale modes for the specified family.</returns>
        public static ImmutableList<ModalFamilyScaleMode> CreateModesFromFamily(ModalFamily modalFamily)
        {
            ArgumentNullException.ThrowIfNull(modalFamily);

            var listBuilder = ImmutableList.CreateBuilder<ModalFamilyScaleMode>();
            for (var degree = 1; degree <= modalFamily.Modes.Count; degree++)
            {
                var pitchClassSet = modalFamily.Modes.ElementAt(degree - 1);

                // Use the ID to look up the mode configuration
                if (!ModesConfigCache.Instance.TryGetModeByPitchClassSetId(pitchClassSet.Id.Value, out var modeConfig))
                {
                    // Instead of throwing an exception, log the missing mode and create a fallback mode
                    var key = $"{modalFamily.IntervalClassVector}_{degree}";
                    MissingModes.TryAdd(key, new MissingModeInfo
                    {
                        IntervalClassVector = modalFamily.IntervalClassVector.ToString(),
                        NoteCount = modalFamily.Modes.Count,
                        ModeCount = modalFamily.Modes.Count,
                        FallbackScale = new Scale(pitchClassSet.Notes)
                    });

                    // Create a fallback mode with the pitch class set notes
                    var fallbackNotes = AccidentedNoteCollection.Parse(string.Join(" ", pitchClassSet.Notes.Select(pc => pc.ToSharp().ToString())));
                    var fallbackMode = new ModalFamilyScaleMode(modalFamily, degree, fallbackNotes, null);
                    listBuilder.Add(fallbackMode);
                    continue;
                }

                var modeNotes = AccidentedNoteCollection.Parse(modeConfig.Mode.Notes);
                var modeInstance = new ModalFamilyScaleMode(modalFamily, degree, modeNotes, modeConfig);
                listBuilder.Add(modeInstance);
            }

            return listBuilder.ToImmutable();
        }

        /// <summary>
        /// Gets information about modes that are missing from the Modes.yaml configuration.
        /// This can be used to identify gaps in the configuration and provide the necessary information to update the YAML file.
        /// </summary>
        /// <returns>A collection of missing mode information.</returns>
        public static IEnumerable<MissingModeInfo> GetMissingModes()
        {
            return MissingModes.Values.ToList();
        }

        /// <summary>
        /// Generates YAML entries for modes that are missing from the Modes.yaml configuration.
        /// </summary>
        /// <returns>A string containing YAML entries for missing modes, or an empty string if no modes are missing.</returns>
        public static string GenerateMissingModesYaml()
        {
            if (!MissingModes.Any())
            {
                return string.Empty; // No missing modes
            }

            var sb = new StringBuilder();

            // Group missing modes by interval class vector
            var groupedModes = MissingModes.Values
                .GroupBy(m => m.IntervalClassVector)
                .OrderBy(g => g.Key);

            sb.AppendLine("ModalFamilies:");
            foreach (var group in groupedModes)
            {
                sb.AppendLine($"  - Name: \"Modal Family with vector {group.Key}\"");
                sb.AppendLine($"    IntervalClassVector: \"{group.Key}\"");
                sb.AppendLine("    Modes:");

                var modes = group.OrderBy(m => m.FallbackScale.ToString());
                foreach (var missingMode in modes)
                {
                    // Generate a descriptive name based on the interval class vector and scale notes
                    var modeName = $"Mode_{missingMode.IntervalClassVector.Replace("<", "").Replace(">", "").Replace(" ", "")}_{missingMode.FallbackScale.ToString().Replace(" ", "")}";

                    sb.AppendLine($"      - Name: \"{modeName}\"");
                    sb.AppendLine($"        Notes: \"{missingMode.ScaleNotes}\"");
                    sb.AppendLine($"        Description: \"A mode with interval class vector {missingMode.IntervalClassVector}.\"");
                    sb.AppendLine($"        AlternateNames: []");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generates a complete YAML file with all modal families and modes.
        /// </summary>
        /// <returns>A string containing YAML entries for all modal families and modes.</returns>
        public static string GenerateCompleteModesYaml()
        {
            var sb = new StringBuilder();

            // Get all modal families
            var modalFamilies = ModalFamily.Items.ToList();

            sb.AppendLine("ModalFamilies:");
            foreach (var modalFamily in modalFamilies.OrderBy(f => f.IntervalClassVector.ToString()))
            {
                sb.AppendLine($"  - Name: \"Modal Family with vector {modalFamily.IntervalClassVector}\"");
                sb.AppendLine($"    IntervalClassVector: \"{modalFamily.IntervalClassVector}\"");
                sb.AppendLine("    Modes:");

                for (var degree = 1; degree <= modalFamily.Modes.Count; degree++)
                {
                    var pitchClassSet = modalFamily.Modes.ElementAt(degree - 1);

                    // Try to get the mode configuration
                    ModesConfigCache.ModeCacheValue? modeConfig = null;
                    ModesConfigCache.Instance.TryGetModeByPitchClassSetId(pitchClassSet.Id.Value, out modeConfig);

                    // Generate a name for the mode
                    string modeName;
                    string notes;
                    string description;
                    List<string> alternateNames = new List<string>();

                    // Use the existing mode configuration
                    modeName = modeConfig.Mode.Name;
                    notes = modeConfig.Mode.Notes;
                    description = modeConfig.Mode.Description != null ?
                        modeConfig.Mode.Description.ToString() :
                        $"A mode with interval class vector {modalFamily.IntervalClassVector}.";
                    if (modeConfig.Mode.AlternateNames != null)
                    {
                        var altNames = modeConfig.Mode.AlternateNames.ToString().Split(',').Select(s => s.Trim()).ToList();
                        alternateNames.AddRange(altNames);
                    }

                    sb.AppendLine($"      - Name: \"{modeName}\"");
                    sb.AppendLine($"        Notes: \"{notes}\"");
                    sb.AppendLine($"        Description: \"{description}\"");
                    sb.AppendLine($"        AlternateNames: [{string.Join(", ", alternateNames.Select(n => $"\"{n}\""))}]");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}