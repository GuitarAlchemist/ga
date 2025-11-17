namespace GaApi.Controllers;

using GA.Business.Core.Tonal;
using GA.Business.Core.Tonal.Modes.Diatonic;
using GA.Business.Core.Tonal.Primitives.Diatonic;
using Microsoft.AspNetCore.RateLimiting;
using Models;

/// <summary>
///     API controller for music theory metadata (keys, modes, scales, intervals)
/// </summary>
[ApiController]
[Route("api/music-theory")]
[EnableRateLimiting("fixed")]
public class MusicTheoryController(ILogger<MusicTheoryController> logger) : ControllerBase
{
    /// <summary>
    ///     Get all available musical keys (major and minor)
    /// </summary>
    /// <returns>List of all keys with their properties</returns>
    [HttpGet("keys")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<KeyDto>>), 200)]
    public IActionResult GetAllKeys()
    {
        try
        {
            var majorKeys = Key.Major.MajorItems.Select(k => new KeyDto
            {
                Name = k.ToString(),
                Root = k.Root.ToString(),
                Mode = "Major",
                KeySignature = k.KeySignature.Value,
                AccidentalKind = k.AccidentalKind.ToString(),
                Notes = [.. k.Notes.Select(n => n.ToString())]
            });

            var minorKeys = Key.Minor.MinorItems.Select(k => new KeyDto
            {
                Name = k.ToString(),
                Root = k.Root.ToString(),
                Mode = "Minor",
                KeySignature = k.KeySignature.Value,
                AccidentalKind = k.AccidentalKind.ToString(),
                Notes = [.. k.Notes.Select(n => n.ToString())]
            });

            var allKeys = majorKeys.Concat(minorKeys).ToList();

            return Ok(ApiResponse<IEnumerable<KeyDto>>.Ok(
                allKeys,
                metadata: new Dictionary<string, object>
                {
                    ["totalKeys"] = allKeys.Count,
                    ["majorKeys"] = majorKeys.Count(),
                    ["minorKeys"] = minorKeys.Count()
                }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting all keys");
            return StatusCode(500, ApiResponse<object>.Fail(
                "An error occurred while retrieving keys",
                ex.Message));
        }
    }

    /// <summary>
    ///     Get all available modes (Ionian, Dorian, Phrygian, etc.)
    /// </summary>
    /// <returns>List of all modes with their properties</returns>
    [HttpGet("modes")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ModeDto>>), 200)]
    public IActionResult GetAllModes()
    {
        try
        {
            var modes = new[]
            {
                MajorScaleDegree.Ionian,
                MajorScaleDegree.Dorian,
                MajorScaleDegree.Phrygian,
                MajorScaleDegree.Lydian,
                MajorScaleDegree.Mixolydian,
                MajorScaleDegree.Aeolian,
                MajorScaleDegree.Locrian
            }.Select((degree, index) =>
            {
                var mode = MajorScaleMode.Get(degree);
                return new ModeDto
                {
                    Name = mode.Name,
                    Degree = index + 1,
                    IsMinor = mode.IsMinorMode,
                    Intervals = [.. mode.SimpleIntervals.Select(i => i.ToString())],
                    CharacteristicNotes = [.. mode.CharacteristicNotes.Select(n => n.ToString())]
                };
            }).ToList();

            return Ok(ApiResponse<IEnumerable<ModeDto>>.Ok(
                modes,
                metadata: new Dictionary<string, object>
                {
                    ["totalModes"] = modes.Count,
                    ["majorModes"] = modes.Count(m => !m.IsMinor),
                    ["minorModes"] = modes.Count(m => m.IsMinor)
                }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting all modes");
            return StatusCode(500, ApiResponse<object>.Fail(
                "An error occurred while retrieving modes",
                ex.Message));
        }
    }

    /// <summary>
    ///     Get scale degrees (I, II, III, IV, V, VI, VII) with Roman numeral notation
    /// </summary>
    /// <returns>List of scale degrees</returns>
    [HttpGet("scale-degrees")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ScaleDegreeDto>>), 200)]
    public IActionResult GetScaleDegrees()
    {
        try
        {
            var degrees = new[]
            {
                new ScaleDegreeDto { Degree = 1, RomanNumeral = "I", Name = "Tonic" },
                new ScaleDegreeDto { Degree = 2, RomanNumeral = "II", Name = "Supertonic" },
                new ScaleDegreeDto { Degree = 3, RomanNumeral = "III", Name = "Mediant" },
                new ScaleDegreeDto { Degree = 4, RomanNumeral = "IV", Name = "Subdominant" },
                new ScaleDegreeDto { Degree = 5, RomanNumeral = "V", Name = "Dominant" },
                new ScaleDegreeDto { Degree = 6, RomanNumeral = "VI", Name = "Submediant" },
                new ScaleDegreeDto { Degree = 7, RomanNumeral = "VII", Name = "Leading Tone" }
            };

            return Ok(ApiResponse<IEnumerable<ScaleDegreeDto>>.Ok(
                degrees,
                metadata: new Dictionary<string, object>
                {
                    ["totalDegrees"] = degrees.Length
                }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting scale degrees");
            return StatusCode(500, ApiResponse<object>.Fail(
                "An error occurred while retrieving scale degrees",
                ex.Message));
        }
    }

    /// <summary>
    ///     Get notes for a specific key
    /// </summary>
    /// <param name="keyName">Key name (e.g., "C Major", "A Minor")</param>
    /// <returns>Notes in the key with fretboard positions</returns>
    [HttpGet("keys/{keyName}/notes")]
    [ProducesResponseType(typeof(ApiResponse<KeyNotesDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public IActionResult GetKeyNotes(string keyName)
    {
        try
        {
            // Parse key name (e.g., "C Major" or "A Minor")
            var parts = keyName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                return BadRequest(ApiResponse<object>.Fail(
                    $"Invalid key name format: {keyName}. Use format like 'C Major' or 'A Minor'"));
            }

            var root = parts[0];
            var mode = parts[1];

            Key? key = mode.ToLower() switch
            {
                "major" => Key.Major.MajorItems.FirstOrDefault(k => k.Root.ToString() == root),
                "minor" => Key.Minor.MinorItems.FirstOrDefault(k => k.Root.ToString() == root),
                _ => null
            };

            if (key == null)
            {
                return BadRequest(ApiResponse<object>.Fail(
                    $"Invalid key: {keyName}"));
            }

            var result = new KeyNotesDto
            {
                KeyName = key.ToString(),
                Root = key.Root.ToString(),
                Mode = key.KeyMode.ToString(),
                Notes = [.. key.Notes.Select(n => n.ToString())],
                KeySignature = key.KeySignature.Value,
                AccidentalKind = key.AccidentalKind.ToString()
            };

            return Ok(ApiResponse<KeyNotesDto>.Ok(result));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting notes for key: {KeyName}", keyName);
            return StatusCode(500, ApiResponse<object>.Fail(
                "An error occurred while retrieving key notes",
                ex.Message));
        }
    }
}

/// <summary>
///     DTO for musical key information
/// </summary>
public class KeyDto
{
    public required string Name { get; set; }
    public required string Root { get; set; }
    public required string Mode { get; set; }
    public required int KeySignature { get; set; }
    public required string AccidentalKind { get; set; }
    public required List<string> Notes { get; set; }
}

/// <summary>
///     DTO for mode information
/// </summary>
public class ModeDto
{
    public required string Name { get; set; }
    public required int Degree { get; set; }
    public required bool IsMinor { get; set; }
    public required List<string> Intervals { get; set; }
    public required List<string> CharacteristicNotes { get; set; }
}

/// <summary>
///     DTO for scale degree information
/// </summary>
public class ScaleDegreeDto
{
    public required int Degree { get; set; }
    public required string RomanNumeral { get; set; }
    public required string Name { get; set; }
}

/// <summary>
///     DTO for key notes with fretboard positions
/// </summary>
public class KeyNotesDto
{
    public required string KeyName { get; set; }
    public required string Root { get; set; }
    public required string Mode { get; set; }
    public required List<string> Notes { get; set; }
    public required int KeySignature { get; set; }
    public required string AccidentalKind { get; set; }
}
