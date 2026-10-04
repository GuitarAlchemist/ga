namespace GaChatbot.Api.Tests.Services;

using System.Net;
using GaChatbot.Api.Services;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Only a request straight from this machine may mark itself as eval traffic: the tunnel also
/// arrives over loopback, but always carries CF-Connecting-IP.
/// </summary>
[TestFixture]
public class LocalCallerTests
{
    [TestCase("127.0.0.1", null, true)]
    [TestCase("::1", null, true)]
    [TestCase("::ffff:127.0.0.1", null, true)]
    [TestCase("127.0.0.1", "203.0.113.7", false)]
    [TestCase("203.0.113.7", null, false)]
    [TestCase(null, null, false)]
    public void IsDirect_OnlyForALoopbackPeerOutsideTheTunnel(string? peer, string? cfConnectingIp, bool expected)
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = peer is null ? null : IPAddress.Parse(peer);
        if (cfConnectingIp is not null) ctx.Request.Headers["CF-Connecting-IP"] = cfConnectingIp;

        Assert.That(LocalCaller.IsDirect(ctx), Is.EqualTo(expected));
    }
}
