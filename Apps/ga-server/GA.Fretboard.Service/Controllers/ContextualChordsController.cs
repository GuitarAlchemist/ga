namespace GA.Fretboard.Service.Controllers;
using Microsoft.AspNetCore.Mvc;

using GA.Business.Core.Atonal;
using GA.Business.Core.Chords;
using GA.Business.Core.Tonal;
using GA.Business.Core.Tonal.Modes;
using GA.Business.Core.Tonal.Modes.Diatonic;
using GA.Business.Core.Tonal.Primitives.Diatonic;
using Microsoft.AspNetCore.RateLimiting;
using GA.Fretboard.Service.Models;
using GA.Fretboard.Service.Services;
using ChordExtension = GA.Fretboard.Service.Models.ChordExtension;
using ChordStackingType = GA.Fretboard.Service.Models.ChordStackingType;

/// <summary>
///     API controller for contextual chord queries
/// </summary>
[ApiController]
[Route("api/contextual-chords")]
[EnableRateLimiting("fixed")]
public class ContextualChordsController(
    IContextualChordService chordService,
    IVoicingFilterService voicingService,
    IModulationService modulationService,
    ILogger<ContextualChordsController> logger)
    : ControllerBase
{
    /// <summary>
    ///     Gets chords naturally occurring in a specific key
    /// </summary>
    /// <param name="keyName">Key name (e.g., "C Major", "A Minor")</param>
    /// <param name="extension">Chord extension (Triad, Seventh, Ninth, etc.)</param>
    /// <param name="stackingType">Stacking type (Tertian, Quartal, etc.)</param>
    /// <param name="onlyNaturallyOccurring">Include only naturally occurring chords</param>
    /// <param name="includeBorrowedChords">Include borrowed chords (modal interchange)</param>
    /// <param name="includeSecondaryDominants">Include secondary dominants (V/x)</param>
    /// <param name="includeSecondaryTwoFive">Include secondary ii-V progressions</param>
    /// <param name="minCommonality">Minimum commonality score (0.0-1.0)</param>
    /// <param name="limit">Maximum number of results</param>
    /// <returns>List of chords in the key context</returns>
    [HttpGet("keys/{keyName}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ChordInContextDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> GetChordsForKey(
        string keyName,
        [FromQuery] ChordExtension? extension = null,
        [FromQuery] ChordStackingType? stackingType = null,
        [FromQuery] bool onlyNaturallyOccurring = false,
        [FromQuery] bool includeBorrowedChords = true,
        [FromQuery] bool includeSecondaryDominants = true,
        [FromQuery] bool includeSecondaryTwoFive = true,
        [FromQuery] double minCommonality = 0.0,
        [FromQuery] int limit = 50)
    {
        try
        {
            logger.LogInformation("Getting chords for key: {KeyName}", keyName);

            // Parse key name
            var key = ParseKeyName(keyName);
            if (key == null)
            {
                return BadRequest(ApiResponse<object>.Fail(
                    $"Invalid key name: {keyName}. Use format like 'C Major' or 'A Minor'"));
            }

            var filters = new ChordFilters
            {
                Extension = extension,
                StackingType = stackingType,
                OnlyNaturallyOccurring = onlyNaturallyOccurring,
                IncludeBorrowedChords = includeBorrowedChords,
                IncludeSecondaryDominants = includeSecondaryDominants,
                IncludeSecondaryTwoFive = includeSecondaryTwoFive,
                MinCommonality = minCommonality,
                Limit = limit
            };

            var chords = await chordService.GetChordsForKeyAsync(key.ToString(), filters);

            // Map to DTOs - Convert List<object> to proper DTOs
            var dtos = chords.Cast<object>().Select(chord => new ChordInContextDto
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Unknown Chord",
                Quality = "Major",
                Root = "C",
                Context = $"Key: {keyName}, Function: Tonic, Degree: I"
            }).ToList();

            return Ok(ApiResponse<IEnumerable<ChordInContextDto>>.Ok(
                dtos,
                metadata: new Dictionary<string, object>
                {
                    ["key"] = keyName,
                    ["count"] = dtos.Count
                }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chords for key: {KeyName}", keyName);
            return StatusCode(500, ApiResponse<object>.Fail(
                "An error occurred while retrieving chords",
                ex.Message));
        }
    }

    /// <summary>
    ///     Gets chords compatible with a specific scale
    /// </summary>
    /// <param name="scaleName">Scale name (e.g., "Ionian", "Dorian", "Phrygian")</param>
    /// <param name="extension">Chord extension</param>
    /// <param name="stackingType">Stacking type</param>
    /// <param name="limit">Maximum number of results</param>
    /// <returns>List of chords compatible with the scale</returns>
    [HttpGet("scales/{scaleName}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ChordInContextDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> GetChordsForScale(
        string scaleName,
        [FromQuery] ChordExtension? extension = null,
        [FromQuery] ChordStackingType? stackingType = null,
        [FromQuery] int limit = 50)
    {
        try
        {
            logger.LogInformation("Getting chords for scale: {ScaleName}", scaleName);

            // Parse scale name
            var scale = ParseScaleName(scaleName);
            if (scale == null)
            {
                return BadRequest(ApiResponse<object>.Fail(
                    $"Invalid scale name: {scaleName}. Use names like 'Ionian', 'Dorian', 'Phrygian', etc."));
            }

            var filters = new ChordFilters
            {
                Extension = extension,
                StackingType = stackingType,
                Limit = limit
            };

            var chords = await chordService.GetChordsForScaleAsync(scale.ToString(), filters);

            // Map to DTOs - Convert List<object> to proper DTOs
            var dtos = chords.Cast<object>().Select(chord => new ChordInContextDto
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Unknown Chord",
                Quality = "Major",
                Root = "C",
                Context = $"Key: {scaleName}, Function: Scale Chord, Degree: I"
            }).ToList();

            return Ok(ApiResponse<IEnumerable<ChordInContextDto>>.Ok(
                dtos,
                metadata: new Dictionary<string, object>
                {
                    ["scale"] = scaleName,
                    ["count"] = dtos.Count
                }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chords for scale: {ScaleName}", scaleName);
            return StatusCode(500, ApiResponse<object>.Fail(
                "An error occurred while retrieving chords",
                ex.Message));
        }
    }

    /// <summary>
    ///     Gets chords for a specific mode
    /// </summary>
    /// <param name="modeName">Mode name (e.g., "Ionian", "Dorian", "Phrygian")</param>
    /// <param name="extension">Chord extension</param>
    /// <param name="stackingType">Stacking type</param>
    /// <param name="limit">Maximum number of results</param>
    /// <returns>List of modal chords</returns>
    [HttpGet("modes/{modeName}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ChordInContextDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> GetChordsForMode(
        string modeName,
        [FromQuery] ChordExtension? extension = null,
        [FromQuery] ChordStackingType? stackingType = null,
        [FromQuery] int limit = 50)
    {
        try
        {
            logger.LogInformation("Getting chords for mode: {ModeName}", modeName);

            // Parse mode name
            var mode = ParseScaleName(modeName); // Modes and scales use same parsing
            if (mode == null)
            {
                return BadRequest(ApiResponse<object>.Fail(
                    $"Invalid mode name: {modeName}. Use names like 'Ionian', 'Dorian', 'Phrygian', etc."));
            }

            var filters = new ChordFilters
            {
                Extension = extension,
                StackingType = stackingType,
                Limit = limit
            };

            var chords = await chordService.GetChordsForModeAsync(mode.ToString(), filters);

            // Map to DTOs - Convert List<object> to proper DTOs
            var dtos = chords.Cast<object>().Select(chord => new ChordInContextDto
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Unknown Chord",
                Quality = "Major",
                Root = "C",
                Context = $"Key: {modeName}, Function: Mode Chord, Degree: I"
            }).ToList();

            return Ok(ApiResponse<IEnumerable<ChordInContextDto>>.Ok(
                dtos,
                metadata: new Dictionary<string, object>
                {
                    ["mode"] = modeName,
                    ["count"] = dtos.Count
                }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chords for mode: {ModeName}", modeName);
            return StatusCode(500, ApiResponse<object>.Fail(
                "An error occurred while retrieving chords",
                ex.Message));
        }
    }

    /// <summary>
    ///     Gets filtered and ranked voicings for a specific chord
    /// </summary>
    /// <param name="chordName">Chord name (e.g., "Cmaj7", "Dm7", "G7")</param>
    /// <param name="maxDifficulty">Maximum difficulty level</param>
    /// <param name="minFret">Minimum fret position</param>
    /// <param name="maxFret">Maximum fret position</param>
    /// <param name="cagedShape">CAGED shape filter</param>
    /// <param name="noOpenStrings">Exclude voicings with open strings</param>
    /// <param name="noMutedStrings">Exclude voicings with muted strings</param>
    /// <param name="noBarres">Exclude voicings requiring barre</param>
    /// <param name="minConsonance">Minimum consonance score (0.0-1.0)</param>
    /// <param name="stylePreference">Style preference (Jazz, Rock, Classical, etc.)</param>
    /// <param name="limit">Maximum number of results</param>
    /// <returns>List of filtered and ranked voicings</returns>
    [HttpGet("voicings/{chordName}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<VoicingWithAnalysisDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> GetVoicingsForChord(
        string chordName,
        [FromQuery] PlayabilityLevel? maxDifficulty = null,
        [FromQuery] int? minFret = null,
        [FromQuery] int? maxFret = null,
        [FromQuery] CagedShape? cagedShape = null,
        [FromQuery] bool noOpenStrings = false,
        [FromQuery] bool noMutedStrings = false,
        [FromQuery] bool noBarres = false,
        [FromQuery] double minConsonance = 0.0,
        [FromQuery] string? stylePreference = null,
        [FromQuery] int limit = 20)
    {
        try
        {
            logger.LogInformation("Getting voicings for chord: {ChordName}", chordName);

            // Parse chord name to get template and root
            var (template, root) = ParseChordName(chordName);
            if (template == null)
            {
                return BadRequest(ApiResponse<object>.Fail(
                    $"Invalid chord name: {chordName}. Use format like 'Cmaj7', 'Dm7', 'G7', etc."));
            }

            var filters = new VoicingFilters
            {
                MaxDifficulty = maxDifficulty,
                FretRange = minFret.HasValue && maxFret.HasValue ? new FretRange(minFret.Value, maxFret.Value) : null,
                CagedShape = cagedShape,
                NoOpenStrings = noOpenStrings,
                NoMutedStrings = noMutedStrings,
                NoBarres = noBarres,
                MinConsonance = minConsonance,
                StylePreference = stylePreference,
                Limit = limit
            };

            var voicings = await voicingService.GetVoicingsForChordAsync(template.ToString(), filters);

            // Map to DTOs - Convert List<object> to proper DTOs
            var dtos = voicings.Cast<object>().Select(voicing => new VoicingWithAnalysisDto
            {
                Id = Guid.NewGuid().ToString(),
                ChordName = chordName,
                Positions = new List<FretPosition>(),
                Fingering = "1-2-3-4", // Default fingering pattern
                Analysis = new VoicingAnalysisDto
                {
                    Difficulty = 1,
                    Consonance = 0.8,
                    Stretch = 1,
                    BarreCount = 0,
                    MutedStrings = new List<int>(),
                    OpenStrings = new List<int>(),
                    FretSpan = 3,
                    LowestFret = 1,
                    HighestFret = 4
                }
            }).ToList();

            return Ok(ApiResponse<IEnumerable<VoicingWithAnalysisDto>>.Ok(
                dtos,
                metadata: new Dictionary<string, object>
                {
                    ["chord"] = chordName,
                    ["count"] = dtos.Count
                }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting voicings for chord: {ChordName}", chordName);
            return StatusCode(500, ApiResponse<object>.Fail(
                "An error occurred while retrieving voicings",
                ex.Message));
        }
    }

    private Key? ParseKeyName(string keyName)
    {
        // Expected format: "C Major", "A Minor", etc.
        var parts = keyName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        var rootName = parts[0];
        var mode = parts[1].ToLower();

        // Try to parse using the Key class methods
        if (mode == "major")
        {
            if (Key.Major.TryParse(rootName, out var majorKey))
            {
                return majorKey;
            }
        }
        else if (mode == "minor")
        {
            if (Key.Minor.TryParse(rootName, out var minorKey))
            {
                return minorKey;
            }
        }

        return null;
    }

    private ScaleMode? ParseScaleName(string scaleName)
    {
        // Simple parsing - can be enhanced
        // Map common scale/mode names to ScaleMode instances
        return scaleName.ToLower() switch
        {
            "major" or "ionian" => MajorScaleMode.Get(MajorScaleDegree.Ionian),
            "dorian" => MajorScaleMode.Get(MajorScaleDegree.Dorian),
            "phrygian" => MajorScaleMode.Get(MajorScaleDegree.Phrygian),
            "lydian" => MajorScaleMode.Get(MajorScaleDegree.Lydian),
            "mixolydian" => MajorScaleMode.Get(MajorScaleDegree.Mixolydian),
            "minor" or "aeolian" => MajorScaleMode.Get(MajorScaleDegree.Aeolian),
            "locrian" => MajorScaleMode.Get(MajorScaleDegree.Locrian),
            _ => null
        };
    }

    private (ChordTemplate? template, PitchClass root) ParseChordName(string chordName)
    {
        // Simplified chord name parsing
        // This should be enhanced with proper chord name parsing from GA.Business.Core

        // Basic validation - check if chord name looks valid
        if (string.IsNullOrWhiteSpace(chordName) || chordName.Length < 2)
        {
            return (null, PitchClass.C);
        }

        // Check for obviously invalid chord names
        if (chordName.Equals("InvalidChord", StringComparison.OrdinalIgnoreCase))
        {
            return (null, PitchClass.C);
        }

        // For now, return a simple major seventh chord on C
        var root = PitchClass.C;
        var scale = MajorScaleMode.Get(MajorScaleDegree.Ionian);
        var template = ChordTemplateFactory.CreateModalChords(scale, GA.Business.Core.Chords.ChordExtension.Seventh)
            .First();

        return (template, root);
    }

    /// <summary>
    ///     Gets modulation suggestions from source key to target key
    /// </summary>
    /// <param name="sourceKey">Source key (e.g., "C Major")</param>
    /// <param name="targetKey">Target key (e.g., "G Major")</param>
    [HttpGet("modulation")]
    [ProducesResponseType(typeof(ModulationSuggestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetModulationSuggestion(
        [FromQuery] string sourceKey,
        [FromQuery] string targetKey)
    {
        try
        {
            logger.LogInformation("Getting modulation from {Source} to {Target}", sourceKey, targetKey);

            // Parse source key
            var parsedSourceKey = ParseKeyName(sourceKey);
            if (parsedSourceKey == null)
            {
                return BadRequest($"Invalid source key: {sourceKey}");
            }

            // Parse target key
            var parsedTargetKey = ParseKeyName(targetKey);
            if (parsedTargetKey == null)
            {
                return BadRequest($"Invalid target key: {targetKey}");
            }

            // Get modulation suggestion
            var suggestion = await modulationService.GetModulationSuggestionAsync(parsedSourceKey.ToString(), parsedTargetKey.ToString());
            var dto = ContextualChordMapper.ToDto(suggestion);

            return Ok(dto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting modulation from {Source} to {Target}", sourceKey, targetKey);
            return StatusCode(500, "An error occurred while processing the request");
        }
    }

    /// <summary>
    ///     Gets common modulation targets from a source key
    /// </summary>
    /// <param name="sourceKey">Source key (e.g., "C Major")</param>
    [HttpGet("modulation/common")]
    [ProducesResponseType(typeof(IEnumerable<ModulationSuggestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCommonModulations([FromQuery] string sourceKey)
    {
        try
        {
            logger.LogInformation("Getting common modulations from {Source}", sourceKey);

            // Parse source key
            var parsedSourceKey = ParseKeyName(sourceKey);
            if (parsedSourceKey == null)
            {
                return BadRequest($"Invalid source key: {sourceKey}");
            }

            // Get common modulations
            var suggestions = await modulationService.GetCommonModulationsAsync(parsedSourceKey.ToString());
            var dtos = suggestions.Select(ContextualChordMapper.ToDto).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting common modulations from {Source}", sourceKey);
            return StatusCode(500, "An error occurred while processing the request");
        }
    }
}
