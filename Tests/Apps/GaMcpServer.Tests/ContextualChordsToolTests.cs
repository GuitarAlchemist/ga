namespace GaMcpServer.Tests;

using System.Net;
using System.Net.Http;
using GaMcpServer.Tools;

/// <summary>
/// ContextualChordsTool forwards to GaApi. These tests stub GaApi's HTTP responses: GaApi itself
/// (and the voicing data behind it) is not needed to check what an MCP client sees.
/// </summary>
[TestFixture]
public sealed class ContextualChordsToolTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(respond(request));
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false) { BaseAddress = new Uri("https://localhost:7001") };
    }

    private static Task<(bool IsError, string Text)> CallGetChordVoicings(
        HttpMessageHandler handler, params (string Name, object? Value)[] arguments) =>
        McpToolInvoker.CallAsync(
            typeof(ContextualChordsTool).GetMethod(nameof(ContextualChordsTool.GetChordVoicings))!,
            new ContextualChordsTool(new StubFactory(handler)),
            arguments);

    [Test]
    public async Task GetChordVoicings_Success_ReturnsGaApiJsonAndForwardsFilters()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""[{"chordName":"C","frets":[-1,3,2,0,1,0],"difficulty":1}]""")
        });

        var (isError, text) = await CallGetChordVoicings(handler, ("chord", "C"), ("maxFret", 3));

        Assert.That(isError, Is.False);
        Assert.That(text, Does.StartWith("[{\"chordName\":\"C\""));
        Assert.That(handler.LastRequestUri!.PathAndQuery,
            Is.EqualTo("/api/contextual-chords/voicings/C?noOpenStrings=False&maxFret=3"));
    }

    [Test]
    public async Task GetChordVoicings_GaApiError_ReportsStatusAndBody()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Internal server error")
        });

        var (isError, text) = await CallGetChordVoicings(handler, ("chord", "C"), ("maxFret", 3));

        Assert.That(isError, Is.True);
        // Used to be only "An error occurred invoking 'get_chord_voicings'."
        Assert.That(text, Contains.Substring("500"));
        Assert.That(text, Contains.Substring("Internal server error"));
    }

    [Test]
    public async Task GetChordVoicings_GaApiUnreachable_SaysSo()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("No connection could be made"));

        var (isError, text) = await CallGetChordVoicings(handler, ("chord", "C"));

        Assert.That(isError, Is.True);
        Assert.That(text, Contains.Substring("GaApi"));
        Assert.That(text, Contains.Substring("https://localhost:7001"));
    }
}
