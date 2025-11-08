namespace GuitarAlchemistChatbot.Tests.Playwright;

using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

/// <summary>
///     WebSocket tests for real-time chatbot interaction with detailed logging
/// </summary>
[TestFixture]
public class ChatbotWebSocketTests : PageTest
{
    [SetUp]
    public async Task Setup()
    {
        TestContext.WriteLine("=".PadRight(80, '='));
        TestContext.WriteLine($"TEST: {TestContext.CurrentContext.Test.Name}");
        TestContext.WriteLine("=".PadRight(80, '='));

        // Enable console logging
        Page.Console += (_, msg) => { TestContext.WriteLine($"🖥️  BROWSER CONSOLE [{msg.Type}]: {msg.Text}"); };

        // Log page errors
        Page.PageError += (_, error) => { TestContext.WriteLine($"❌ PAGE ERROR: {error}"); };

        // Log network requests
        Page.Request += (_, request) =>
        {
            if (request.Url.Contains("/api/") || request.Url.Contains("/hubs/"))
            {
                TestContext.WriteLine($"📤 HTTP REQUEST: {request.Method} {request.Url}");
            }
        };

        // Log network responses
        Page.Response += (_, response) =>
        {
            if (response.Url.Contains("/api/") || response.Url.Contains("/hubs/"))
            {
                TestContext.WriteLine($"📥 HTTP RESPONSE: {response.Status} {response.Url}");
            }
        };

        TestContext.WriteLine($"🌐 Navigating to: {_demoUrl}");
        await Page.GotoAsync(_demoUrl);

        TestContext.WriteLine("⏳ Waiting for page to load...");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        TestContext.WriteLine("✅ Page loaded successfully");
        TestContext.WriteLine("");
    }

    [TearDown]
    public async Task TearDown()
    {
        TestContext.WriteLine("");
        TestContext.WriteLine("=".PadRight(80, '='));
        TestContext.WriteLine("TEST COMPLETED");
        TestContext.WriteLine("=".PadRight(80, '='));
        TestContext.WriteLine("");
    }

    private const string _demoUrl = "http://localhost:5232/chatbot-demo.html";
    private const int _defaultTimeout = 60000; // 60 seconds

    [Test]
    [Category("Chatbot")]
    [Category("WebSocket")]
    public async Task WebSocket_ShouldConnectSuccessfully()
    {
        TestContext.WriteLine("Testing WebSocket connection...");

        // Wait for connection status
        var statusElement = Page.Locator("#status");
        await statusElement.WaitForAsync(new() { Timeout = _defaultTimeout });

        var statusText = await statusElement.TextContentAsync();
        TestContext.WriteLine($"📊 Connection Status: {statusText}");

        var statusClass = await statusElement.GetAttributeAsync("class");
        TestContext.WriteLine($"📊 Status Class: {statusClass}");

        Assert.That(statusClass, Does.Contain("connected"),
            "Status should show connected");
        Assert.That(statusText, Does.Contain("Connected").Or.Contains("ready"),
            "Status text should indicate connection");
    }

    [Test]
    [Category("Chatbot")]
    [Category("WebSocket")]
    public async Task WebSocket_SendMessage_ShouldReceiveStreamingResponse()
    {
        TestContext.WriteLine("Testing WebSocket message sending and streaming response...");

        // Wait for connection
        await Page.WaitForSelectorAsync("#status.connected", new() { Timeout = _defaultTimeout });
        TestContext.WriteLine("✅ WebSocket connected");

        var testMessage = "What is a C major chord? Answer in one sentence.";
        TestContext.WriteLine($"📝 Sending message: '{testMessage}'");

        // Fill input
        var input = Page.Locator("#messageInput");
        await input.FillAsync(testMessage);
        TestContext.WriteLine("✅ Message filled in input field");

        // Track message chunks
        var chunks = new List<string>();
        var chunkTimes = new List<DateTime>();

        // Listen for console logs to track chunks
        Page.Console += (_, msg) =>
        {
            var text = msg.Text;
            if (text.StartsWith("Chunk:"))
            {
                var chunk = text.Substring(6).Trim();
                chunks.Add(chunk);
                chunkTimes.Add(DateTime.Now);
                TestContext.WriteLine($"📦 Received chunk #{chunks.Count}: '{chunk}'");
            }
            else if (text.StartsWith("Complete:"))
            {
                var complete = text.Substring(9).Trim();
                TestContext.WriteLine($"✅ Complete message: '{complete}'");
            }
        };

        var sendTime = DateTime.Now;

        // Click send button
        var sendButton = Page.Locator("#sendButton");
        await sendButton.ClickAsync();
        TestContext.WriteLine($"✅ Send button clicked at {sendTime:HH:mm:ss.fff}");

        // Wait for user message to appear
        await Page.WaitForSelectorAsync(".message.user", new() { Timeout = 5000 });
        var userMessages = await Page.Locator(".message.user .message-content").AllTextContentsAsync();
        TestContext.WriteLine($"✅ User message displayed: '{userMessages.Last()}'");

        // Wait for typing indicator
        var typingIndicator = Page.Locator("#typing-indicator");
        try
        {
            await typingIndicator.WaitForAsync(new() { Timeout = 5000 });
            TestContext.WriteLine("✅ Typing indicator appeared");
        }
        catch
        {
            TestContext.WriteLine("⚠️  Typing indicator not detected (may be too fast)");
        }

        // Wait for assistant response
        await Page.WaitForSelectorAsync(".message.assistant:last-child .message-content",
            new() { Timeout = _defaultTimeout });

        var receiveTime = DateTime.Now;
        var duration = (receiveTime - sendTime).TotalMilliseconds;

        var assistantMessages = await Page.Locator(".message.assistant .message-content").AllTextContentsAsync();
        var response = assistantMessages.Last();

        TestContext.WriteLine("");
        TestContext.WriteLine("📊 RESPONSE METRICS:");
        TestContext.WriteLine($"   Total Duration: {duration:F0}ms ({duration / 1000:F2}s)");
        TestContext.WriteLine($"   Chunks Received: {chunks.Count}");
        TestContext.WriteLine($"   Response Length: {response.Length} characters");
        TestContext.WriteLine("");
        TestContext.WriteLine("📝 FULL RESPONSE:");
        TestContext.WriteLine($"   {response}");
        TestContext.WriteLine("");

        if (chunkTimes.Count > 1)
        {
            var firstChunkDelay = (chunkTimes[0] - sendTime).TotalMilliseconds;
            var avgChunkInterval = chunkTimes.Zip(chunkTimes.Skip(1), (a, b) => (b - a).TotalMilliseconds).Average();

            TestContext.WriteLine("⏱️  TIMING DETAILS:");
            TestContext.WriteLine($"   Time to First Chunk: {firstChunkDelay:F0}ms");
            TestContext.WriteLine($"   Average Chunk Interval: {avgChunkInterval:F0}ms");
            TestContext.WriteLine("");
        }

        Assert.That(response, Is.Not.Null.And.Not.Empty, "Should receive a response");
        Assert.That(response.ToLower(), Does.Contain("c").Or.Contains("chord").Or.Contains("major"),
            "Response should be relevant to the query");
    }

    [Test]
    [Category("Chatbot")]
    [Category("WebSocket")]
    public async Task WebSocket_MultipleMessages_ShouldMaintainConversation()
    {
        TestContext.WriteLine("Testing multiple message conversation...");

        await Page.WaitForSelectorAsync("#status.connected", new() { Timeout = _defaultTimeout });

        var messages = new[]
        {
            "What is a C major chord?",
            "What about D major?",
            "Compare them"
        };

        for (var i = 0; i < messages.Length; i++)
        {
            TestContext.WriteLine("");
            TestContext.WriteLine($"📨 Message {i + 1}/{messages.Length}: '{messages[i]}'");

            var input = Page.Locator("#messageInput");
            await input.FillAsync(messages[i]);

            var sendButton = Page.Locator("#sendButton");
            var sendTime = DateTime.Now;
            await sendButton.ClickAsync();

            // Wait for response
            await Page.WaitForSelectorAsync($".message.assistant:nth-child({(i + 1) * 2 + 1})",
                new() { Timeout = _defaultTimeout });

            var receiveTime = DateTime.Now;
            var duration = (receiveTime - sendTime).TotalMilliseconds;

            var assistantMessages = await Page.Locator(".message.assistant .message-content").AllTextContentsAsync();
            var response = assistantMessages[i];

            TestContext.WriteLine($"✅ Response {i + 1} received in {duration:F0}ms");
            TestContext.WriteLine($"   Length: {response.Length} characters");
            TestContext.WriteLine($"   Preview: {response.Substring(0, Math.Min(100, response.Length))}...");
        }

        // Verify all messages are present
        var allUserMessages = await Page.Locator(".message.user .message-content").AllTextContentsAsync();
        var allAssistantMessages = await Page.Locator(".message.assistant .message-content").AllTextContentsAsync();

        TestContext.WriteLine("");
        TestContext.WriteLine("📊 CONVERSATION SUMMARY:");
        TestContext.WriteLine($"   User Messages: {allUserMessages.Count}");
        TestContext.WriteLine($"   Assistant Messages: {allAssistantMessages.Count}");

        Assert.That(allUserMessages.Count, Is.EqualTo(messages.Length + 1), // +1 for welcome message
            "Should have all user messages");
        Assert.That(allAssistantMessages.Count, Is.EqualTo(messages.Length + 1), // +1 for welcome message
            "Should have all assistant responses");
    }

    [Test]
    [Category("Chatbot")]
    [Category("WebSocket")]
    public async Task WebSocket_ClearHistory_ShouldResetConversation()
    {
        TestContext.WriteLine("Testing conversation history clearing...");

        await Page.WaitForSelectorAsync("#status.connected", new() { Timeout = _defaultTimeout });

        // Send a message
        var input = Page.Locator("#messageInput");
        await input.FillAsync("Test message");

        var sendButton = Page.Locator("#sendButton");
        await sendButton.ClickAsync();

        await Page.WaitForSelectorAsync(".message.assistant:nth-child(3)",
            new() { Timeout = _defaultTimeout });

        var messagesBefore = await Page.Locator(".message").CountAsync();
        TestContext.WriteLine($"📊 Messages before clear: {messagesBefore}");

        // Clear history
        var clearButton = Page.Locator("button:has-text('Clear')");
        await clearButton.ClickAsync();
        TestContext.WriteLine("🗑️  Clear button clicked");

        await Task.Delay(1000); // Wait for clear to process

        var messagesAfter = await Page.Locator(".message").CountAsync();
        TestContext.WriteLine($"📊 Messages after clear: {messagesAfter}");

        Assert.That(messagesAfter, Is.LessThan(messagesBefore),
            "Message count should decrease after clearing");
    }

    [Test]
    [Category("Chatbot")]
    [Category("WebSocket")]
    public async Task WebSocket_LongResponse_ShouldStreamEfficiently()
    {
        TestContext.WriteLine("Testing long response streaming...");

        await Page.WaitForSelectorAsync("#status.connected", new() { Timeout = _defaultTimeout });

        var input = Page.Locator("#messageInput");
        await input.FillAsync("Explain the circle of fifths in detail");

        var sendButton = Page.Locator("#sendButton");
        var sendTime = DateTime.Now;
        await sendButton.ClickAsync();

        TestContext.WriteLine($"📤 Sent at: {sendTime:HH:mm:ss.fff}");

        // Wait for response to complete
        await Page.WaitForSelectorAsync(".message.assistant:last-child .message-content",
            new() { Timeout = _defaultTimeout });

        // Wait a bit more to ensure streaming is complete
        await Task.Delay(2000);

        var receiveTime = DateTime.Now;
        var duration = (receiveTime - sendTime).TotalMilliseconds;

        var response = await Page.Locator(".message.assistant:last-child .message-content").TextContentAsync();

        TestContext.WriteLine("");
        TestContext.WriteLine("📊 STREAMING PERFORMANCE:");
        TestContext.WriteLine($"   Total Duration: {duration:F0}ms ({duration / 1000:F2}s)");
        TestContext.WriteLine($"   Response Length: {response?.Length ?? 0} characters");
        TestContext.WriteLine($"   Characters/Second: {(response?.Length ?? 0) / (duration / 1000):F1}");
        TestContext.WriteLine("");
        TestContext.WriteLine("📝 RESPONSE:");
        TestContext.WriteLine($"   {response}");

        Assert.That(response, Is.Not.Null.And.Not.Empty);
        Assert.That(response!.Length, Is.GreaterThan(50),
            "Long query should produce substantial response");
    }
}
