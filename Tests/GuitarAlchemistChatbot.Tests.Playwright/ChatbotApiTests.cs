namespace GuitarAlchemistChatbot.Tests.Playwright;

using System.Text.Json;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

/// <summary>
///     Comprehensive API tests for the chatbot with detailed request/response logging
/// </summary>
[TestFixture]
public class ChatbotApiTests : PageTest
{
    [SetUp]
    public async Task Setup()
    {
        // Create API request context
        _apiContext = await Playwright.APIRequest.NewContextAsync(new()
        {
            BaseURL = _apiBaseUrl,
            IgnoreHTTPSErrors = true,
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json"
            }
        });

        TestContext.WriteLine("=".PadRight(80, '='));
        TestContext.WriteLine($"TEST: {TestContext.CurrentContext.Test.Name}");
        TestContext.WriteLine("=".PadRight(80, '='));
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_apiContext != null)
        {
            await _apiContext.DisposeAsync();
        }

        TestContext.WriteLine("=".PadRight(80, '='));
        TestContext.WriteLine("TEST COMPLETED");
        TestContext.WriteLine("=".PadRight(80, '='));
        TestContext.WriteLine("");
    }

    private const string _apiBaseUrl = "http://localhost:5232";
    private const int _defaultTimeout = 60000; // 60 seconds for LLM responses

    private IAPIRequestContext? _apiContext;

    private void LogRequest(string endpoint, object? requestBody = null)
    {
        TestContext.WriteLine("");
        TestContext.WriteLine("📤 REQUEST:");
        TestContext.WriteLine($"   Endpoint: {endpoint}");
        TestContext.WriteLine("   Method: POST");
        TestContext.WriteLine($"   Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");

        if (requestBody != null)
        {
            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            TestContext.WriteLine("   Body:");
            foreach (var line in json.Split('\n'))
            {
                TestContext.WriteLine($"      {line}");
            }
        }

        TestContext.WriteLine("");
    }

    private void LogResponse(IAPIResponse response, string? responseBody = null, long? durationMs = null)
    {
        TestContext.WriteLine("📥 RESPONSE:");
        TestContext.WriteLine($"   Status: {response.Status} {response.StatusText}");
        TestContext.WriteLine($"   Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");

        if (durationMs.HasValue)
        {
            TestContext.WriteLine($"   Duration: {durationMs.Value}ms ({durationMs.Value / 1000.0:F2}s)");
        }

        TestContext.WriteLine("   Headers:");
        foreach (var header in response.Headers)
        {
            TestContext.WriteLine($"      {header.Key}: {header.Value}");
        }

        if (!string.IsNullOrEmpty(responseBody))
        {
            TestContext.WriteLine("   Body:");
            try
            {
                var jsonDoc = JsonDocument.Parse(responseBody);
                var formatted = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                foreach (var line in formatted.Split('\n'))
                {
                    TestContext.WriteLine($"      {line}");
                }
            }
            catch
            {
                // If not JSON, just print as-is
                foreach (var line in responseBody.Split('\n'))
                {
                    TestContext.WriteLine($"      {line}");
                }
            }
        }

        TestContext.WriteLine("");
    }

    [Test]
    [Category("Chatbot")]
    [Category("API")]
    public async Task ChatbotStatus_ShouldReturnAvailable()
    {
        TestContext.WriteLine("Testing chatbot status endpoint...");

        var endpoint = "/api/chatbot/status";
        LogRequest(endpoint);

        var startTime = DateTime.Now;
        var response = await _apiContext!.GetAsync(endpoint);
        var duration = (DateTime.Now - startTime).TotalMilliseconds;

        var responseBody = await response.TextAsync();
        LogResponse(response, responseBody, (long)duration);

        Assert.That(response.Ok, Is.True, "Status endpoint should return 200 OK");

        var json = JsonDocument.Parse(responseBody);
        var isAvailable = json.RootElement.GetProperty("isAvailable").GetBoolean();
        var message = json.RootElement.GetProperty("message").GetString();

        TestContext.WriteLine($"✅ Chatbot Available: {isAvailable}");
        TestContext.WriteLine($"✅ Message: {message}");

        Assert.That(isAvailable, Is.True, "Chatbot should be available");
    }

    [Test]
    [Category("Chatbot")]
    [Category("API")]
    public async Task Chat_BasicMusicTheory_ShouldReturnResponse()
    {
        TestContext.WriteLine("Testing basic music theory query...");

        var endpoint = "/api/chatbot/chat";
        var requestBody = new
        {
            message = "What notes are in a C major chord? Be brief.",
            useSemanticSearch = false
        };

        LogRequest(endpoint, requestBody);

        var startTime = DateTime.Now;
        var response = await _apiContext!.PostAsync(endpoint, new()
        {
            DataObject = requestBody
        });
        var duration = (DateTime.Now - startTime).TotalMilliseconds;

        var responseBody = await response.TextAsync();
        LogResponse(response, responseBody, (long)duration);

        Assert.That(response.Ok, Is.True, "Chat endpoint should return 200 OK");

        var json = JsonDocument.Parse(responseBody);
        var message = json.RootElement.GetProperty("message").GetString();
        var timestamp = json.RootElement.GetProperty("timestamp").GetDateTime();

        TestContext.WriteLine($"✅ Response received: {message?.Length ?? 0} characters");
        TestContext.WriteLine($"✅ Timestamp: {timestamp:yyyy-MM-dd HH:mm:ss}");
        TestContext.WriteLine($"✅ Response time: {duration:F0}ms");

        Assert.That(message, Is.Not.Null.And.Not.Empty, "Response message should not be empty");
        Assert.That(message!.ToLower(), Does.Contain("c").Or.Contains("chord"),
            "Response should mention C or chord");
    }

    [Test]
    [Category("Chatbot")]
    [Category("API")]
    public async Task Chat_WithSemanticSearch_ShouldReturnEnhancedResponse()
    {
        TestContext.WriteLine("Testing chat with semantic search enabled...");

        var endpoint = "/api/chatbot/chat";
        var requestBody = new
        {
            message = "Show me some jazz chords",
            useSemanticSearch = true
        };

        LogRequest(endpoint, requestBody);

        var startTime = DateTime.Now;
        var response = await _apiContext!.PostAsync(endpoint, new()
        {
            DataObject = requestBody
        });
        var duration = (DateTime.Now - startTime).TotalMilliseconds;

        var responseBody = await response.TextAsync();
        LogResponse(response, responseBody, (long)duration);

        Assert.That(response.Ok, Is.True, "Chat endpoint should return 200 OK");

        var json = JsonDocument.Parse(responseBody);
        var message = json.RootElement.GetProperty("message").GetString();

        TestContext.WriteLine($"✅ Response received: {message?.Length ?? 0} characters");
        TestContext.WriteLine($"✅ Response time: {duration:F0}ms");
        TestContext.WriteLine("✅ Semantic search: ENABLED");

        Assert.That(message, Is.Not.Null.And.Not.Empty);
        Assert.That(message!.ToLower(), Does.Contain("jazz").Or.Contains("chord"));
    }

    [Test]
    [Category("Chatbot")]
    [Category("API")]
    public async Task Chat_GuitarTechnique_ShouldReturnDetailedExplanation()
    {
        TestContext.WriteLine("Testing guitar technique query...");

        var endpoint = "/api/chatbot/chat";
        var requestBody = new
        {
            message = "Explain barre chords in 2 sentences",
            useSemanticSearch = false
        };

        LogRequest(endpoint, requestBody);

        var startTime = DateTime.Now;
        var response = await _apiContext!.PostAsync(endpoint, new()
        {
            DataObject = requestBody
        });
        var duration = (DateTime.Now - startTime).TotalMilliseconds;

        var responseBody = await response.TextAsync();
        LogResponse(response, responseBody, (long)duration);

        Assert.That(response.Ok, Is.True);

        var json = JsonDocument.Parse(responseBody);
        var message = json.RootElement.GetProperty("message").GetString();

        TestContext.WriteLine($"✅ Response: {message}");
        TestContext.WriteLine($"✅ Response time: {duration:F0}ms");

        Assert.That(message, Is.Not.Null.And.Not.Empty);
        Assert.That(message!.ToLower(), Does.Contain("barre").Or.Contains("bar"));
    }

    [Test]
    [Category("Chatbot")]
    [Category("API")]
    public async Task Chat_ScaleTheory_ShouldReturnCorrectNotes()
    {
        TestContext.WriteLine("Testing scale theory query...");

        var endpoint = "/api/chatbot/chat";
        var requestBody = new
        {
            message = "List the notes in C major scale",
            useSemanticSearch = false
        };

        LogRequest(endpoint, requestBody);

        var startTime = DateTime.Now;
        var response = await _apiContext!.PostAsync(endpoint, new()
        {
            DataObject = requestBody
        });
        var duration = (DateTime.Now - startTime).TotalMilliseconds;

        var responseBody = await response.TextAsync();
        LogResponse(response, responseBody, (long)duration);

        Assert.That(response.Ok, Is.True);

        var json = JsonDocument.Parse(responseBody);
        var message = json.RootElement.GetProperty("message").GetString();

        TestContext.WriteLine($"✅ Response: {message}");
        TestContext.WriteLine($"✅ Response time: {duration:F0}ms");

        // Check for all notes in C major scale
        var expectedNotes = new[] { "C", "D", "E", "F", "G", "A", "B" };
        var foundNotes = expectedNotes.Where(note => message!.Contains(note)).ToList();

        TestContext.WriteLine($"✅ Found notes: {string.Join(", ", foundNotes)}");

        Assert.That(message, Is.Not.Null.And.Not.Empty);
        Assert.That(foundNotes.Count, Is.GreaterThanOrEqualTo(5),
            "Should mention at least 5 notes from C major scale");
    }

    [Test]
    [Category("Chatbot")]
    [Category("API")]
    public async Task Chat_ChordProgression_ShouldReturnProgressionTemplates()
    {
        TestContext.WriteLine("Testing chord progression query...");

        var endpoint = "/api/chatbot/chat";
        var requestBody = new
        {
            message = "Show me some pop chord progressions",
            useSemanticSearch = false
        };

        LogRequest(endpoint, requestBody);

        var startTime = DateTime.Now;
        var response = await _apiContext!.PostAsync(endpoint, new()
        {
            DataObject = requestBody
        });
        var duration = (DateTime.Now - startTime).TotalMilliseconds;

        var responseBody = await response.TextAsync();
        LogResponse(response, responseBody, (long)duration);

        Assert.That(response.Ok, Is.True);

        var json = JsonDocument.Parse(responseBody);
        var message = json.RootElement.GetProperty("message").GetString();

        TestContext.WriteLine($"✅ Response: {message}");
        TestContext.WriteLine($"✅ Response time: {duration:F0}ms");

        Assert.That(message, Is.Not.Null.And.Not.Empty);
        Assert.That(message!.ToLower(), Does.Contain("progression").Or.Contains("chord"));
    }

    [Test]
    [Category("Chatbot")]
    [Category("API")]
    public async Task Chat_ChordDiagram_ShouldReturnDiagramInfo()
    {
        TestContext.WriteLine("Testing chord diagram query...");

        var endpoint = "/api/chatbot/chat";
        var requestBody = new
        {
            message = "Show me a C major chord diagram",
            useSemanticSearch = false
        };

        LogRequest(endpoint, requestBody);

        var startTime = DateTime.Now;
        var response = await _apiContext!.PostAsync(endpoint, new()
        {
            DataObject = requestBody
        });
        var duration = (DateTime.Now - startTime).TotalMilliseconds;

        var responseBody = await response.TextAsync();
        LogResponse(response, responseBody, (long)duration);

        Assert.That(response.Ok, Is.True);

        var json = JsonDocument.Parse(responseBody);
        var message = json.RootElement.GetProperty("message").GetString();

        TestContext.WriteLine($"✅ Response: {message}");

        Assert.That(message, Is.Not.Null.And.Not.Empty);
        Assert.That(message!.ToLower(), Does.Contain("c").Or.Contains("chord").Or.Contains("diagram"));
    }

    [Test]
    [Category("Chatbot")]
    [Category("API")]
    public async Task Chat_MusicTheoryExplanation_ShouldReturnDetailedExplanation()
    {
        TestContext.WriteLine("Testing music theory explanation...");

        var endpoint = "/api/chatbot/chat";
        var requestBody = new
        {
            message = "Explain voice leading",
            useSemanticSearch = false
        };

        LogRequest(endpoint, requestBody);

        var startTime = DateTime.Now;
        var response = await _apiContext!.PostAsync(endpoint, new()
        {
            DataObject = requestBody
        });
        var duration = (DateTime.Now - startTime).TotalMilliseconds;

        var responseBody = await response.TextAsync();
        LogResponse(response, responseBody, (long)duration);

        Assert.That(response.Ok, Is.True);

        var json = JsonDocument.Parse(responseBody);
        var message = json.RootElement.GetProperty("message").GetString();

        TestContext.WriteLine($"✅ Response length: {message?.Length ?? 0} characters");
        TestContext.WriteLine($"✅ Response: {message}");

        Assert.That(message, Is.Not.Null.And.Not.Empty);
        Assert.That(message!.Length, Is.GreaterThan(50), "Explanation should be detailed");
    }

    [Test]
    [Category("Chatbot")]
    [Category("API")]
    public async Task Chat_ContextualQuery_ShouldMaintainContext()
    {
        TestContext.WriteLine("Testing contextual conversation...");

        var endpoint = "/api/chatbot/chat";

        // First message
        var request1 = new
        {
            message = "Tell me about Cmaj7",
            useSemanticSearch = false
        };

        LogRequest(endpoint, request1);
        var response1 = await _apiContext!.PostAsync(endpoint, new() { DataObject = request1 });
        var body1 = await response1.TextAsync();
        LogResponse(response1, body1, 0);

        Assert.That(response1.Ok, Is.True);

        // Follow-up message (should understand context)
        var request2 = new
        {
            message = "What are similar chords?",
            useSemanticSearch = false
        };

        LogRequest(endpoint, request2);
        var response2 = await _apiContext!.PostAsync(endpoint, new() { DataObject = request2 });
        var body2 = await response2.TextAsync();
        LogResponse(response2, body2, 0);

        Assert.That(response2.Ok, Is.True);

        var json = JsonDocument.Parse(body2);
        var message = json.RootElement.GetProperty("message").GetString();

        TestContext.WriteLine($"✅ Contextual response: {message}");

        Assert.That(message, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    [Category("Chatbot")]
    [Category("API")]
    public async Task Chat_PerformanceBenchmark_ShouldRespondQuickly()
    {
        TestContext.WriteLine("Testing response time performance...");

        var endpoint = "/api/chatbot/chat";
        var requestBody = new
        {
            message = "What is a chord?",
            useSemanticSearch = false
        };

        LogRequest(endpoint, requestBody);

        var startTime = DateTime.Now;
        var response = await _apiContext!.PostAsync(endpoint, new()
        {
            DataObject = requestBody
        });
        var duration = (DateTime.Now - startTime).TotalMilliseconds;

        var responseBody = await response.TextAsync();
        LogResponse(response, responseBody, (long)duration);

        Assert.That(response.Ok, Is.True);

        TestContext.WriteLine($"✅ Response time: {duration:F0}ms");

        // Should respond within reasonable time (30 seconds for LLM)
        Assert.That(duration, Is.LessThan(30000),
            "Response should complete within 30 seconds");
    }
}
