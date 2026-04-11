namespace GaApi.Services;

/// <summary>
///     Bridge to a Discord bot for presence, messages, and channels.
///     Stub implementation returns demo data; a real bot connection
///     can be wired in later without changing the controller contract.
/// </summary>
public interface IDiscordBridgeService
{
    /// <summary>True when a real Discord bot connection is active.</summary>
    bool IsLive { get; }

    Task<IReadOnlyList<DiscordMember>> GetOnlineMembersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DiscordMessage>> GetRecentMessagesAsync(string? channelId, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<DiscordChannel>> GetChannelsAsync(CancellationToken ct = default);
}

/// <summary>
///     A Discord guild member. Shape matches the frontend's
///     <c>DiscordMember</c> type in <c>PresencePanel.tsx</c>.
/// </summary>
public record DiscordMember(
    string Id,
    string Username,
    string? AvatarUrl,
    string Status);

/// <summary>
///     A Discord channel message. Shape matches the frontend's
///     <c>DiscordMessage</c> type in <c>PresencePanel.tsx</c>.
/// </summary>
public record DiscordMessage(
    string Id,
    string Username,
    string? AvatarUrl,
    string Content,
    string Timestamp);

/// <summary>
///     A tracked Discord channel.
/// </summary>
public record DiscordChannel(
    string Id,
    string Name,
    string Type,
    int MemberCount);
