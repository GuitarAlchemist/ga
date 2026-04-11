namespace GaApi.Controllers;

using Services;

/// <summary>
///     Discord bridge endpoints — serve presence, messages, and channels to
///     the Prime Radiant <c>PresencePanel</c>. Currently backed by a stub
///     service; swap <see cref="IDiscordBridgeService"/> for a real bot
///     implementation to go live.
///
///     Response shapes match the frontend <c>DiscordMember</c> /
///     <c>DiscordMessage</c> types in <c>PresencePanel.tsx</c> (raw arrays).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DiscordController(
    IDiscordBridgeService discordService,
    ILogger<DiscordController> logger) : ControllerBase
{
    /// <summary>
    ///     List currently online Discord members.
    /// </summary>
    [HttpGet("presence")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPresence(CancellationToken ct)
    {
        var members = await discordService.GetOnlineMembersAsync(ct);
        Response.Headers.Append("X-Discord-Source", discordService.IsLive ? "live" : "stub");
        return Ok(members);
    }

    /// <summary>
    ///     Fetch recent messages from a Discord channel.
    /// </summary>
    /// <param name="channel">Channel id (optional — defaults to general).</param>
    /// <param name="limit">Max number of messages to return (1..100, default 20).</param>
    [HttpGet("messages")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessages(
        [FromQuery] string? channel,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        if (limit < 1) limit = 1;
        if (limit > 100) limit = 100;

        var messages = await discordService.GetRecentMessagesAsync(channel, limit, ct);
        Response.Headers.Append("X-Discord-Source", discordService.IsLive ? "live" : "stub");
        return Ok(messages);
    }

    /// <summary>
    ///     List tracked Discord channels.
    /// </summary>
    [HttpGet("channels")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChannels(CancellationToken ct)
    {
        var channels = await discordService.GetChannelsAsync(ct);
        Response.Headers.Append("X-Discord-Source", discordService.IsLive ? "live" : "stub");
        return Ok(new { channels });
    }
}
