namespace GaApi.Controllers;

using Microsoft.AspNetCore.Mvc;
using Services;
using Models;

[ApiController]
[Route("api/contextual-chords")]
public class ContextualChordsController(
    ContextualChordService chordService,
    VoicingFilterService voicingService,
    ILogger<ContextualChordsController> logger) : ControllerBase
{
    [HttpGet("keys/{keyName}")]
    public async Task<ActionResult<IEnumerable<ChordInContext>>> GetChordsForKey(string keyName)
    {
        try
        {
            var results = await chordService.GetChordsForKeyAsync(keyName);
            return Ok(results);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chords for key {KeyName}", keyName);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("scales/{scaleName}/{rootName}")]
    public async Task<ActionResult<IEnumerable<ChordInContext>>> GetChordsForScale(string scaleName, string rootName)
    {
        try
        {
            var results = await chordService.GetChordsForScaleAsync(scaleName, rootName);
            return Ok(results);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chords for scale {ScaleName} with root {RootName}", scaleName, rootName);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("modes/{modeName}/{rootName}")]
    public async Task<ActionResult<IEnumerable<ChordInContext>>> GetChordsForMode(string modeName, string rootName)
    {
        try
        {
            var results = await chordService.GetChordsForModeAsync(modeName, rootName);
            return Ok(results);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chords for mode {ModeName} with root {RootName}", modeName, rootName);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("borrowed/{keyName}")]
    public async Task<ActionResult<IEnumerable<BorrowedChordInContext>>> GetBorrowedChords(string keyName)
    {
        try
        {
            var results = await chordService.GetBorrowedChordsAsync(keyName);
            return Ok(results);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting borrowed chords for key {KeyName}", keyName);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("voicings/{chordName}")]
    public async Task<ActionResult<IEnumerable<VoicingWithAnalysis>>> GetVoicingsForChord(string chordName, [FromQuery] int? maxDifficulty = null)
    {
        try
        {
            var results = await voicingService.GetVoicingsForChordAsync(chordName, maxDifficulty);
            return Ok(results);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting voicings for chord {ChordName}", chordName);
            return StatusCode(500, "Internal server error");
        }
    }
}
