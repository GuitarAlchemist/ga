namespace GaApi.Tests.Deploy;

using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using GaApi.Services;

/// <summary>
///     Runs the health probes that <c>Scripts/deploy-gaapi.ps1</c> sends to a new release and
///     replays them against GaApi in Production, where <c>/api/chatbot/*</c> answers 401
///     without an <c>X-Api-Key</c> header unless the request is one of the public page's own
///     calls. A probe that GaApi rejects would make the deploy roll back a healthy release.
/// </summary>
/// <remarks>
///     The probes are built by the script's own <c>Get-GaApiProbes</c>, lifted out of the
///     script and executed with pwsh, so this guards the shipped code rather than a copy.
///     Both plans are replayed: the one for a build without the public page, and the one for
///     the page as this tree ships it, so the guard holds before and after the page lands.
/// </remarks>
[TestFixture]
public class DeployGaApiProbeTests
{
    private const string ProbeFunctionName = "Get-GaApiProbes";

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Status must not depend on a live model provider.
        var provider = new Mock<IChatProviderAvailability>();
        provider.Setup(service => service.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _factory = new TestWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IChatProviderAvailability>();
                services.AddSingleton(provider.Object);
            });
        });
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    private static bool TreeShipsChatbotPage =>
        File.Exists(TestPaths.RepositoryPath("Apps", "ga-server", "GaApi", "wwwroot", "chatbot", "index.html"));

    [Test]
    public async Task ProbesForABuildWithoutThePage_PassTheProductionGate() =>
        await AssertProbesPass(hasChatbotPage: false);

    [Test]
    public async Task ProbesForThisTree_PassTheProductionGate() =>
        await AssertProbesPass(TreeShipsChatbotPage);

    [Test]
    public void StatusProbe_CarriesTheKeyWhetherOrNotThePageShips()
    {
        foreach (var hasPage in new[] { false, true })
        {
            var status = RunProbeFunction(hasPage).Single(probe => probe.Path == "/api/chatbot/status");
            Assert.That(status.Headers, Does.ContainKey("X-Api-Key"),
                $"HasChatbotPage={hasPage}: the status probe must send X-Api-Key, or Production answers 401 " +
                "on builds that do not exempt it and the deploy rolls back a healthy release.");
        }
    }

    private async Task AssertProbesPass(bool hasChatbotPage)
    {
        var probes = RunProbeFunction(hasChatbotPage);
        Assert.That(probes, Is.Not.Empty);

        foreach (var probe in probes)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, probe.Path);
            foreach (var (name, value) in probe.Headers)
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }

            using var response = await _client!.SendAsync(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"HasChatbotPage={hasChatbotPage}: GaApi in Production rejected the deploy probe {probe.Path} " +
                $"(headers: {string.Join(", ", probe.Headers.Keys)}); the deploy would roll back.");
            if (probe.Html)
            {
                Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"),
                    $"{probe.Path} must serve the page.");
            }
        }
    }

    private sealed record Probe(string Path, Dictionary<string, string> Headers, bool Html);

    private static IReadOnlyList<Probe> RunProbeFunction(bool hasChatbotPage)
    {
        const string baseUrl = "http://probe.invalid";
        var script = Path.Combine(Path.GetTempPath(), $"ga-deploy-gaapi-probes-{Guid.NewGuid():N}.ps1");
        File.WriteAllText(script, string.Join(Environment.NewLine,
            ShippedProbeFunction(),
            $"$probes = {ProbeFunctionName} -BaseUrl '{baseUrl}' -HasChatbotPage ${hasChatbotPage.ToString().ToLowerInvariant()} -KeyHeaders @{{ 'X-Api-Key' = 'test' }}",
            "ConvertTo-Json -InputObject @($probes) -Depth 4 -Compress"));

        try
        {
            using var pwsh = Process.Start(new ProcessStartInfo("pwsh")
            {
                ArgumentList = { "-NoProfile", "-NonInteractive", "-File", script },
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }) ?? throw new InvalidOperationException("pwsh produced no process handle.");

            var stdout = pwsh.StandardOutput.ReadToEndAsync();
            var stderr = pwsh.StandardError.ReadToEnd();
            pwsh.WaitForExit();
            var json = stdout.GetAwaiter().GetResult();
            Assert.That(pwsh.ExitCode, Is.Zero, $"{ProbeFunctionName} failed: {stderr}");

            using var document = JsonDocument.Parse(json);
            return document.RootElement.EnumerateArray().Select(element =>
            {
                var uri = element.GetProperty("Uri").GetString()!;
                Assert.That(uri, Does.StartWith(baseUrl));
                var headers = element.GetProperty("Headers").ValueKind == JsonValueKind.Object
                    ? element.GetProperty("Headers").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty)
                    : [];
                return new Probe(uri[baseUrl.Length..], headers, element.GetProperty("Html").GetBoolean());
            }).ToList();
        }
        catch (Win32Exception e)
        {
            // A skip would make the guard vacuous on the machines that run the deploy.
            Assert.Fail($"pwsh could not be launched, so the deploy probes were never checked: {e.Message}");
            return [];
        }
        finally
        {
            File.Delete(script);
        }
    }

    /// <summary>Lifts <c>Get-GaApiProbes</c> out of the shipped deploy script.</summary>
    private static string ShippedProbeFunction()
    {
        var path = TestPaths.RepositoryPath("Scripts", "deploy-gaapi.ps1");
        var lines = File.ReadAllLines(path);
        var start = Array.FindIndex(lines, line => line.StartsWith($"function {ProbeFunctionName}", StringComparison.Ordinal));
        Assert.That(start, Is.GreaterThanOrEqualTo(0),
            $"{path} no longer defines a column-0 '{ProbeFunctionName}' function; the deploy's health probes " +
            "must stay in that function so this guard can execute them.");

        // The function's braces are balanced and none appear inside its strings.
        var depth = 0;
        for (var i = start; i < lines.Length; i++)
        {
            depth += lines[i].Count(c => c == '{') - lines[i].Count(c => c == '}');
            if (depth == 0) return string.Join(Environment.NewLine, lines[start..(i + 1)]);
        }

        Assert.Fail($"{ProbeFunctionName} in {path} is never closed.");
        return string.Empty;
    }
}
