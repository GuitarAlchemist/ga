namespace GuitarAlchemistChatbot.Tests.Playwright;

using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

/// <summary>
///     Base class for chatbot Playwright tests
/// </summary>
public class ChatbotTestBase : PageTest
{
    protected const string BaseUrl = "https://localhost:7001"; // Update with actual URL
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
        // Find the input field
        var input = Page.Locator("input.form-control[placeholder*='Ask about']");
        await input.FillAsync(message);

        // Click the send button
        var sendButton = Page.Locator("button.btn-primary:has(i.fa-paper-plane)");
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

        // Get the last assistant message
        var messages = await Page.Locator(".assistant-message .message-text").AllAsync();
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
        var newChatButton = Page.Locator("button:has-text('New Chat')");
        await newChatButton.ClickAsync();

        // Wait for messages to clear
        await Task.Delay(500);
    }

    /// <summary>
    ///     Get all user messages
    /// </summary>
    protected async Task<List<string>> GetUserMessagesAsync()
    {
        var messages = await Page.Locator(".user-message .message-text").AllAsync();
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
        var messages = await Page.Locator(".assistant-message .message-text").AllAsync();
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
