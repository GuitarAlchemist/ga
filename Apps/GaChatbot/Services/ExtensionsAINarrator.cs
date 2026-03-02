namespace GaChatbot.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GaChatbot.Abstractions;
using GaChatbot.Models;

/// <summary>
/// Modern narrator implementation using Microsoft.Extensions.AI (2026 pattern).
/// Supports multiple providers through IChatClient abstraction:
/// - Ollama (local)
/// - OpenAI / Azure OpenAI
/// - GitHub Models
/// </summary>
public class ExtensionsAINarrator : IGroundedNarrator
{
    private readonly IChatClient _chatClient;
    private readonly GroundedPromptBuilder _promptBuilder;
    private readonly ResponseValidator _validator;
    private readonly ILogger<ExtensionsAINarrator> _logger;

    public ExtensionsAINarrator(
        IChatClient chatClient,
        GroundedPromptBuilder promptBuilder,
        ResponseValidator validator,
        ILogger<ExtensionsAINarrator> logger)
    {
        _chatClient = chatClient;
        _promptBuilder = promptBuilder;
        _validator = validator;
        _logger = logger;
    }

    public async Task<string> NarrateAsync(
        string query, 
        List<CandidateVoicing> candidates, 
        bool simulateHallucination = false)
    {
        // 1. Build the grounded prompt (system + user message)
        var systemPrompt = _promptBuilder.Build(query, candidates);
        
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, query)
        };

        // 2. Request completion via unified IChatClient
        try
        {
            var options = new ChatOptions
            {
                Temperature = 0.7f,
                MaxOutputTokens = 512
            };

            var response = await _chatClient.GetResponseAsync(messages, options);
            var content = response.Text ?? "No response generated.";

            // 3. Validate and clean response
            var validation = _validator.Validate(content, candidates);
            
            if (validation.HallucinatedChords.Count > 0)
            {
                _logger.LogWarning("Hallucination detected and removed from LLM response");
            }

            return validation.CleanedMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IChatClient call failed, using fallback");
            return FormatFallback(query, candidates);
        }
    }

    private static string FormatFallback(string query, List<CandidateVoicing> candidates)
    {
        if (candidates.Count == 0)
        {
            return "No matching voicings found in the database.";
        }

        var lines = new List<string>
        {
            $"Found {candidates.Count} voicing(s) for '{query}':"
        };

        foreach (var c in candidates.Take(5))
        {
            lines.Add($"  • {c.DisplayName} ({c.Shape}) - Score: {c.Score:F2}");
            if (!string.IsNullOrWhiteSpace(c.ExplanationText))
            {
                lines.Add($"    {c.ExplanationText}");
            }
        }

        return string.Join("\n", lines);
    }
}

/// <summary>
/// Extension methods for registering AI services using Microsoft.Extensions.AI patterns.
/// </summary>
public static class AIServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers Ollama as the IChatClient provider (local development).
        /// </summary>
        public IServiceCollection AddOllamaAIChatClient(
            string modelId = "llama3.2",
            string endpoint = "http://localhost:11434")
        {
            services.AddSingleton<IChatClient>(sp =>
            {
                return new OllamaChatClient(new Uri(endpoint), modelId);
            });

            return services;
        }

        /// <summary>
        /// Registers OpenAI as the IChatClient provider (production/cloud).
        /// Compatible with Azure OpenAI, GitHub Models, and standard OpenAI.
        /// </summary>
        public IServiceCollection AddOpenAIChatClient(
            string modelId,
            string apiKey,
            string? endpoint = null)
        {
            services.AddSingleton<IChatClient>(_ =>
            {
                var options = new OpenAI.OpenAIClientOptions();
                if (!string.IsNullOrEmpty(endpoint))
                {
                    options.Endpoint = new Uri(endpoint);
                }

                var credential = new System.ClientModel.ApiKeyCredential(apiKey);
                var openAiClient = new OpenAI.OpenAIClient(credential, options);
                return openAiClient.AsChatClient(modelId);
            });

            return services;
        }

        /// <summary>
        /// Registers GitHub Models as the IChatClient provider (free tier development).
        /// Uses the GitHub Models endpoint with a personal access token.
        /// </summary>
        public IServiceCollection AddGitHubModelsChatClient(
            string modelId = "gpt-4o-mini",
            string? githubToken = null)
        {
            var token = githubToken ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException(
                    "GitHub token not found. Set GITHUB_TOKEN environment variable or pass token explicitly.");
            }

            return services.AddOpenAIChatClient(
                modelId: modelId,
                apiKey: token,
                endpoint: "https://models.inference.ai.azure.com");
        }

        /// <summary>
        /// Registers the modern ExtensionsAINarrator as the IGroundedNarrator implementation.
        /// </summary>
        public IServiceCollection AddExtensionsAINarrator()
        {
            services.AddSingleton<IGroundedNarrator, ExtensionsAINarrator>();
            return services;
        }
    }
}
