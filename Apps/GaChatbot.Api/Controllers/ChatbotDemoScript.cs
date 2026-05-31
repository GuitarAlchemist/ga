namespace GaChatbot.Api.Controllers;

public sealed record ChatbotDemoScript(string Version, IReadOnlyList<ChatbotDemoCategory> Categories);

public sealed record ChatbotDemoCategory(
    string Id,
    string Name,
    string Icon,
    string Description,
    IReadOnlyList<ChatbotDemoPrompt> Prompts);

public sealed record ChatbotDemoPrompt(string Prompt, string Description);
