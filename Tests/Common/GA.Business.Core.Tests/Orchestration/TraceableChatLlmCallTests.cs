namespace GA.Business.Core.Tests.Orchestration;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;
using GA.Business.Core.Orchestration.Trace;
using GA.Business.ML.Agents;
using Moq;

/// <summary>
/// Pins that <see cref="TraceableChatApplicationService"/> turns each LLM call an agent reports
/// during the request into an <c>llm.call</c> trace step, so the agentic trace shows where the
/// time inside <c>orchestration.answer</c> goes.
/// </summary>
[TestFixture]
public class TraceableChatLlmCallTests
{
    [Test]
    public async Task LlmCallsReportedDuringTheRequest_BecomeTraceSteps()
    {
        var inner = new Mock<IHarmonicChatOrchestrator>();
        inner.Setup(s => s.AnswerAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Yield();
                LlmCallTrace.Report(new LlmCall("theory", "draft", "claude-haiku-5-5", 4200, null, 900, 350, 2, 1200, true));
                LlmCallTrace.Report(new LlmCall("theory", "critique", "claude-haiku-5-5", 3100, null, 1500, 200, 0, 800, false));
                return new ChatResponse("answer", []);
            });
        List<(string Name, string Status, long Ms, IReadOnlyDictionary<string, object?>? Attrs)> steps = [];
        var capture = new Mock<IAgenticTraceCapture>();
        capture.Setup(c => c.AddStep(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Callback<string, string, long, IReadOnlyDictionary<string, object?>?>((n, s, ms, a) => steps.Add((n, s, ms, a)));
        capture.Setup(c => c.StartStep(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Returns(Mock.Of<ITimedStep>());

        await new TraceableChatApplicationService(inner.Object, capture.Object).ChatAsync(new ChatRequest("Why does Dm7 sound sad?"));

        var llm = steps.Where(s => s.Name == "llm.call").ToList();
        Assert.That(llm, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(llm[0].Status, Is.EqualTo("completed"));
            Assert.That(llm[0].Ms, Is.EqualTo(4200));
            Assert.That(llm[0].Attrs!["agent.id"], Is.EqualTo("theory"));
            Assert.That(llm[0].Attrs!["llm.pass"], Is.EqualTo("draft"));
            Assert.That(llm[0].Attrs!["gen_ai.response.model"], Is.EqualTo("claude-haiku-5-5"));
            Assert.That(llm[0].Attrs!["gen_ai.usage.input_tokens"], Is.EqualTo(900L));
            Assert.That(llm[0].Attrs!["gen_ai.usage.output_tokens"], Is.EqualTo(350L));
            Assert.That(llm[0].Attrs!["llm.tool_calls"], Is.EqualTo(2));
            Assert.That(llm[1].Status, Is.EqualTo("error"));
        });
    }

    [Test]
    public async Task CallsReportedAfterTheRequest_AreNotRecorded()
    {
        var inner = new Mock<IHarmonicChatOrchestrator>();
        inner.Setup(s => s.AnswerAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse("answer", []));
        var capture = new Mock<IAgenticTraceCapture>();
        capture.Setup(c => c.StartStep(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Returns(Mock.Of<ITimedStep>());

        await new TraceableChatApplicationService(inner.Object, capture.Object).ChatAsync(new ChatRequest("hi"));
        LlmCallTrace.Report(new LlmCall("theory", "single", null, 10, null, null, null, 0, 0, true));

        capture.Verify(c => c.AddStep("llm.call", It.IsAny<string>(), It.IsAny<long>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()), Times.Never);
    }
}
