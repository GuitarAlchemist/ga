namespace GA.Business.Core.Tests.AI;

using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

/// <summary>
///     Integration tests for the chatbot API with detailed request/response logging
/// </summary>
[TestFixture]
public class ChatbotIntegrationTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Create HttpClientHandler that accepts any SSL certificate (for localhost testing)
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(_apiBaseUrl),
            Timeout = TimeSpan.FromSeconds(_timeoutSeconds)
        };

        TestContext.WriteLine("=".PadRight(80, '='));
        TestContext.WriteLine("CHATBOT INTEGRATION TESTS");
        TestContext.WriteLine("=".PadRight(80, '='));
        TestContext.WriteLine($"API Base URL: {_apiBaseUrl}");
        TestContext.WriteLine($"Timeout: {_timeoutSeconds}s");
        TestContext.WriteLine("=".PadRight(80, '='));
        TestContext.WriteLine("");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _httpClient?.Dispose();
    }

    [SetUp]
    public void SetUp()
    {
        TestContext.WriteLine("");
        TestContext.WriteLine("─".PadRight(80, '─'));
        TestContext.WriteLine($"TEST: {TestContext.CurrentContext.Test.Name}");
        TestContext.WriteLine("─".PadRight(80, '─'));
    }

    [TearDown]
    public void TearDown()
    {
        TestContext.WriteLine("─".PadRight(80, '─'));
        TestContext.WriteLine("TEST COMPLETED");
        TestContext.WriteLine("─".PadRight(80, '─'));
    }

    private HttpClient? _httpClient;
    private const string _apiBaseUrl = "https://localhost:7001";
    private const int _timeoutSeconds = 120; // 2 minutes for LLM responses

    private void LogRequest(string method, string endpoint, object? body = null)
    {
        TestContext.WriteLine("");
        TestContext.WriteLine("📤 REQUEST:");
        TestContext.WriteLine($"   Method:    {method}");
        TestContext.WriteLine($"   Endpoint:  {endpoint}");
        TestContext.WriteLine($"   Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");

        if (body != null)
        {
            var json = JsonSerializer.Serialize(body, new JsonSerializerOptions { WriteIndented = true });
            TestContext.WriteLine("   Body:");
            foreach (var line in json.Split('\n'))
            {
                TestContext.WriteLine($"      {line}");
            }
        }

        TestContext.WriteLine("");
    }

    private void LogResponse(HttpResponseMessage response, string? body, long durationMs)
    {
        TestContext.WriteLine("📥 RESPONSE:");
        TestContext.WriteLine($"   Status:    {(int)response.StatusCode} {response.ReasonPhrase}");
        TestContext.WriteLine($"   Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        TestContext.WriteLine($"   Duration:  {durationMs}ms ({durationMs / 1000.0:F2}s)");

        TestContext.WriteLine("   Headers:");
        foreach (var header in response.Headers)
        {
            TestContext.WriteLine($"      {header.Key}: {string.Join(", ", header.Value)}");
        }

        if (!string.IsNullOrEmpty(body))
        {
            TestContext.WriteLine("   Body:");
            try
            {
                var jsonDoc = JsonDocument.Parse(body);
                var formatted = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
                foreach (var line in formatted.Split('\n'))
                {
                    TestContext.WriteLine($"      {line}");
                }
            }
            catch
            {
                foreach (var line in body.Split('\n'))
                {
                    TestContext.WriteLine($"      {line}");
                }
            }
        }

        TestContext.WriteLine("");
    }

    [Test]
    [Category("Chatbot")]
    [Category("Integration")]
    public async Task ChatbotStatus_ShouldReturnAvailable()
    {
        // Arrange
        var endpoint = "/api/chatbot/status";
        LogRequest("GET", endpoint);

        // Act
        var startTime = DateTime.Now;
        var response = await _httpClient!.GetAsync(endpoint);
        var duration = (DateTime.Now - startTime).TotalMilliseconds;
        var body = await response.Content.ReadAsStringAsync();

        LogResponse(response, body, (long)duration);

        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True, "Status endpoint should return success");

        var json = JsonDocument.Parse(body);
        var isAvailable = json.RootElement.GetProperty("isAvailable").GetBoolean();
        var message = json.RootElement.GetProperty("message").GetString();

        TestContext.WriteLine("✅ ASSERTIONS:");
        TestContext.WriteLine($"   Chatbot Available: {isAvailable}");
        TestContext.WriteLine($"   Message: {message}");

        Assert.That(isAvailable, Is.True, "Chatbot should be available");
        Assert.That(message, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    [Category("Chatbot")]
    [Category("Integration")]
    public async Task Chat_BasicMusicTheory_ShouldReturnCorrectResponse()
    {
        // Arrange
        var endpoint = "/api/chatbot/chat";
        var request = new
        {
            message = "What notes are in a C major chord? Answer in one sentence.",
            useSemanticSearch = false
        };

        LogRequest("POST", endpoint, request);

        // Act
        var startTime = DateTime.Now;
        var response = await _httpClient!.PostAsJsonAsync(endpoint, request);
        var duration = (DateTime.Now - startTime).TotalMilliseconds;
        var body = await response.Content.ReadAsStringAsync();

        LogResponse(response, body, (long)duration);

        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True, "Chat endpoint should return success");

        var json = JsonDocument.Parse(body);
        var message = json.RootElement.GetProperty("message").GetString();
        var timestamp = json.RootElement.GetProperty("timestamp").GetDateTime();

        TestContext.WriteLine("✅ ASSERTIONS:");
        TestContext.WriteLine($"   Response Length: {message?.Length ?? 0} characters");
        TestContext.WriteLine($"   Timestamp: {timestamp:yyyy-MM-dd HH:mm:ss}");
        TestContext.WriteLine($"   Response Time: {duration:F0}ms");
        TestContext.WriteLine($"   Response: {message}");

        Assert.That(message, Is.Not.Null.And.Not.Empty, "Response should not be empty");
        Assert.That(message!.ToLower(), Does.Contain("c").Or.Contains("chord").Or.Contains("major"),
            "Response should mention C, chord, or major");
    }

    [Test]
    [Category("Chatbot")]
    [Category("Integration")]
    public async Task Chat_WithSemanticSearch_ShouldReturnEnhancedResponse()
    {
        // Arrange
        var endpoint = "/api/chatbot/chat";
        var request = new
        {
            message = "Show me some jazz chords",
            useSemanticSearch = true
        };

        LogRequest("POST", endpoint, request);

        // Act
        var startTime = DateTime.Now;
        var response = await _httpClient!.PostAsJsonAsync(endpoint, request);
        var duration = (DateTime.Now - startTime).TotalMilliseconds;
        var body = await response.Content.ReadAsStringAsync();

        LogResponse(response, body, (long)duration);

        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True);

        var json = JsonDocument.Parse(body);
        var message = json.RootElement.GetProperty("message").GetString();

        TestContext.WriteLine("✅ ASSERTIONS:");
        TestContext.WriteLine($"   Response Length: {message?.Length ?? 0} characters");
        TestContext.WriteLine("   Semantic Search: ENABLED");
        TestContext.WriteLine($"   Response: {message}");

        Assert.That(message, Is.Not.Null.And.Not.Empty);
        Assert.That(message!.ToLower(), Does.Contain("jazz").Or.Contains("chord"));
    }

    [Test]
    [Category("Chatbot")]
    [Category("Integration")]
    public async Task Chat_GuitarTechnique_ShouldReturnDetailedExplanation()
    {
        // Arrange
        var endpoint = "/api/chatbot/chat";
        var request = new
        {
            message = "Explain barre chords in 2 sentences",
            useSemanticSearch = false
        };

        LogRequest("POST", endpoint, request);

        // Act
        var startTime = DateTime.Now;
        var response = await _httpClient!.PostAsJsonAsync(endpoint, request);
        var duration = (DateTime.Now - startTime).TotalMilliseconds;
        var body = await response.Content.ReadAsStringAsync();

        LogResponse(response, body, (long)duration);

        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True);

        var json = JsonDocument.Parse(body);
        var message = json.RootElement.GetProperty("message").GetString();

        TestContext.WriteLine("✅ ASSERTIONS:");
        TestContext.WriteLine($"   Response: {message}");

        Assert.That(message, Is.Not.Null.And.Not.Empty);
        Assert.That(message!.ToLower(), Does.Contain("barre").Or.Contains("bar").Or.Contains("finger"));
    }

    [Test]
    [Category("Chatbot")]
    [Category("Integration")]
    public async Task Chat_ScaleTheory_ShouldReturnCorrectNotes()
    {
        // Arrange
        var endpoint = "/api/chatbot/chat";
        var request = new
        {
            message = "List the notes in C major scale",
            useSemanticSearch = false
        };

        LogRequest("POST", endpoint, request);

        // Act
        var startTime = DateTime.Now;
        var response = await _httpClient!.PostAsJsonAsync(endpoint, request);
        var duration = (DateTime.Now - startTime).TotalMilliseconds;
        var body = await response.Content.ReadAsStringAsync();

        LogResponse(response, body, (long)duration);

        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True);

        var json = JsonDocument.Parse(body);
        var message = json.RootElement.GetProperty("message").GetString();

        // Check for notes in C major scale
        var expectedNotes = new[] { "C", "D", "E", "F", "G", "A", "B" };
        var foundNotes = expectedNotes.Where(note => message!.Contains(note)).ToList();

        TestContext.WriteLine("✅ ASSERTIONS:");
        TestContext.WriteLine($"   Expected Notes: {string.Join(", ", expectedNotes)}");
        TestContext.WriteLine($"   Found Notes: {string.Join(", ", foundNotes)}");
        TestContext.WriteLine($"   Response: {message}");

        Assert.That(message, Is.Not.Null.And.Not.Empty);
        Assert.That(foundNotes.Count, Is.GreaterThanOrEqualTo(5),
            "Should mention at least 5 notes from C major scale");
    }

    [Test]
    [Category("Chatbot")]
    [Category("Integration")]
    public async Task ChatbotExamples_ShouldReturnExampleQueries()
    {
        // Arrange
        var endpoint = "/api/chatbot/examples";
        LogRequest("GET", endpoint);

        // Act
        var startTime = DateTime.Now;
        var response = await _httpClient!.GetAsync(endpoint);
        var duration = (DateTime.Now - startTime).TotalMilliseconds;
        var body = await response.Content.ReadAsStringAsync();

        LogResponse(response, body, (long)duration);

        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True);

        var examples = JsonSerializer.Deserialize<string[]>(body);

        TestContext.WriteLine("✅ ASSERTIONS:");
        TestContext.WriteLine($"   Example Count: {examples?.Length ?? 0}");
        if (examples != null)
        {
            TestContext.WriteLine("   Examples:");
            foreach (var example in examples)
            {
                TestContext.WriteLine($"      - {example}");
            }
        }

        Assert.That(examples, Is.Not.Null.And.Not.Empty);
        Assert.That(examples!.Length, Is.GreaterThan(0));
    }

    [Test]
    [Category("Chatbot")]
    [Category("Integration")]
    public async Task Chat_ChordProgression_ShouldReturnProgressionInfo()
    {
        // Arrange
        var endpoint = "/api/chatbot/chat";
        var request = new
        {
            message = "Tell me about the I-V-vi-IV progression",
            useSemanticSearch = false
        };

        LogRequest("POST", endpoint, request);

        // Act
        var startTime = DateTime.Now;
        var response = await _httpClient!.PostAsJsonAsync(endpoint, request);
        var duration = (DateTime.Now - startTime).TotalMilliseconds;
        var body = await response.Content.ReadAsStringAsync();

        LogResponse(response, body, (long)duration);

        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True);

        var json = JsonDocument.Parse(body);
        var message = json.RootElement.GetProperty("message").GetString();

        TestContext.WriteLine("✅ ASSERTIONS:");
        TestContext.WriteLine($"   Response: {message}");

        Assert.That(message, Is.Not.Null.And.Not.Empty);
        Assert.That(message!.ToLower(), Does.Contain("progression").Or.Contains("chord"));
    }

    [Test]
    [Category("Chatbot")]
    [Category("Integration")]
    public async Task Chat_MusicTheoryExplanation_ShouldReturnDetailedExplanation()
    {
        // Arrange
        var endpoint = "/api/chatbot/chat";
        var request = new
        {
            message = "Explain the circle of fifths",
            useSemanticSearch = false
        };

        LogRequest("POST", endpoint, request);

        // Act
        var startTime = DateTime.Now;
        var response = await _httpClient!.PostAsJsonAsync(endpoint, request);
        var duration = (DateTime.Now - startTime).TotalMilliseconds;
        var body = await response.Content.ReadAsStringAsync();

        LogResponse(response, body, (long)duration);

        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True);

        var json = JsonDocument.Parse(body);
        var message = json.RootElement.GetProperty("message").GetString();

        TestContext.WriteLine("✅ ASSERTIONS:");
        TestContext.WriteLine($"   Response Length: {message?.Length ?? 0} characters");
        TestContext.WriteLine($"   Response: {message}");

        Assert.That(message, Is.Not.Null.And.Not.Empty);
        Assert.That(message!.ToLower(), Does.Contain("circle").Or.Contains("fifth").Or.Contains("key"));
        Assert.That(message!.Length, Is.GreaterThan(50), "Explanation should be detailed");
    }

    [Test]
    [Category("Chatbot")]
    [Category("Integration")]
    public async Task Chat_ChordDiagram_ShouldReturnDiagramInfo()
    {
        // Arrange
        var endpoint = "/api/chatbot/chat";
        var request = new
        {
            message = "Show me how to play a G major chord",
            useSemanticSearch = false
        };

        LogRequest("POST", endpoint, request);

        // Act
        var startTime = DateTime.Now;
        var response = await _httpClient!.PostAsJsonAsync(endpoint, request);
        var duration = (DateTime.Now - startTime).TotalMilliseconds;
        var body = await response.Content.ReadAsStringAsync();

        LogResponse(response, body, (long)duration);

        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True);

        var json = JsonDocument.Parse(body);
        var message = json.RootElement.GetProperty("message").GetString();

        TestContext.WriteLine("✅ ASSERTIONS:");
        TestContext.WriteLine($"   Response: {message}");

        Assert.That(message, Is.Not.Null.And.Not.Empty);
        Assert.That(message!.ToLower(), Does.Contain("g").Or.Contains("chord").Or.Contains("finger"));
    }

    [Test]
    [Category("Chatbot")]
    [Category("Integration")]
    public async Task Chat_ErrorHandling_ShouldHandleInvalidInput()
    {
        // Arrange
        var endpoint = "/api/chatbot/chat";
        var request = new
        {
            message = "",
            useSemanticSearch = false
        };

        LogRequest("POST", endpoint, request);

        // Act
        var startTime = DateTime.Now;
        var response = await _httpClient!.PostAsJsonAsync(endpoint, request);
        var duration = (DateTime.Now - startTime).TotalMilliseconds;
        var body = await response.Content.ReadAsStringAsync();

        LogResponse(response, body, (long)duration);

        // Assert - Should handle gracefully
        TestContext.WriteLine("✅ ASSERTIONS:");
        TestContext.WriteLine($"   Status Code: {response.StatusCode}");
        TestContext.WriteLine($"   Response: {body}");

        // Either returns an error status or a helpful message
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK)
            .Or.EqualTo(HttpStatusCode.BadRequest));
    }
}
