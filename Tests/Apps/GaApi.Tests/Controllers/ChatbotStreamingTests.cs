namespace GaApi.Tests.Controllers;

using System.Collections.Concurrent;
using System.Net.Http.Json;
using GA.Business.ML.Agents.Intents;
using GA.Business.ML.Extensions;
using GaApi.Hubs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using AiChatResponse = Microsoft.Extensions.AI.ChatResponse;

/// <summary>
/// Every chat transport must stream an LLM agent's answer as the model generates it: the
/// SSE endpoint behind the /chatbot page, the AG-UI SSE endpoint, and the SignalR hub. They
/// used to wait for the whole answer and then chunk it: on Haiku every frame arrived together
/// after about 30 s. The fake model here yields its first token and then holds the rest until
/// the test has seen that token on the transport.
/// </summary>
[TestFixture]
public class ChatbotStreamingTests
{
    private const string Prompt = "Who was Wes Montgomery and what made his octave technique special?";
    private const string FirstToken = "Wes Montgomery was a jazz guitarist";

    [Test]
    [CancelAfter(60_000)]
    public async Task Sse_FirstTextFrameArrivesBeforeTheModelFinishes(CancellationToken cancellationToken)
    {
        var model = new GatedStreamingChatClient();
        var filterCall = new HangingHandler();
        using var factory = CreateFactory(model, filterCall);
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chatbot/chat/stream")
        {
            Content = JsonContent.Create(new { message = Prompt }),
        };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancellationToken));

        var routing = await ReadFrameAsync(reader, cancellationToken);
        var firstText = await ReadFrameAsync(reader, cancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(routing, Does.StartWith("{\"type\":\"routing\""), "the routing frame comes first, as the page expects");
            Assert.That(model.Finished, Is.False, "the first text frame must not wait for the model to finish");
            Assert.That(filterCall.Completed, Is.False,
                "the first text frame must not wait for the filter extraction, which only steers tab answers");
            Assert.That(firstText, Does.Contain(FirstToken));
        });

        model.Release();
        var rest = await reader.ReadToEndAsync(cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(rest, Does.Contain("octaves"), "the tokens after the gate are streamed too");
            Assert.That(rest, Does.Contain("data: [DONE]"));
            Assert.That(rest, Does.Not.Contain("event: error"));
        });
        filterCall.Release();
    }

    [Test]
    [CancelAfter(60_000)]
    public async Task AgUi_FirstTextEventArrivesBeforeTheModelFinishes(CancellationToken cancellationToken)
    {
        var model = new GatedStreamingChatClient();
        var filterCall = new HangingHandler();
        using var factory = CreateFactory(model, filterCall);
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chatbot/agui/stream")
        {
            Content = JsonContent.Create(new
            {
                threadId = "thread-1",
                runId = "run-1",
                messages = new[] { new { role = "user", content = Prompt } },
            }),
        };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancellationToken));

        string frame;
        do
        {
            frame = await ReadFrameAsync(reader, cancellationToken);
        }
        while (!frame.Contains("\"TEXT_MESSAGE_CONTENT\"", StringComparison.Ordinal));

        Assert.Multiple(() =>
        {
            Assert.That(model.Finished, Is.False, "the first text event must not wait for the model to finish");
            Assert.That(filterCall.Completed, Is.False,
                "the first text event must not wait for the filter extraction, which only steers tab answers");
            Assert.That(frame, Does.Contain(FirstToken));
        });

        model.Release();
        var rest = await reader.ReadToEndAsync(cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(rest, Does.Contain("octaves"));
            Assert.That(rest, Does.Contain("\"RUN_FINISHED\""));
            Assert.That(rest, Does.Not.Contain("\"RUN_ERROR\""));
        });
        filterCall.Release();
    }

    [Test]
    [CancelAfter(60_000)]
    public async Task SignalR_FirstChunkArrivesBeforeTheModelFinishes(CancellationToken cancellationToken)
    {
        var model = new GatedStreamingChatClient();
        var filterCall = new HangingHandler();
        using var factory = CreateFactory(model, filterCall);
        using var scope = factory.Services.CreateScope();
        var caller = new RecordingCaller();
        var hub = ActivatorUtilities.CreateInstance<ChatbotHub>(scope.ServiceProvider);
        hub.Context = HubContext($"hub-stream-{Guid.NewGuid():N}");
        hub.Clients = HubClients(caller);

        var send = hub.SendMessage(Prompt);
        await caller.FirstChunk.WaitAsync(cancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(model.Finished, Is.False, "the first chunk must not wait for the model to finish");
            Assert.That(filterCall.Completed, Is.False,
                "the first chunk must not wait for the filter extraction, which only steers tab answers");
            Assert.That(caller.Methods[0], Is.EqualTo("MessageRoutingMetadata"), "routing goes out first");
            Assert.That(caller.Calls.First(call => call.Method == "ReceiveMessageChunk").Args[0], Is.EqualTo(FirstToken));
        });

        model.Release();
        await send.WaitAsync(cancellationToken);
        Assert.Multiple(() =>
        {
            Assert.That(caller.Methods[^1], Is.EqualTo("MessageComplete"));
            Assert.That((string?)caller.Calls[^1].Args[0], Does.Contain("octaves"));
            Assert.That(caller.Methods, Does.Not.Contain("Error"));
        });
        filterCall.Release();
    }

    /// <summary>Returns the data of the next SSE frame.</summary>
    private static async Task<string> ReadFrameAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var data = new List<string>();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                data.Add(line[6..]);
            }
            else if (line.Length == 0 && data.Count > 0)
            {
                return string.Join('\n', data);
            }
        }

        throw new InvalidOperationException("The stream ended before the next frame.");
    }

    internal static HubCallerContext HubContext(string connectionId, CancellationToken connectionAborted = default)
    {
        var context = new Mock<HubCallerContext>();
        context.Setup(x => x.ConnectionId).Returns(connectionId);
        context.Setup(x => x.ConnectionAborted).Returns(connectionAborted);
        return context.Object;
    }

    internal static IHubCallerClients HubClients(ISingleClientProxy caller)
    {
        var clients = new Mock<IHubCallerClients>();
        clients.Setup(x => x.Caller).Returns(caller);
        return clients.Object;
    }

    /// <summary>Records what the hub sends to its caller.</summary>
    internal sealed class RecordingCaller : ISingleClientProxy
    {
        private readonly ConcurrentQueue<(string Method, object?[] Args)> _calls = new();
        private readonly TaskCompletionSource _firstChunk = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<(string Method, object?[] Args)> Calls => [.. _calls];

        public IReadOnlyList<string> Methods => [.. _calls.Select(call => call.Method)];

        public Task FirstChunk => _firstChunk.Task;

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            _calls.Enqueue((method, args));
            if (method == "ReceiveMessageChunk") _firstChunk.TrySetResult();
            return Task.CompletedTask;
        }

        public Task<T> InvokeCoreAsync<T>(string method, object?[] args, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static WebApplicationFactory<Program> CreateFactory(IChatClient model, HangingHandler filterCall) =>
        new TestWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            // Unreachable Ollama: any call the fakes miss fails fast instead of reaching a local model.
            builder.UseSetting("Ollama:Endpoint", "http://127.0.0.1:9");
            builder.UseSetting("Ollama:BaseUrl", "http://127.0.0.1:9");
            builder.UseSetting("IX:External:Enabled", "false");
            builder.ConfigureTestServices(services =>
            {
                // The only intent declines, so the orchestrator falls through to the LLM agents.
                services.RemoveAll<IIntent>();
                services.AddSingleton<IIntent>(new DecliningIntent());
                var embeddings = new ConstantEmbeddingGenerator();
                services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
                services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(embeddings);
                services.RemoveAll<IEmbeddingGeneratorFactory>();
                services.AddSingleton<IEmbeddingGeneratorFactory>(new ConstantEmbeddingGeneratorFactory(embeddings));
                services.RemoveAll<IChatClient>();
                services.AddSingleton(model);
                // The filter extraction's raw Ollama call hangs until the request ends.
                services.AddHttpClient("ollama").ConfigurePrimaryHttpMessageHandler(() => filterCall);
            });
        });

    private sealed class HangingHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Completed { get; private set; }

        /// <summary>Fails the pending call, so the host shuts down without waiting for its timeout.</summary>
        public void Release() => _released.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                await _released.Task.WaitAsync(cancellationToken);
                throw new HttpRequestException("filter extraction released by the test");
            }
            finally
            {
                Completed = true;
            }
        }
    }

    /// <summary>
    /// Answers routing and filter calls at once. A streamed answer yields its first token,
    /// then waits for <see cref="Release"/> before the rest.
    /// </summary>
    private sealed class GatedStreamingChatClient : IChatClient
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Finished { get; private set; }

        public void Release() => _gate.TrySetResult();

        public Task<AiChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiChatResponse(new ChatMessage(ChatRole.Assistant, "{}")));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, FirstToken);
            await _gate.Task.WaitAsync(cancellationToken);
            yield return new ChatResponseUpdate(ChatRole.Assistant, " who played melodies in octaves with his thumb.");
            Finished = true;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class DecliningIntent : IIntent
    {
        public string Id => "skill.declining";
        public string Description => "Declines every message";
        public IReadOnlyList<string> ExamplePrompts => [Prompt];

        public Task<IntentResult> ExecuteAsync(string query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new IntentResult("declined", Confidence: 0.1f, Declined: true));
    }

    private sealed class ConstantEmbeddingGeneratorFactory(ConstantEmbeddingGenerator embeddings) : IEmbeddingGeneratorFactory
    {
        public IEmbeddingGenerator<string, Embedding<float>> Create(string purpose) => embeddings;
    }

    // Every text embeds to the same vector, so the only registered intent always scores 1.0.
    private sealed class ConstantEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(
                values.Select(_ => new Embedding<float>(new float[] { 1f, 0f, 0f }))));

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
