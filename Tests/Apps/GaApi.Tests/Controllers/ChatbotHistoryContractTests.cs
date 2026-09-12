namespace GaApi.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
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
    public async Task Chat_PreservesConversationHistory(string route, string historyKind)
    {
        ChatIntakeRequest? received = null;
        var intake = new Mock<IChatIntake>();
        intake.Setup(x => x.IntakeAsync(It.IsAny<ChatIntakeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ChatIntakeRequest, CancellationToken>((request, _) => received = request)
            .ReturnsAsync(Result<ChatResponse, ChatIntakeError>.Success(new ChatResponse("Try D Dorian.", [])));

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.Services.AddSingleton(intake.Object);
        builder.Services.AddSingleton(Mock.Of<IAgenticTraceCapture>());
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

        if (route == "chat/stream")
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.That(body, Does.Contain("data: [DONE]"));
        }
        else
        {
            var body = await response.Content.ReadFromJsonAsync<ChatJsonResponse>();
            Assert.That(body!.NaturalLanguageAnswer, Is.EqualTo("Try D Dorian."));
        }
    }
}
