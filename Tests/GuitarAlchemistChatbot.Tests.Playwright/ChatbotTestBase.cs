namespace GuitarAlchemistChatbot.Tests.Playwright;

using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

/// <summary>
///     Base class for chatbot Playwright tests
/// </summary>
public class ChatbotTestBase : PageTest
{
    // GaApi serves chatbot-demo.html from Apps/ga-server/GaApi/wwwroot.
    // 5232 = the http profile in GaApi's launchSettings.json. The previous
    // value `https://localhost:7001` was a placeholder ("Update with actual
    // URL" comment) that never matched any real binding — see ga#134.
    // Override via env var so CI can pin the URL to whatever the workflow
    // actually starts; default keeps local dev pointed at the canonical
    // http profile.
    protected static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("CHATBOT_BASE_URL")
        ?? "http://localhost:5232/chatbot-demo.html";
    protected const int DefaultTimeout = 30000; // 30 seconds

    [SetUp]
    public async Task Setup()
    {
        // Navigate to the chatbot page
        await Page.GotoAsync(BaseUrl);

        // Wait for the page to load
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for the chat interface to be ready
        await Page.WaitForSelectorAsync(".chat-container", new() { Timeout = DefaultTimeout });
    }

    /// <summary>
    ///     Send a message in the chat
    /// </summary>
    protected async Task SendMessageAsync(string message)
    {
        // Input field. chatbot-demo.html: <input id="messageInput" class="chat-input"
        // placeholder="Ask me about chords, scales, or techniques...">. The old
        // `input.form-control[placeholder*='Ask about']` matched a Bootstrap UI that
        // was never built (ga#145).
        var input = Page.Locator("#messageInput");
        await input.FillAsync(message);

        // Send button: <button id="sendButton" class="send-button">Send</button>
        // (no Bootstrap btn-primary, no FontAwesome paper-plane icon).
        var sendButton = Page.Locator("#sendButton");
        await sendButton.ClickAsync();
    }

    /// <summary>
    ///     Wait for assistant response
    /// </summary>
    protected async Task<string> WaitForResponseAsync()
    {
        // Wait for typing indicator to disappear
        await Page.WaitForSelectorAsync(".typing-indicator", new()
        {
            State = WaitForSelectorState.Hidden,
            Timeout = DefaultTimeout
        });

        // Get the last assistant message. chatbot-demo.html renders each turn as
        // `<div class="message assistant"><div class="message-content">…</div></div>`
        // (the typing indicator is a sibling `.message.assistant` WITHOUT a
        // `.message-content`, so it is naturally excluded from this query).
        var messages = await Page.Locator(".message.assistant .message-content").AllAsync();
        if (messages.Count == 0)
        {
            throw new Exception("No assistant messages found");
        }

        var lastMessage = messages[^1];
        return await lastMessage.TextContentAsync() ?? "";
    }

    /// <summary>
    ///     Wait for function call indicator
    /// </summary>
    protected async Task<string?> WaitForFunctionCallAsync()
    {
        // NOTE: chatbot-demo.html (the CI-served page) does not render a
        // `.function-indicator`; this helper degrades to null on that page and is
        // here for richer chatbot hosts. See ga#145.
        try
        {
            var functionIndicator = Page.Locator(".function-indicator");
            await functionIndicator.WaitForAsync(new() { Timeout = 5000 });
            return await functionIndicator.TextContentAsync();
        }
        catch
        {
            return null; // No function call
        }
    }

    /// <summary>
    ///     Get context summary
    /// </summary>
    protected async Task<string?> GetContextSummaryAsync()
    {
        // NOTE: not rendered by chatbot-demo.html — degrades to null there (ga#145).
        try
        {
            var contextIndicator = Page.Locator(".context-indicator");
            await contextIndicator.WaitForAsync(new() { Timeout = 2000 });
            return await contextIndicator.TextContentAsync();
        }
        catch
        {
            return null; // No context
        }
    }

    /// <summary>
    ///     Click new chat button
    /// </summary>
    protected async Task ClickNewChatAsync()
    {
        // chatbot-demo.html exposes a "Clear" button (clearHistory()) rather than a
        // "New Chat" button; it empties the message list, which is what callers of
        // this helper rely on (ga#145).
        var newChatButton = Page.Locator("button.clear-button");
        await newChatButton.ClickAsync();

        // Wait for messages to clear
        await Task.Delay(500);
    }

    /// <summary>
    ///     Get all user messages
    /// </summary>
    protected async Task<List<string>> GetUserMessagesAsync()
    {
        var messages = await Page.Locator(".message.user .message-content").AllAsync();
        var texts = new List<string>();
        foreach (var msg in messages)
        {
            var text = await msg.TextContentAsync();
            if (text != null)
            {
                texts.Add(text);
            }
        }

        return texts;
    }

    /// <summary>
    ///     Get all assistant messages
    /// </summary>
    protected async Task<List<string>> GetAssistantMessagesAsync()
    {
        var messages = await Page.Locator(".message.assistant .message-content").AllAsync();
        var texts = new List<string>();
        foreach (var msg in messages)
        {
            var text = await msg.TextContentAsync();
            if (text != null)
            {
                texts.Add(text);
            }
        }

        return texts;
    }

    /// <summary>
    ///     Check if VexTab element exists
    /// </summary>
    // NOTE: chatbot-demo.html does not render VexTab (`.vex-tabdiv`); HasVexTabAsync
    // returns false there and WaitForVexTabRenderAsync will time out. Both exist for
    // richer chatbot hosts that embed notation. See ga#145.
    protected async Task<bool> HasVexTabAsync()
    {
        try
        {
            var vexTab = Page.Locator(".vex-tabdiv");
            await vexTab.WaitForAsync(new() { Timeout = 2000 });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Wait for VexTab to render
    /// </summary>
    protected async Task WaitForVexTabRenderAsync()
    {
        // Wait for VexTab div with data-rendered attribute
        await Page.WaitForSelectorAsync(".vex-tabdiv[data-rendered='true']", new()
        {
            Timeout = DefaultTimeout
        });
    }
}
