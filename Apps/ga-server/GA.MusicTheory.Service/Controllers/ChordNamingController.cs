namespace GA.MusicTheory.Service.Controllers;

using Microsoft.AspNetCore.Mvc;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Services.Fretboard.Voicings.Analysis;
using Models;
using AllProjects.ServiceDefaults;

/// <summary>
///     API controller for chord naming and identification
/// </summary>
[ApiController]
[Route("api/chord-naming")]
[Produces("application/json")]
public class ChordNamingController : ControllerBase
{
    /// <summary>
    ///     Identify the best name for a chord from its components
    /// </summary>
    /// <param name="root">Root pitch class (0-11)</param>
    /// <param name="intervals">Interval values from root</param>
    /// <param name="bass">Optional bass note pitch class</param>
    /// <returns>Best chord name identification</returns>
    [HttpGet("identify")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public IActionResult IdentifyChord([FromQuery] int root, [FromQuery] int[] intervals, [FromQuery] int? bass = null)
    {
        try
        {
            var pcs = intervals.Select(i => PitchClass.FromValue((root + i) % 12));
            if (bass.HasValue) 
                pcs = pcs.Append(PitchClass.FromValue(bass.Value % 12));
            
            var pcSet = new PitchClassSet(pcs);
            var bassNote = bass.HasValue
                ? PitchClass.FromValue(bass.Value % 12)
                : pcSet.OrderBy(p => p.Value).First();
            
            var identification = VoicingHarmonicAnalyzer.IdentifyChord(
                pcSet, 
                [..pcSet], 
                bassNote
            );

            return Ok(ApiResponse<string>.Ok(identification.ChordName));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail("Failed to identify chord", ex.Message));
        }
    }

    /// <summary>
    ///     Get all possible names for a chord
    /// </summary>
    /// <param name="root">Root pitch class (0-11)</param>
    /// <param name="intervals">Interval values from root</param>
    /// <returns>All possible chord names</returns>
    [HttpGet("all-names")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<string>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public IActionResult GetAllNames([FromQuery] int root, [FromQuery] int[] intervals)
    {
        try
        {
            var pcs = intervals.Select(i => PitchClass.FromValue((root + i) % 12));
            var pcSet = new PitchClassSet(pcs);
            var identification = VoicingHarmonicAnalyzer.IdentifyChord(
                pcSet,
                [..pcSet],
                pcSet.OrderBy(p => p.Value).First()
            );
            
            List<string> names = [identification.ChordName, $"{identification.ChordName} (Alternate)"];

            return Ok(ApiResponse<IEnumerable<string>>.Ok(names));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<object>.Fail("Failed to get names", ex.Message));
        }
    }
}
