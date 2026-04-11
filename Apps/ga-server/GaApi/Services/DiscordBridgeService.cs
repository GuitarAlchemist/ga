namespace GaApi.Services;

/// <summary>
///     Stub implementation of <see cref="IDiscordBridgeService"/>.
///     Returns demo data today; replace internals with real Discord bot
///     calls (Discord.Net or HTTP Gateway) when available. The controller
///     contract does not need to change.
/// </summary>
public sealed class DiscordBridgeService(ILogger<DiscordBridgeService> logger) : IDiscordBridgeService
{
    // Flip to true once a real bot connection is configured.
    public bool IsLive => false;

    public Task<IReadOnlyList<DiscordMember>> GetOnlineMembersAsync(CancellationToken ct = default)
    {
        logger.LogDebug("DiscordBridge: serving stub presence (IsLive={IsLive})", IsLive);

        IReadOnlyList<DiscordMember> stub =
        [
            new DiscordMember("1", "stephane", null, "online"),
            new DiscordMember("2", "demerzel-bot", null, "online"),
            new DiscordMember("3", "seldon-bot", null, "idle"),
        ];
        return Task.FromResult(stub);
    }

    public Task<IReadOnlyList<DiscordMessage>> GetRecentMessagesAsync(
        string? channelId,
        int limit,
        CancellationToken ct = default)
    {
        logger.LogDebug("DiscordBridge: serving stub messages (channel={Channel}, limit={Limit})", channelId, limit);

        var now = DateTime.UtcNow;
        IReadOnlyList<DiscordMessage> stub =
        [
            new DiscordMessage(
                "m1",
                "stephane",
                null,
                "Ship the asteroid belt",
                now.AddMinutes(-5).ToString("O")),
            new DiscordMessage(
                "m2",
                "demerzel-bot",
                null,
                "Audit complete — 42 policies healthy",
                now.AddMinutes(-10).ToString("O")),
            new DiscordMessage(
                "m3",
                "seldon-bot",
                null,
                "Markov forecast updated: resilience 0.87",
                now.AddMinutes(-18).ToString("O")),
        ];

        var clamped = Math.Max(1, Math.Min(limit, stub.Count));
        IReadOnlyList<DiscordMessage> result = stub.Take(clamped).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<DiscordChannel>> GetChannelsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<DiscordChannel> stub =
        [
            new DiscordChannel("general", "general", "text", 12),
            new DiscordChannel("governance", "governance", "text", 8),
            new DiscordChannel("academy", "academy", "text", 5),
        ];
        return Task.FromResult(stub);
    }
}
