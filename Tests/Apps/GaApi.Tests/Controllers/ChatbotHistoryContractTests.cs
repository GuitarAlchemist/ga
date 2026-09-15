namespace GaApi.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Trace;
using GA.Core.Functional;
using GaApi.Controllers;
using GaApi.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

[TestFixture]
public class ChatbotHistoryContractTests
{
    [TestCase("chat", "populated")]
    [TestCase("chat/stream", "populated")]
    [TestCase("chat", "missing")]
    [TestCase("chat/stream", "missing")]
    [TestCase("chat", "empty")]
    [TestCase("chat/stream", "empty")]
    [TestCase("chat", "blank")]
    [TestCase("chat/stream", "blank")]
    public async Task Chat_PreservesHistoryAndWireContract(string route, string historyKind)
    {
        ChatIntakeRequest? received = null;
        var intake = new Mock<IChatIntake>();
        intake.Setup(x => x.IntakeAsync(It.IsAny<ChatIntakeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ChatIntakeRequest, CancellationToken>((request, _) => received = request)
            .ReturnsAsync(Result<ChatResponse, ChatIntakeError>.Success(new ChatResponse("Try D Dorian.", [], Routing: new("theory", 0.9f, "deterministic"), Grounding: new("theory-library", "test-revision", "scale"))));

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.Services.AddSingleton(intake.Object);
        var trace = new Mock<IAgenticTraceCapture>();
        trace.Setup(value => value.Build()).Returns(new AgenticTrace("test-trace", "test-protocol", "test-run",
            [new AgenticTraceStep("orchestration.answer", "completed", 7, new Dictionary<string, object?>())]));
        builder.Services.AddSingleton(trace.Object);
        builder.Services.AddSingleton(Mock.Of<IChatService>());
        builder.Services.AddControllers().AddApplicationPart(typeof(ChatbotController).Assembly);
        await using var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        using var client = app.GetTestClient();
        List<ChatMessage> history =
        [
            new() { Role = "user", Content = "I am playing Dm7." },
            new() { Role = "assistant", Content = "Its notes are D F A C." }
        ];
        if (historyKind == "empty") history.Clear();
        if (historyKind == "blank") history.Insert(1, new ChatMessage { Role = "user", Content = "   " });
        object payload = historyKind == "missing"
            ? new { message = "Which scale fits that chord?" }
            : new { message = "Which scale fits that chord?", conversationHistory = history };
        using var response = await client.PostAsJsonAsync($"/api/chatbot/{route}", payload);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(received, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(received!.Message, Is.EqualTo("Which scale fits that chord?"));
            Assert.That(received.SessionId, Is.Not.Null.And.Not.Empty);
        });

        if (historyKind == "missing")
            Assert.That(received!.History, Is.Null);
        else if (historyKind == "empty")
            Assert.That(received!.History, Is.Empty);
        else
        {
            Assert.That(received!.History, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(received.History!.Select(turn => turn.Role), Is.EqualTo(new[] { "user", "assistant" }));
                Assert.That(received.History.Select(turn => turn.Content),
                    Is.EqualTo(new[] { "I am playing Dm7.", "Its notes are D F A C." }));
            });
        }

        JsonElement metadata;
        if (route == "chat/stream")
        {
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/event-stream"));
            Assert.That(response.Headers.CacheControl?.NoCache, Is.True);
            Assert.That(response.Headers.TryGetValues("X-Accel-Buffering", out var buffering), Is.True);
            Assert.That(buffering, Is.EqualTo(new[] { "no" }));
            var frames = (await response.Content.ReadAsStringAsync()).Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
            Assert.That(frames, Has.Length.EqualTo(3));
            using var routing = JsonDocument.Parse(frames[0]["data: ".Length..]);
            metadata = routing.RootElement.Clone();
            Assert.That(metadata.GetProperty("type").GetString(), Is.EqualTo("routing"));
            Assert.That(frames[1], Is.EqualTo("data: Try D Dorian."));
            Assert.That(frames[2], Is.EqualTo("data: [DONE]"));
        }
        else
        {
            metadata = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(metadata.GetProperty("naturalLanguageAnswer").GetString(), Is.EqualTo("Try D Dorian."));
        }

        Assert.Multiple(() =>
        {
            Assert.That(metadata.GetProperty("agentId").GetString(), Is.EqualTo("theory"));
            Assert.That(metadata.GetProperty("routingMethod").GetString(), Is.EqualTo("deterministic"));
            Assert.That(metadata.GetProperty("confidence").GetSingle(), Is.EqualTo(0.9f));
            Assert.That(metadata.GetProperty("grounding").GetProperty("source").GetString(), Is.EqualTo("theory-library"));
            Assert.That(metadata.GetProperty("grounding").GetProperty("revision").GetString(), Is.EqualTo("test-revision"));
            Assert.That(metadata.GetProperty("grounding").GetProperty("queryType").GetString(), Is.EqualTo("scale"));
            Assert.That(metadata.GetProperty("trace").GetProperty("traceId").GetString(), Is.EqualTo("test-trace"));
            Assert.That(metadata.GetProperty("trace").GetProperty("runId").GetString(), Is.EqualTo("test-run"));
            Assert.That(metadata.GetProperty("trace").GetProperty("steps")[0].GetProperty("name").GetString(), Is.EqualTo("orchestration.answer"));
        });
    }
}
