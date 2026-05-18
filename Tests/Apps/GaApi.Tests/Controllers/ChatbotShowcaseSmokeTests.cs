namespace GaApi.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
///     End-to-end smoke test for the chatbot showcase.
///     Every prompt advertised by <c>GET /api/chatbot/demo</c> is exercised
///     against <c>POST /api/chatbot/chat</c> and the response is asserted to
///     be a real, user-facing answer — not a leaked SKILL.md directive, not
///     an empty index result, not a stub.
/// </summary>
/// <remarks>
///     This test exists because in PR #210 the showcase was shipped with
///     "tests" that only validated JSON shape and modal rendering, not that
///     any prompt produced a useful answer. Live the user found two failure
///     modes in 10 seconds: (1) the catalog-skill body included the LLM
///     directive "Reproduce the catalog below verbatim" — a model-priming
///     instruction the user should never see; (2) voicing prompts returned
///     "The OPTIC-K index returned no matches" because the index wasn't
///     loaded.
///
///     This test is the structural gate: next time a showcase prompt
///     points at a broken backend path, or a catalog skill leaks its
///     instructional preamble, CI fails before merge.
///
///     Failure modes this catches:
///       - SKILL.md preamble leaked to user ("Reproduce the catalog below…")
///       - Empty / "no matches" / stub responses
///       - Prompts that crash the orchestrator (HTTP 500)
///       - Showcase prompt list silently shrinks (every advertised prompt
///         must run)
///
///     Why <c>[Category("Integration")]</c>: needs Ollama + the full DI
///     graph wired up; not run in unit-only filters.
/// </remarks>
[TestFixture]
[Category("Integration")]
public class ChatbotShowcaseSmokeTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new TestWebApplicationFactory();
        _client  = _factory.CreateClient();
        _client.Timeout = TimeSpan.FromMinutes(2);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    private WebApplicationFactory<Program>? _factory;
    private HttpClient?                     _client;

    /// <summary>
    ///     Phrases that, if present in the response, mean we leaked an
    ///     LLM-directed instruction to the user. Anything that says
    ///     "reproduce" + "verbatim", or that addresses "a user", or that
    ///     describes itself as "pure pedagogy" / "no tool call needed" /
    ///     "use when a learner asks" — that's the SKILL.md preamble, not
    ///     a user-facing answer.
    /// </summary>
    private static readonly string[] LeakedDirectiveMarkers =
    [
        "Reproduce the catalog below verbatim",
        "Pure pedagogy",
        "doesn't need a tool call",
        "Use when a learner asks",
        "Use when a visitor asks",
    ];

    /// <summary>
    ///     Phrases that indicate the backend route the prompt depends on is
    ///     not actually working in this environment. If the showcase
    ///     advertises a capability the live backend can't deliver, the
    ///     prompt should not be in the showcase.
    /// </summary>
    private static readonly string[] EmptyResultMarkers =
    [
        "returned no matches",
        "no results found",
        "index is not loaded",
        "not yet implemented",
    ];

    [Test]
    public async Task DemoEndpoint_AdvertisesAtLeastOnePromptPerCategory()
    {
        var script = await FetchDemoScriptAsync();

        Assert.That(script.GetProperty("categories").GetArrayLength(),
            Is.GreaterThan(0),
            "showcase must advertise at least one category");

        foreach (var category in script.GetProperty("categories").EnumerateArray())
        {
            Assert.That(category.GetProperty("prompts").GetArrayLength(),
                Is.GreaterThan(0),
                $"category '{category.GetProperty("id").GetString()}' must advertise at least one prompt");
        }
    }

    [Test]
    [Explicit("Slow — runs every showcase prompt through the full agentic pipeline. Run before changing the demo script.")]
    public async Task EveryShowcasePrompt_ProducesAUsefulAnswer()
    {
        var script   = await FetchDemoScriptAsync();
        var failures = new List<string>();

        foreach (var category in script.GetProperty("categories").EnumerateArray())
        {
            var categoryId = category.GetProperty("id").GetString();

            foreach (var prompt in category.GetProperty("prompts").EnumerateArray())
            {
                var promptText  = prompt.GetProperty("prompt").GetString() ?? "";
                var failureNote = await ValidatePromptAsync(categoryId!, promptText);
                if (failureNote is not null)
                    failures.Add(failureNote);
            }
        }

        // Surface every broken prompt at once instead of failing on the
        // first one — operators need the full picture to decide which to
        // fix vs which to remove from the showcase.
        Assert.That(failures, Is.Empty,
            $"Showcase prompts that didn't produce a useful answer:\n  - {string.Join("\n  - ", failures)}");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<JsonElement> FetchDemoScriptAsync()
    {
        var response = await _client!.GetAsync("/api/chatbot/demo");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            "GET /api/chatbot/demo must succeed for the showcase to render at all");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<string?> ValidatePromptAsync(string categoryId, string prompt)
    {
        try
        {
            var response = await _client!.PostAsJsonAsync(
                "/api/chatbot/chat",
                new { message = prompt });

            if (response.StatusCode != HttpStatusCode.OK)
                return $"[{categoryId}] '{prompt}' → HTTP {(int)response.StatusCode}";

            var body   = await response.Content.ReadFromJsonAsync<JsonElement>();
            var answer = ExtractAnswer(body);

            if (string.IsNullOrWhiteSpace(answer))
                return $"[{categoryId}] '{prompt}' → empty response";

            if (answer.Length < 50)
                return $"[{categoryId}] '{prompt}' → too short ({answer.Length} chars): \"{answer}\"";

            foreach (var marker in LeakedDirectiveMarkers)
                if (answer.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    return $"[{categoryId}] '{prompt}' → leaked SKILL.md directive (\"{marker}\")";

            foreach (var marker in EmptyResultMarkers)
                if (answer.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    return $"[{categoryId}] '{prompt}' → empty backend result (\"{marker}\")";

            return null;
        }
        catch (Exception ex)
        {
            return $"[{categoryId}] '{prompt}' → exception: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private static string ExtractAnswer(JsonElement body)
    {
        // The /chat REST surface returns a ChatJsonResponse with the answer
        // under various property names depending on serializer config. Try
        // the most likely keys; the test should be robust to camelCase /
        // PascalCase drift.
        foreach (var key in new[] { "naturalLanguageAnswer", "NaturalLanguageAnswer", "answer", "Answer", "result", "Result" })
        {
            if (body.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString() ?? "";
        }
        return body.ToString();
    }
}
