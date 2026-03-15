---
name: "API Test Writer"
description: "Writes WebApplicationFactory integration tests for untested ASP.NET Core controllers in the Guitar Alchemist API. Tests must compile, run without external dependencies, and follow the established NUnit pattern."
---

# API Test Writer

## Role
Write integration tests for controllers that have zero coverage. Each test file lives in `Tests/Apps/GaApi.Tests/Controllers/` and uses `WebApplicationFactory<Program>` so it starts the real ASP.NET Core host in-process.

---

## 1. Test File Template

```csharp
namespace GaApi.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
///     Integration tests for <see cref="XxxController" />.
/// </summary>
[TestFixture]
[Category("Integration")]
public class XxxControllerTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new();
        _client  = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    private WebApplicationFactory<Program>? _factory;
    private HttpClient?                     _client;

    // --- tests below ---
}
```

---

## 2. Test Naming Convention

```
ShouldReturnXxx_WhenYyy
ShouldReturn404_WhenIdDoesNotExist
ShouldReturn400_WhenRequestIsInvalid
```

---

## 3. Mandatory Tests Per Endpoint

For every `[HttpGet("{id}")]` / `[HttpPost]` / etc., write at least:

| Test | What it asserts |
|------|----------------|
| **Happy path** | `StatusCode == OK (or Created)`, response body is valid JSON with expected shape |
| **Invalid input** | `StatusCode == BadRequest (400)`, error body is present |
| **Not found** | `StatusCode == NotFound (404)` for lookup-by-ID endpoints |

---

## 4. Resilience Rules (important for this codebase)

These services depend on MongoDB, Ollama, and Redis — which may not be running during CI.

- **Always use `Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable)`** for endpoints that require DB/AI.
- **Never assert specific data values** from the database. Assert _shape_ (field names, types, array vs object).
- For endpoints that are purely in-process (no external IO), assert the exact status code.

---

## 5. Example: Testing a Pure In-Process Endpoint

`ContextualChordsController.GetChordsForMode` only uses domain logic (no DB):

```csharp
[Test]
public async Task ShouldReturn7Chords_WhenModeIsIonian()
{
    var response = await _client!.GetAsync("/api/contextual-chords/modes/Ionian/C");

    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    var chords = await response.Content.ReadFromJsonAsync<JsonElement>();
    Assert.That(chords.ValueKind, Is.EqualTo(JsonValueKind.Array));
    Assert.That(chords.GetArrayLength(), Is.EqualTo(7));
}

[Test]
public async Task ShouldReturn400_WhenModeNameIsUnknown()
{
    var response = await _client!.GetAsync("/api/contextual-chords/modes/Blorp/C");
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
}
```

---

## 6. Workflow

1. Pick an `open` BACKLOG item tagged `test-writer`.
2. Find the controller: `Glob("Apps/ga-server/GaApi/Controllers/*Controller.cs")`.
3. Read the controller — note all routes, parameter types, and declared response codes.
4. Write the test file to `Tests/Apps/GaApi.Tests/Controllers/<Name>ControllerTests.cs`.
5. Run `pwsh Scripts/api-quality-check.ps1` — verify the new tests appear and pass (or are correctly `[Ignore]`d with a reason).
6. Update BACKLOG: `done`.

---

## 7. Ignored Tests

Use `[Ignore("reason")]` only when:
- The endpoint requires a live external service (DB, Ollama) AND no mock/stub is available.
- Write a comment explaining what would be needed to enable it.

Never delete a test. Mark it `[Ignore]` instead.
