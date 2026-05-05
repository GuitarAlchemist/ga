namespace GaChatbot.Api.Tests.Controllers;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GA.Business.Core.Orchestration.Models;
using GaChatbot.Api.Controllers;
using GaChatbot.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

[TestFixture]
public class ChatbotApiSurfaceTests
{
    [Test]
    public async Task Root_ReturnsMiniChatUi_WithChatFormAndOperationalLinks()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));

        var html = await response.Content.ReadAsStringAsync();
        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("id=\"chatForm\""));
            Assert.That(html, Does.Contain("id=\"messageInput\""));
            Assert.That(html, Does.Contain("id=\"sendButton\""));
            Assert.That(html, Does.Contain("Agentic trace"));
            // URLs in the inline HTML are RELATIVE (no leading slash) so the
            // page works when mounted under a path-base (e.g.
            // demos.guitaralchemist.com/chatbot/) via Cloudflare Tunnel
            // ingress. Match the path component without anchoring on /.
            Assert.That(html, Does.Contain("vendor/vexflow/vexflow.js"));
            Assert.That(html, Does.Contain("renderTabNotation"));
            Assert.That(html, Does.Contain("parseTabPositions"));
            Assert.That(html, Does.Contain("```(?:vextab|vexflow|tab)"));
            Assert.That(html, Does.Contain("api/chatbot/status"));
            Assert.That(html, Does.Contain("api/chatbot/examples"));
            Assert.That(html, Does.Contain("api/chatbot/chat"));
        });
    }

    [Test]
    public async Task VexFlowVendorAsset_ReturnsLocalBrowserBundle()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/vendor/vexflow/vexflow.js");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/javascript"));

        var body = await response.Content.ReadAsStringAsync();
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("VexFlow 4.2.5"));
            Assert.That(body, Does.Contain("t.Vex=e()"));
            Assert.That(body, Does.Contain("TabStave"));
            Assert.That(body, Does.Contain("TabNote"));
        });
    }

    [Test]
    public async Task ApiMetadata_ReturnsServiceIdentity()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<JsonElement>("/api");

        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("service").GetString(), Is.EqualTo("ga-chatbot-api"));
            Assert.That(body.GetProperty("version").GetString(), Is.EqualTo("0.1.0"));
            Assert.That(body.GetProperty("description").GetString(), Does.Contain("chatbot API"));
        });
    }

    [Test]
    public async Task A2AAgentCard_ReturnsDiscoverableJsonRpcEndpointAndMusicTheorySkill()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<JsonElement>("/.well-known/agent-card.json");

        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("name").GetString(), Is.EqualTo("Guitar Alchemist Chatbot"));
            Assert.That(body.GetProperty("url").GetString(), Does.EndWith("/a2a"));
            Assert.That(body.GetProperty("preferredTransport").GetString(), Is.EqualTo("JSONRPC"));
            Assert.That(body.GetProperty("capabilities").GetProperty("streaming").GetBoolean(), Is.True);
            Assert.That(body.GetProperty("defaultInputModes")[0].GetString(), Is.EqualTo("text/plain"));
            Assert.That(body.GetProperty("defaultOutputModes")[0].GetString(), Is.EqualTo("text/plain"));
            Assert.That(body.GetProperty("skills")[0].GetProperty("id").GetString(), Is.EqualTo("music-theory-chat"));
        });
    }

    [Test]
    public async Task A2A_WhenDisabled_ReturnsNotFoundForDiscoveryAndRpcEndpoint()
    {
        using var factory = CreateFactory(configure: builder =>
        {
            builder.UseSetting("A2A:Enabled", "false");
        });
        using var client = factory.CreateClient();

        var cardResponse = await client.GetAsync("/.well-known/agent-card.json");
        var rpcResponse = await client.PostAsJsonAsync("/a2a", new
        {
            jsonrpc = "2.0",
            id = "req-1",
            method = "message/send",
            @params = new
            {
                message = new
                {
                    role = "user",
                    parts = new[] { new { kind = "text", text = "Explain C major." } }
                }
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(cardResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(rpcResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [Test]
    public async Task A2AMessageSend_ForwardsTextMessageAndReturnsJsonRpcAgentMessage()
    {
        var fake = new FakeChatApplicationService();
        using var factory = CreateFactory(chatService: fake);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/a2a", new
        {
            jsonrpc = "2.0",
            id = "req-1",
            method = "message/send",
            @params = new
            {
                message = new
                {
                    role = "user",
                    messageId = "client-message",
                    contextId = "ctx-existing",
                    parts = new[]
                    {
                        new { kind = "text", text = "Explain C major." }
                    }
                }
            }
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(fake.LastRequest, Is.Not.Null);
        Assert.That(fake.LastRequest!.Message, Is.EqualTo("Explain C major."));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var result = body.GetProperty("result");
        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("jsonrpc").GetString(), Is.EqualTo("2.0"));
            Assert.That(body.GetProperty("id").GetString(), Is.EqualTo("req-1"));
            Assert.That(result.GetProperty("kind").GetString(), Is.EqualTo("message"));
            Assert.That(result.GetProperty("role").GetString(), Is.EqualTo("agent"));
            Assert.That(result.GetProperty("contextId").GetString(), Is.EqualTo("ctx-existing"));
            Assert.That(result.GetProperty("parts")[0].GetProperty("kind").GetString(), Is.EqualTo("text"));
            Assert.That(result.GetProperty("parts")[0].GetProperty("text").GetString(), Is.EqualTo("fake answer"));
            Assert.That(result.GetProperty("metadata").GetProperty("agentId").GetString(), Is.EqualTo("fake-agent"));
            Assert.That(result.GetProperty("metadata").GetProperty("routingMethod").GetString(), Is.EqualTo("fake-route"));
            Assert.That(result.GetProperty("metadata").GetProperty("trace").GetProperty("steps")[0].GetProperty("name").GetString(), Is.EqualTo("chat.request"));
            Assert.That(result.TryGetProperty("error", out _), Is.False);
        });
    }

    [Test]
    public async Task A2AMessageSend_WithSameContext_ForwardsStoredConversationHistory()
    {
        var fake = new FakeChatApplicationService();
        using var factory = CreateFactory(chatService: fake);
        using var client = factory.CreateClient();

        await PostA2AAsync(client, "message/send", new
        {
            message = new
            {
                role = "user",
                contextId = "ctx-memory",
                parts = new[] { new { kind = "text", text = "Explain C major." } }
            }
        });

        var body = await PostA2AAsync(client, "message/send", new
        {
            message = new
            {
                role = "user",
                contextId = "ctx-memory",
                parts = new[] { new { kind = "text", text = "Continue that." } }
            }
        });

        Assert.That(fake.LastRequest, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(fake.LastRequest!.Message, Is.EqualTo("Continue that."));
            Assert.That(fake.LastRequest.History, Has.Count.EqualTo(2));
            Assert.That(fake.LastRequest.History!.Select(turn => turn.Content), Does.Not.Contain("Continue that."));
            Assert.That(fake.LastRequest.History[0].Role, Is.EqualTo("user"));
            Assert.That(fake.LastRequest.History[0].Content, Is.EqualTo("Explain C major."));
            Assert.That(fake.LastRequest.History[1].Role, Is.EqualTo("assistant"));
            Assert.That(fake.LastRequest.History[1].Content, Is.EqualTo("fake answer"));
            Assert.That(body.GetProperty("result").GetProperty("metadata").GetProperty("historyTurnCount").GetInt32(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task A2AMessageSend_WithDifferentContext_DoesNotLeakConversationHistory()
    {
        var fake = new FakeChatApplicationService();
        using var factory = CreateFactory(chatService: fake);
        using var client = factory.CreateClient();

        await PostA2AAsync(client, "message/send", new
        {
            message = new
            {
                role = "user",
                contextId = "ctx-one",
                parts = new[] { new { kind = "text", text = "Explain C major." } }
            }
        });

        await PostA2AAsync(client, "message/send", new
        {
            message = new
            {
                role = "user",
                contextId = "ctx-two",
                parts = new[] { new { kind = "text", text = "Continue that." } }
            }
        });

        Assert.That(fake.LastRequest, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(fake.LastRequest!.Message, Is.EqualTo("Continue that."));
            Assert.That(fake.LastRequest.History, Is.Null);
        });
    }

    [Test]
    public async Task A2AMessageSend_WhenConcurrencyGateIsFull_ReturnsJsonRpcBusyError()
    {
        using var factory = CreateFactory(gate: new ClosedConcurrencyGate());
        using var client = factory.CreateClient();

        var body = await PostA2AAsync(client, "message/send", new
        {
            message = new
            {
                role = "user",
                parts = new[] { new { kind = "text", text = "Explain C major." } }
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32000));
            Assert.That(body.GetProperty("error").GetProperty("message").GetString(), Does.Contain("busy"));
            Assert.That(body.TryGetProperty("result", out _), Is.False);
        });
    }

    [Test]
    public async Task A2AMessageStream_EmitsTaskArtifactAndFinalStatusEvents()
    {
        var fake = new FakeChatApplicationService();
        using var factory = CreateFactory(chatService: fake);
        using var client = factory.CreateClient();

        var response = await PostA2AStreamAsync(client, new
        {
            message = new
            {
                role = "user",
                contextId = "ctx-stream",
                parts = new[] { new { kind = "text", text = "Explain C major." } }
            }
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/event-stream"));
        Assert.That(fake.LastRequest, Is.Not.Null);
        Assert.That(fake.LastRequest!.Message, Is.EqualTo("Explain C major."));

        var events = ReadSseDataLines(await response.Content.ReadAsStringAsync());

        Assert.That(events, Has.Count.EqualTo(3));
        var task = JsonSerializer.Deserialize<JsonElement>(events[0]).GetProperty("result");
        var artifact = JsonSerializer.Deserialize<JsonElement>(events[1]).GetProperty("result");
        var status = JsonSerializer.Deserialize<JsonElement>(events[2]).GetProperty("result");

        Assert.Multiple(() =>
        {
            Assert.That(task.GetProperty("kind").GetString(), Is.EqualTo("task"));
            Assert.That(task.GetProperty("contextId").GetString(), Is.EqualTo("ctx-stream"));
            Assert.That(task.GetProperty("status").GetProperty("state").GetString(), Is.EqualTo("working"));
            Assert.That(artifact.GetProperty("kind").GetString(), Is.EqualTo("artifact-update"));
            Assert.That(artifact.GetProperty("artifact").GetProperty("parts")[0].GetProperty("text").GetString(), Is.EqualTo("fake answer"));
            Assert.That(status.GetProperty("kind").GetString(), Is.EqualTo("status-update"));
            Assert.That(status.GetProperty("status").GetProperty("state").GetString(), Is.EqualTo("completed"));
            Assert.That(status.GetProperty("final").GetBoolean(), Is.True);
        });
    }

    [Test]
    public async Task A2AMessageStream_WithSameContext_ForwardsStoredConversationHistory()
    {
        var fake = new FakeChatApplicationService();
        using var factory = CreateFactory(chatService: fake);
        using var client = factory.CreateClient();

        using var firstResponse = await PostA2AStreamAsync(client, new
        {
            message = new
            {
                role = "user",
                contextId = "ctx-stream-memory",
                parts = new[] { new { kind = "text", text = "Explain C major." } }
            }
        });
        _ = await firstResponse.Content.ReadAsStringAsync();

        using var secondResponse = await PostA2AStreamAsync(client, new
        {
            message = new
            {
                role = "user",
                contextId = "ctx-stream-memory",
                parts = new[] { new { kind = "text", text = "Continue that." } }
            }
        });
        _ = await secondResponse.Content.ReadAsStringAsync();

        Assert.That(fake.LastRequest, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(fake.LastRequest!.Message, Is.EqualTo("Continue that."));
            Assert.That(fake.LastRequest.History, Has.Count.EqualTo(2));
            Assert.That(fake.LastRequest.History!.Select(turn => turn.Content), Does.Not.Contain("Continue that."));
            Assert.That(fake.LastRequest.History[0].Role, Is.EqualTo("user"));
            Assert.That(fake.LastRequest.History[0].Content, Is.EqualTo("Explain C major."));
            Assert.That(fake.LastRequest.History[1].Role, Is.EqualTo("assistant"));
            Assert.That(fake.LastRequest.History[1].Content, Is.EqualTo("fake answer"));
        });
    }

    [Test]
    public async Task A2AMessageStream_WhenConcurrencyGateIsFull_EmitsJsonRpcBusyError()
    {
        using var factory = CreateFactory(gate: new ClosedConcurrencyGate());
        using var client = factory.CreateClient();

        var response = await PostA2AStreamAsync(client, new
        {
            message = new
            {
                role = "user",
                parts = new[] { new { kind = "text", text = "Explain C major." } }
            }
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var events = ReadSseDataLines(await response.Content.ReadAsStringAsync());

        Assert.That(events, Has.Count.EqualTo(1));
        var body = JsonSerializer.Deserialize<JsonElement>(events[0]);
        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32000));
            Assert.That(body.GetProperty("error").GetProperty("message").GetString(), Does.Contain("busy"));
        });
    }

    [Test]
    public async Task A2A_UnsupportedMethod_ReturnsJsonRpcMethodNotFound()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var body = await PostA2AAsync(client, "tasks/get", new { id = "task-1" });

        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32601));
            Assert.That(body.GetProperty("error").GetProperty("message").GetString(), Does.Contain("tasks/get"));
        });
    }

    [Test]
    public async Task A2A_InvalidMessageParams_ReturnsJsonRpcInvalidParams()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var body = await PostA2AAsync(client, "message/send", new
        {
            message = new
            {
                role = "user",
                parts = Array.Empty<object>()
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32602));
            Assert.That(body.GetProperty("error").GetProperty("message").GetString(), Does.Contain("params.message.parts"));
        });
    }

    [Test]
    public async Task Status_UsesApplicationServiceReadiness()
    {
        var fake = new FakeChatApplicationService
        {
            Status = new ChatbotStatus
            {
                IsAvailable = true,
                Message = "test provider ready",
                Timestamp = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        using var factory = CreateFactory(chatService: fake);
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<JsonElement>("/api/chatbot/status");

        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("isAvailable").GetBoolean(), Is.True);
            Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("test provider ready"));
        });
    }

    [Test]
    public async Task Examples_ReturnFivePrompts()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var examples = await client.GetFromJsonAsync<List<string>>("/api/chatbot/examples");

        Assert.That(examples, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(examples, Has.Count.EqualTo(5));
            Assert.That(examples, Has.Some.Contains("voice leading"));
            Assert.That(examples, Has.Some.Contains("modes"));
        });
    }

    [Test]
    public async Task Chat_EmptyMessage_ReturnsBadRequest()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chatbot/chat", new { message = "   " });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("title").GetString(), Does.Contain("validation"));
        Assert.That(body.GetProperty("errors").GetProperty("Message")[0].GetString(), Does.Contain("required"));
    }

    [Test]
    public async Task Chat_MessageOverMaxLength_ReturnsBadRequest()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chatbot/chat", new { message = new string('x', 2001) });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("title").GetString(), Does.Contain("validation"));
            Assert.That(body.GetProperty("errors").GetProperty("Message")[0].GetString(), Does.Contain("maximum length"));
        });
    }

    [Test]
    public async Task Chat_WhenConcurrencyGateIsFull_ReturnsServiceUnavailable()
    {
        using var factory = CreateFactory(gate: new ClosedConcurrencyGate());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chatbot/chat", new { message = "Explain C major." });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("error").GetString(), Does.Contain("Service is busy"));
    }

    [Test]
    public async Task ChatStream_EmptyMessage_EmitsSseError()
    {
        var controller = CreateController();

        await controller.ChatStream(new GaChatbot.Api.Controllers.ChatRequest { Message = "   " }, CancellationToken.None);

        Assert.That(controller.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var events = ReadSseDataLines(ReadResponseBody(controller.Response));
        Assert.That(events, Has.Count.EqualTo(1));

        var error = JsonSerializer.Deserialize<JsonElement>(events[0]);
        Assert.That(error.GetProperty("error").GetString(), Is.EqualTo("Message cannot be empty."));
    }

    [Test]
    public async Task ChatStream_WhenConcurrencyGateIsFull_EmitsSseError()
    {
        using var factory = CreateFactory(gate: new ClosedConcurrencyGate());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chatbot/chat/stream", new { message = "Explain C major." });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/event-stream"));

        var events = await ReadSseDataLinesAsync(response);
        Assert.That(events, Has.Count.EqualTo(1));

        var error = JsonSerializer.Deserialize<JsonElement>(events[0]);
        Assert.That(error.GetProperty("error").GetString(), Is.EqualTo("Service is busy. Please try again in a few seconds."));
    }

    [Test]
    public async Task ChatStream_EmitsRoutingMetadataBeforeTextChunks()
    {
        var fake = new FakeChatApplicationService();
        using var factory = CreateFactory(chatService: fake);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chatbot/chat/stream", new
        {
            message = "Explain C major.",
            conversationHistory = new[]
            {
                new { role = "user", content = "What is the C major scale?" }
            }
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/event-stream"));
        Assert.That(fake.LastRequest, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(fake.LastRequest!.Message, Is.EqualTo("Explain C major."));
            Assert.That(fake.LastRequest.History, Has.Count.EqualTo(1));
            Assert.That(fake.LastRequest.History![0].Role, Is.EqualTo("user"));
        });

        var events = await ReadSseDataLinesAsync(response);

        Assert.That(events, Has.Count.EqualTo(3));
        var routing = JsonSerializer.Deserialize<JsonElement>(events[0]);
        Assert.Multiple(() =>
        {
            Assert.That(routing.GetProperty("type").GetString(), Is.EqualTo("routing"));
            Assert.That(routing.GetProperty("agentId").GetString(), Is.EqualTo("fake-agent"));
            Assert.That(routing.GetProperty("routingMethod").GetString(), Is.EqualTo("fake-route"));
            Assert.That(routing.GetProperty("grounding").GetProperty("source").GetString(), Is.EqualTo("test"));
            Assert.That(routing.GetProperty("trace").GetProperty("steps")[0].GetProperty("name").GetString(), Is.EqualTo("chat.request"));
            Assert.That(events[1], Is.EqualTo("fake answer"));
        });
    }

    [Test]
    public async Task ChatStream_EmitsDoneSentinel()
    {
        using var factory = CreateFactory(chatService: new FakeChatApplicationService());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chatbot/chat/stream", new { message = "Explain C major." });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var events = await ReadSseDataLinesAsync(response);
        Assert.That(events, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(events[^1], Is.EqualTo("[DONE]"));
    }

    [Test]
    public async Task Chat_ForwardsHistoryAndReturnsRoutingMetadata()
    {
        var fake = new FakeChatApplicationService();
        using var factory = CreateFactory(chatService: fake);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chatbot/chat", new
        {
            message = "Continue that idea.",
            conversationHistory = new[]
            {
                new { role = "user", content = "Explain C major." },
                new { role = "assistant", content = "C major uses C D E F G A B." }
            }
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(fake.LastRequest, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(fake.LastRequest!.Message, Is.EqualTo("Continue that idea."));
            Assert.That(fake.LastRequest.History, Has.Count.EqualTo(2));
            Assert.That(fake.LastRequest.History![0].Role, Is.EqualTo("user"));
        });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("naturalLanguageAnswer").GetString(), Is.EqualTo("fake answer"));
            Assert.That(body.GetProperty("agentId").GetString(), Is.EqualTo("fake-agent"));
            Assert.That(body.GetProperty("routingMethod").GetString(), Is.EqualTo("fake-route"));
            Assert.That(body.GetProperty("grounding").GetProperty("source").GetString(), Is.EqualTo("test"));
            Assert.That(body.GetProperty("trace").GetProperty("protocol").GetString(), Does.Contain("otel-genai"));
            Assert.That(body.GetProperty("trace").GetProperty("steps")[0].GetProperty("name").GetString(), Is.EqualTo("chat.request"));
        });
    }

    [Test]
    public async Task AgUiStream_EmitsStandardEventsAndTextChunks()
    {
        var fake = new FakeChatApplicationService();
        using var factory = CreateFactory(chatService: fake);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chatbot/agui/stream", new
        {
            threadId = "thread-test",
            runId = "run-test",
            messages = new[]
            {
                new { role = "user", content = "Explain C major." }
            }
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/event-stream"));

        var body = await response.Content.ReadAsStringAsync();
        var eventTypes = ReadAgUiEventTypes(body);
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("\"type\":\"RUN_STARTED\""));
            Assert.That(body, Does.Contain("\"runId\":\"run-test\""));
            Assert.That(body, Does.Contain("\"type\":\"STEP_STARTED\""));
            Assert.That(body, Does.Contain("\"type\":\"TEXT_MESSAGE_CONTENT\""));
            Assert.That(body, Does.Contain("\"delta\":\"fake answer\""));
            Assert.That(body, Does.Contain("\"type\":\"RUN_FINISHED\""));
            Assert.That(eventTypes.IndexOf("RUN_STARTED"), Is.LessThan(eventTypes.IndexOf("STEP_STARTED")));
            Assert.That(eventTypes.IndexOf("STEP_STARTED"), Is.LessThan(eventTypes.IndexOf("TEXT_MESSAGE_START")));
            Assert.That(eventTypes.IndexOf("TEXT_MESSAGE_START"), Is.LessThan(eventTypes.IndexOf("TEXT_MESSAGE_CONTENT")));
            Assert.That(eventTypes.IndexOf("TEXT_MESSAGE_END"), Is.LessThan(eventTypes.IndexOf("RUN_FINISHED")));
        });
    }

    [Test]
    public async Task AgUiStream_AcceptsShortClientRunId()
    {
        using var factory = CreateFactory(chatService: new FakeChatApplicationService());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chatbot/agui/stream", new
        {
            threadId = "thread-test",
            runId = "r-test",
            messages = new[]
            {
                new { role = "user", content = "Explain C major." }
            }
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadAsStringAsync();
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("\"runId\":\"r-test\""));
            Assert.That(body, Does.Contain("\"messageId\":\"msg_r-test\""));
            Assert.That(body, Does.Contain("\"type\":\"RUN_FINISHED\""));
        });
    }

    [Test]
    public async Task AgUiJson_ForwardsPriorMessagesWithoutDuplicatingCurrentUserTurn()
    {
        var fake = new FakeChatApplicationService();
        using var factory = CreateFactory(chatService: fake);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chatbot/agui/json", new
        {
            threadId = "thread-test",
            runId = "run-test",
            messages = new[]
            {
                new { role = "user", content = "Explain C major." },
                new { role = "assistant", content = "C major contains C, E, and G." },
                new { role = "user", content = "What about minor?" }
            }
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(fake.LastRequest, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(fake.LastRequest!.Message, Is.EqualTo("What about minor?"));
            Assert.That(fake.LastRequest.History, Has.Count.EqualTo(2));
            Assert.That(fake.LastRequest.History!.Select(turn => turn.Content), Does.Not.Contain("What about minor?"));
            Assert.That(fake.LastRequest.History[0].Role, Is.EqualTo("user"));
            Assert.That(fake.LastRequest.History[1].Role, Is.EqualTo("assistant"));
        });
    }

    [Test]
    public async Task AgUiStream_EmptyMessage_ReturnsBadRequestWithoutSseBody()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chatbot/agui/stream", new
        {
            threadId = "thread-test",
            runId = "run-test",
            messages = new[]
            {
                new { role = "user", content = "   " }
            }
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.Empty);
    }

    [Test]
    public async Task AgUiStream_WhenConcurrencyGateIsFull_EmitsRunError()
    {
        using var factory = CreateFactory(gate: new ClosedConcurrencyGate());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chatbot/agui/stream", new
        {
            threadId = "thread-test",
            runId = "run-test",
            messages = new[]
            {
                new { role = "user", content = "Explain C major." }
            }
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadAsStringAsync();
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("\"type\":\"RUN_ERROR\""));
            Assert.That(body, Does.Contain("\"code\":\"SERVICE_BUSY\""));
            Assert.That(body, Does.Contain("Service is busy"));
        });
    }

    [Test]
    public async Task AgUiStream_WhenApplicationServiceThrows_ClosesTextAndEmitsRunError()
    {
        using var factory = CreateFactory(chatService: new ThrowingChatApplicationService());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chatbot/agui/stream", new
        {
            threadId = "thread-test",
            runId = "run-test",
            messages = new[]
            {
                new { role = "user", content = "Explain C major." }
            }
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadAsStringAsync();
        var eventTypes = ReadAgUiEventTypes(body);
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("\"type\":\"RUN_ERROR\""));
            Assert.That(body, Does.Contain("\"code\":\"INTERNAL_ERROR\""));
            Assert.That(eventTypes, Does.Contain("TEXT_MESSAGE_END"));
            Assert.That(eventTypes.IndexOf("TEXT_MESSAGE_END"), Is.LessThan(eventTypes.IndexOf("RUN_ERROR")));
        });
    }

    [Test]
    public async Task CorsPreflight_WhenOriginAllowed_ReturnsCorsHeaders()
    {
        using var factory = CreateFactory(configure: builder =>
        {
            builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:5173");
        });
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/chatbot/status");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins), Is.True);
            Assert.That(origins, Does.Contain("http://localhost:5173"));
        });
    }

    private static WebApplicationFactory<Program> CreateFactory(
        IChatApplicationService? chatService = null,
        ILlmConcurrencyGate? gate = null,
        Action<IWebHostBuilder>? configure = null) =>
        new TestWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                configure?.Invoke(builder);

                builder.ConfigureTestServices(services =>
                {
                    if (chatService is not null)
                    {
                        services.RemoveAll<IChatApplicationService>();
                        services.AddSingleton(chatService);
                    }

                    if (gate is not null)
                    {
                        services.RemoveAll<ILlmConcurrencyGate>();
                        services.AddSingleton(gate);
                    }
                });
            });

    private static ChatbotController CreateController(
        IChatApplicationService? chatService = null,
        ILlmConcurrencyGate? gate = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        return new ChatbotController(
            NullLogger<ChatbotController>.Instance,
            gate ?? new OpenConcurrencyGate(),
            chatService ?? new FakeChatApplicationService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    private static async Task<List<string>> ReadSseDataLinesAsync(HttpResponseMessage response) =>
        ReadSseDataLines(await response.Content.ReadAsStringAsync());

    private static string ReadResponseBody(HttpResponse response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private static List<string> ReadSseDataLines(string body) =>
        [
            .. body
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("data: ", StringComparison.Ordinal))
            .Select(line => line["data: ".Length..])
        ];

    private static List<string> ReadAgUiEventTypes(string body) =>
        [
            .. ReadSseDataLines(body)
                .Select(payload => JsonSerializer.Deserialize<JsonElement>(payload))
                .Select(json => json.GetProperty("type").GetString()!)
        ];

    private static async Task<JsonElement> PostA2AAsync(HttpClient client, string method, object parameters)
    {
        var response = await client.PostAsJsonAsync("/a2a", new
        {
            jsonrpc = "2.0",
            id = "req-1",
            method,
            @params = parameters
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<HttpResponseMessage> PostA2AStreamAsync(HttpClient client, object parameters) =>
        client.PostAsJsonAsync("/a2a", new
        {
            jsonrpc = "2.0",
            id = "stream-1",
            method = "message/stream",
            @params = parameters
        });

    private sealed class FakeChatApplicationService : IChatApplicationService
    {
        public ChatExecutionRequest? LastRequest { get; private set; }

        public ChatbotStatus Status { get; init; } = new()
        {
            IsAvailable = true,
            Message = "fake ready",
            Timestamp = DateTime.UtcNow
        };

        public Task<ChatExecutionResult> ChatAsync(
            ChatExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new ChatExecutionResult(
                "fake answer",
                new AgentRoutingMetadata("fake-agent", 0.75f, "fake-route"),
                new GroundingMetadata("test", "fixture", "unit"),
                CreateFakeTrace()));
        }

        public async IAsyncEnumerable<ChatStreamUpdate> ChatStreamAsync(
            ChatExecutionRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            yield return new ChatStreamUpdate(
                Routing: new AgentRoutingMetadata("fake-agent", 0.75f, "fake-route"),
                Grounding: new GroundingMetadata("test", "fixture", "unit"),
                Trace: CreateFakeTrace());
            yield return new ChatStreamUpdate("fake answer");
            await Task.Yield();
            yield return new ChatStreamUpdate(IsCompleted: true);
        }

        public Task<ChatbotStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Status);

        private static AgenticTrace CreateFakeTrace() =>
            new(
                "trace-test",
                "w3c-trace-context+otel-genai+ag-ui",
                "run-test",
                [
                    new AgenticTraceStep(
                        "chat.request",
                        "completed",
                        0,
                        new Dictionary<string, object?>
                        {
                            ["agent.id"] = "fake-agent"
                        })
                ]);
    }

    private sealed class ThrowingChatApplicationService : IChatApplicationService
    {
        public Task<ChatExecutionResult> ChatAsync(
            ChatExecutionRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");

        public async IAsyncEnumerable<ChatStreamUpdate> ChatStreamAsync(
            ChatExecutionRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatStreamUpdate(
                Routing: new AgentRoutingMetadata("fake-agent", 0.75f, "fake-route"),
                Grounding: new GroundingMetadata("test", "fixture", "unit"));
            yield return new ChatStreamUpdate("partial answer");
            await Task.Yield();
            throw new InvalidOperationException("boom");
        }

        public Task<ChatbotStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatbotStatus
            {
                IsAvailable = true,
                Message = "fake ready",
                Timestamp = DateTime.UtcNow
            });
    }

    private sealed class OpenConcurrencyGate : ILlmConcurrencyGate
    {
        public ValueTask<bool> TryEnterAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(true);

        public void Release()
        {
        }
    }

    private sealed class ClosedConcurrencyGate : ILlmConcurrencyGate
    {
        public ValueTask<bool> TryEnterAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);

        public void Release()
        {
        }
    }
}
