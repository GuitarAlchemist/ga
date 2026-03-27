namespace GaApi.Controllers;

using Models;
using Services;

/// <summary>
///     REST API controller for algedonic signals (pain/pleasure events
///     detected from governance belief state transitions).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AlgedonicController(
    AlgedonicSignalService algedonicSignalService,
    ILogger<AlgedonicController> logger)
    : ControllerBase
{
    /// <summary>
    ///     Get the most recent algedonic signals, sorted by timestamp descending.
    ///     Returns up to 50 signals from the persisted signal files.
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(List<AlgedonicSignalDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AlgedonicSignalDto>>> GetRecentSignals()
    {
        try
        {
            var signals = await algedonicSignalService.GetRecentSignalsAsync();
            return Ok(signals);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve recent algedonic signals");
            return Ok(new List<AlgedonicSignalDto>());
        }
    }
}
