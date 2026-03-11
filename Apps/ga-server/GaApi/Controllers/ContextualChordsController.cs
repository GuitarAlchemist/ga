namespace GaApi.Controllers;

using Microsoft.AspNetCore.Mvc;
using Services;
using Models;

[ApiController]
[Route("api/contextual-chords")]
public class ContextualChordsController(
    ContextualChordService chordService,
    VoicingFilterService   voicingService,
    ILogger<ContextualChordsController> logger) : ControllerBase
{
    [HttpGet("keys/{keyName}")]
    public async Task<ActionResult<IEnumerable<ChordInContext>>> GetChordsForKey(string keyName)
    {
        var result = await chordService.GetChordsForKeyAsync(keyName);
        if (!result.IsSuccess) return BadRequest(result.GetErrorOrThrow());
        return Ok(result.GetValueOrThrow());
    }

    [HttpGet("scales/{scaleName}/{rootName}")]
    public async Task<ActionResult<IEnumerable<ChordInContext>>> GetChordsForScale(string scaleName, string rootName)
    {
        var result = await chordService.GetChordsForScaleAsync(scaleName, rootName);
        if (!result.IsSuccess) return BadRequest(result.GetErrorOrThrow());
        return Ok(result.GetValueOrThrow());
    }

    [HttpGet("modes/{modeName}/{rootName}")]
    public async Task<ActionResult<IEnumerable<ChordInContext>>> GetChordsForMode(string modeName, string rootName)
    {
        var result = await chordService.GetChordsForModeAsync(modeName, rootName);
        if (!result.IsSuccess) return BadRequest(result.GetErrorOrThrow());
        return Ok(result.GetValueOrThrow());
    }

    [HttpGet("borrowed/{keyName}")]
    public async Task<ActionResult<IEnumerable<BorrowedChordInContext>>> GetBorrowedChords(string keyName)
    {
        var result = await chordService.GetBorrowedChordsAsync(keyName);
        if (!result.IsSuccess) return BadRequest(result.GetErrorOrThrow());
        return Ok(result.GetValueOrThrow());
    }

    [HttpGet("voicings/{chordName}")]
    public async Task<ActionResult<IEnumerable<VoicingWithAnalysis>>> GetVoicingsForChord(
        string              chordName,
        [FromQuery] int?  maxDifficulty = null,
        [FromQuery] int?  minFret       = null,
        [FromQuery] int?  maxFret       = null,
        [FromQuery] bool  noOpenStrings = false)
    {
        try
        {
            var results = await voicingService.GetVoicingsForChordAsync(
                chordName, maxDifficulty, minFret, maxFret, noOpenStrings);
            return Ok(results);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid argument for voicings request for chord {ChordName}", chordName);
            return BadRequest("Invalid request.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting voicings for chord {ChordName}", chordName);
            return StatusCode(500, "Internal server error");
        }
    }
}
