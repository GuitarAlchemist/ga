namespace GA.MusicTheory.Service.Controllers;

using Microsoft.AspNetCore.Mvc;
using GA.Domain.Core.Theory.Tonal;
using GA.Domain.Core.Primitives.Notes;
using Models;
using AllProjects.ServiceDefaults;

/// <summary>
///     API controller for basic music theory lookup (notes, keys)
/// </summary>
[ApiController]
[Route("api/theory")]
[Produces("application/json")]
public class TheoryController : ControllerBase
{
    /// <summary>
    ///     Get a musical key by name (e.g. "C Major", "F# Minor", "Am", "Eb")
    /// </summary>
    /// <param name="name">Key name</param>
    /// <returns>Key info or error</returns>
    [HttpGet("key/{name}")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public IActionResult GetKey(string name)
    {
        if (Key.Major.TryParse(name, out var major))
            return Ok(ApiResponse<string>.Ok(major.ToString()));

        if (Key.Minor.TryParse(name, out var minor))
            return Ok(ApiResponse<string>.Ok(minor.ToString()));

        return NotFound(ApiResponse<object>.Fail($"Key '{name}' not found"));
    }

    /// <summary>
    ///     Get a note by name (e.g. "C#", "Bb")
    /// </summary>
    /// <param name="name">Note name</param>
    /// <returns>Note name or error</returns>
    [HttpGet("note/{name}")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public IActionResult GetNote(string name)
    {
        try
        {
            var note = Note.Accidented.Parse(name, null);
            return Ok(ApiResponse<string>.Ok(note.ToString()));
        }
        catch
        {
            return NotFound(ApiResponse<object>.Fail($"Note '{name}' not found"));
        }
    }
}
