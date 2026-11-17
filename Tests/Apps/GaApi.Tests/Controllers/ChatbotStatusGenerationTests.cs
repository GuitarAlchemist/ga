namespace GaApi.Tests.Controllers;

using System.Net.Http.Json;
using System.Text.Json;
using GA.Business.ML.Providers;
using GaApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// GET /api/chatbot/status must reflect a failed generation. The provider check only
/// proves the provider answers; Ollama Cloud without credits answered it while every
/// generation failed, and status kept reporting ready. Same contract as GaChatbot.Api's
/// probe: <c>generationOk</c>, unavailable with the provider's error, unknown after
/// <see cref="ChatGenerationHealth.OutcomeWindow"/>.
/// </summary>
[TestFixture]
public class ChatbotStatusGenerationTests
{
    private const string OllamaCreditsError =
        "Ollama error: Add usage credits or subscribe to continue using cloud models.";

    [Test]
    public async Task Status_LastGenerationFailed_ReportsUnavailableWithTheProviderError()
    {
        using var factory = CreateFactory();
        factory.Services.GetRequiredService<ChatGenerationHealth>()
            .RecordFailure(new InvalidOperationException(OllamaCreditsError));

        var status = await GetStatusAsync(factory);

        Assert.Multiple(() =>
        {
            Assert.That(status.GetProperty("isAvailable").GetBoolean(), Is.False);
            Assert.That(status.GetProperty("generationOk").GetBoolean(), Is.False);
            Assert.That(status.GetProperty("message").GetString(), Does.Contain("Add usage credits"));
        });
    }

    [Test]
    public async Task Status_GenerationRecovered_ReportsAvailable()
    {
        using var factory = CreateFactory();
        var health = factory.Services.GetRequiredService<ChatGenerationHealth>();
        health.RecordFailure("timed out");
        health.RecordSuccess();

        var status = await GetStatusAsync(factory);

        Assert.Multiple(() =>
        {
            Assert.That(status.GetProperty("isAvailable").GetBoolean(), Is.True);
            Assert.That(status.GetProperty("generationOk").GetBoolean(), Is.True);
        });
    }

    [Test]
    public async Task Status_FailureOlderThanTheWindow_ReportsUnknownAndAvailable()
    {
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-11-17T12:00:00Z"));
        var health = new ChatGenerationHealth(time);
        using var factory = CreateFactory(health);
        health.RecordFailure(new InvalidOperationException(OllamaCreditsError));
        time.Advance(ChatGenerationHealth.OutcomeWindow + TimeSpan.FromSeconds(1));

        var status = await GetStatusAsync(factory);

        Assert.Multiple(() =>
        {
            Assert.That(status.GetProperty("isAvailable").GetBoolean(), Is.True);
            Assert.That(status.GetProperty("generationOk").ValueKind, Is.EqualTo(JsonValueKind.Null));
        });
    }

    [Test]
    public void RegisteredChatClient_RecordsGenerationOutcomes()
    {
        // Without this wrapper nothing feeds ChatGenerationHealth in GaApi.
        using var factory = new TestWebApplicationFactory();

        Assert.That(factory.Services.GetRequiredService<IChatClient>(), Is.InstanceOf<GenerationHealthChatClient>());
    }

    private static async Task<JsonElement> GetStatusAsync(WebApplicationFactory<Program> factory)
    {
        using var client = factory.CreateClient();
        return await client.GetFromJsonAsync<JsonElement>("/api/chatbot/status");
    }

    private static WebApplicationFactory<Program> CreateFactory(ChatGenerationHealth? health = null) =>
        new TestWebApplicationFactory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                // The provider check passes: only the generation outcome may turn status red.
                services.RemoveAll<IChatProviderAvailability>();
                services.AddSingleton<IChatProviderAvailability>(new AvailableProvider());
                if (health is not null)
                {
                    services.RemoveAll<ChatGenerationHealth>();
                    services.AddSingleton(health);
                }
            }));

    private sealed class AvailableProvider : IChatProviderAvailability
    {
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
