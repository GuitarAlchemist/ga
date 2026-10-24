namespace GaChatbot.Api.Services;

using System.Net;

/// <summary>
/// Whether a request came straight from this machine rather than through the public tunnel.
/// </summary>
/// <remarks>
/// The cloudflared connector also reaches this host over loopback, but Cloudflare stamps
/// <c>CF-Connecting-IP</c> on everything it forwards and a client cannot remove it; Program.cs
/// tells tunnelled requests apart by the same header. A loopback peer without it is a script on
/// this machine, such as <c>Scripts/theory-qa/run_eval.py</c>, so only such a request may mark
/// itself as eval traffic (<see cref="GA.Business.ML.Agents.Intents.RoutingTelemetryLog.IsSyntheticTraffic"/>).
/// </remarks>
public static class LocalCaller
{
    public static bool IsDirect(HttpContext context) =>
        context.Connection.RemoteIpAddress is { } ip
        && IPAddress.IsLoopback(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip)
        && string.IsNullOrEmpty(context.Request.Headers["CF-Connecting-IP"].ToString());
}
