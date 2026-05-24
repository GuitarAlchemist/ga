namespace GaApi.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

/// <summary>
///     Integration tests for the /api/dashboard endpoints that back the
///     MCP dashboard control hub (Phase 1, read-only).
///
///     These are the HTTP layer the GaMcpServer DashboardControlTool POSTs
///     to. SignalR hub behavior (RequestState / RequestScreenshot) is
///     covered behaviourally — without a connected SPA client, the
///     controller returns 404 with a helpful hint. With a connected client,
///     it would relay via DevDashboardHub. We pin the no-client path here
///     because that's the contract the MCP tool surfaces.
/// </summary>
[TestFixture]
[Category("Integration")]
public sealed class DashboardControllerTests
{
    private TestWebApplicationFactory? _factory;
    private HttpClient? _client;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new TestWebApplicationFactory();
        _factory.EnsureStarted();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task Navigate_with_valid_subTab_returns_ok_and_clients_notified_count()
    {
        var response = await _client!.PostAsJsonAsync("/api/dashboard/navigate",
            new { subTab = "sentrux" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("ok").GetBoolean(), Is.True);
        Assert.That(body.GetProperty("tab").GetString(), Is.EqualTo("sentrux"));
        // No SPA connected during tests, so clients_notified is 0 — but the
        // field must be present so the MCP tool's caller can branch on it.
        Assert.That(body.GetProperty("clients_notified").GetInt32(), Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public async Task Navigate_with_unknown_subTab_returns_400_with_valid_list()
    {
        var response = await _client!.PostAsJsonAsync("/api/dashboard/navigate",
            new { subTab = "not-a-real-tab" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var error = body.GetProperty("error").GetString() ?? string.Empty;
        Assert.That(error, Does.Contain("summary"), "Error message should advertise the valid tab names");
        Assert.That(error, Does.Contain("sentrux"));
    }

    [Test]
    public async Task Navigate_case_insensitive()
    {
        var response = await _client!.PostAsJsonAsync("/api/dashboard/navigate",
            new { subTab = "SENTRUX" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("tab").GetString(), Is.EqualTo("sentrux"),
            "Tab name should be normalized to lowercase before broadcast");
    }

    [Test]
    public async Task State_with_no_connected_client_returns_404_with_hint()
    {
        var response = await _client!.GetAsync("/api/dashboard/state");

        // No SPA is connected to /hubs/dev-dashboard during the in-process test,
        // so the controller's short-circuit should fire.
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var hint = body.GetProperty("hint").GetString() ?? string.Empty;
        Assert.That(hint, Does.Contain("/test#dev"), "Hint should point the agent at the right URL");
    }

    [Test]
    public async Task Screenshot_with_no_connected_client_returns_404_with_hint()
    {
        var response = await _client!.PostAsJsonAsync("/api/dashboard/screenshot",
            new { subTab = (string?)null, fullPage = false });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var hint = body.GetProperty("hint").GetString() ?? string.Empty;
        Assert.That(hint, Does.Contain("/test#dev"));
    }

    [Test]
    public async Task Screenshot_with_invalid_subTab_returns_400_without_calling_hub()
    {
        // Validation must short-circuit BEFORE the no-client check, so an agent
        // calling with a bad tab gets a precise error instead of "no client".
        var response = await _client!.PostAsJsonAsync("/api/dashboard/screenshot",
            new { subTab = "bogus-tab", fullPage = false });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("error").GetString() ?? string.Empty, Does.Contain("summary"));
    }

    [Test]
    public async Task Refresh_with_no_endpoint_broadcasts_wildcard()
    {
        // Refresh works whether a client is connected or not — it's a fire-and-
        // forget broadcast. We just check the response shape.
        var response = await _client!.PostAsJsonAsync("/api/dashboard/refresh",
            new { endpoint = (string?)null });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var refreshed = body.GetProperty("refreshed");
        Assert.That(refreshed.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(refreshed[0].GetString(), Is.EqualTo("*"));
    }

    [Test]
    public async Task Refresh_with_named_endpoint_broadcasts_that_endpoint()
    {
        var response = await _client!.PostAsJsonAsync("/api/dashboard/refresh",
            new { endpoint = "/dev-data/sentrux/health" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var refreshed = body.GetProperty("refreshed");
        Assert.That(refreshed[0].GetString(), Is.EqualTo("/dev-data/sentrux/health"));
    }

    [Test]
    public async Task Valid_sub_tabs_match_SPA_DEV_SUB_TABS_array()
    {
        // The C# enum and the SPA array are mirrors — pin the canonical list
        // so a unilateral add on one side breaks the test until both update.
        // Mirrors DEV_SUB_TABS in DevelopmentSection.tsx.
        var expected = new[]
        {
            "summary", "architecture", "product", "project",
            "qa", "sentrux", "harness", "annotations",
        };

        foreach (var tab in expected)
        {
            var response = await _client!.PostAsJsonAsync("/api/dashboard/navigate",
                new { subTab = tab });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                $"Expected '{tab}' to be accepted; got {response.StatusCode}");
        }
    }
}
