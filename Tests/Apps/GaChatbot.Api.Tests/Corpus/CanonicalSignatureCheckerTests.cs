namespace GaChatbot.Api.Tests.Corpus;

using NUnit.Framework;
using static CanonicalSignatureChecker;

[TestFixture]
public class CanonicalSignatureCheckerTests
{
    [TestCase("What is Locrian", "what-is-locrian")]
    [TestCase("What chord is C E G", "what-chord-is-c-e-g")]
    [TestCase("WHAT IS MIXOLYDIAN", "what-is-mixolydian")]
    [TestCase("modes of melodi minor", "modes-of-melodi-minor")]
    [TestCase("---hello---", "hello")]
    [TestCase("a/b\\c.d?e!", "a-b-c-d-e")]
    public void ToSlug_MatchesPowershellRecorder(string input, string expected) =>
        // Round-trips with ConvertTo-Slug in record-golden-traces.ps1.
        // If you change either, change both.
        Assert.That(ToSlug(input), Is.EqualTo(expected));

    [Test]
    public void ToSlug_TruncatesAt64Chars()
    {
        var long_ = new string('a', 80);
        Assert.That(ToSlug(long_).Length, Is.LessThanOrEqualTo(64));
    }

    [Test]
    public void Compare_MatchingTrace_ReturnsNull()
    {
        var sig = new Signature(
            SchemaVersion: 1,
            PromptId: "test",
            Prompt: "x",
            Category: "test",
            Steps:
            [
                new StepSignature("chat.request",        "completed", null),
                new StepSignature("orchestration.answer","completed", "skill.modes"),
                new StepSignature("response.emit",       "completed", null),
            ]);

        var trace = ToTrace(
            ("chat.request",         null),
            ("orchestration.answer", new Dictionary<string, string?> { ["agent.id"] = "skill.modes" }),
            ("response.emit",        null));

        Assert.That(Compare(trace, sig), Is.Null);
    }

    [Test]
    public void Compare_AgentIdMismatch_Diagnostic()
    {
        var sig = new Signature(1, "test", "x", "test",
        [
            new StepSignature("orchestration.answer", "completed", "skill.modes"),
        ]);

        var trace = ToTrace(
            ("orchestration.answer",
                new Dictionary<string, string?> { ["agent.id"] = "skill.relativekey" }));

        var msg = Compare(trace, sig);
        Assert.That(msg, Is.Not.Null);
        Assert.That(msg!, Does.Contain("skill.modes"));
        Assert.That(msg, Does.Contain("skill.relativekey"));
    }

    [Test]
    public void Compare_TraceTooShort_Diagnostic()
    {
        var sig = new Signature(1, "test", "x", "test",
        [
            new StepSignature("chat.request",       "completed", null),
            new StepSignature("orchestration.answer","completed", "skill.modes"),
            new StepSignature("response.emit",      "completed", null),
        ]);

        var trace = ToTrace(("chat.request", null));

        var msg = Compare(trace, sig);
        Assert.That(msg, Is.Not.Null);
        Assert.That(msg!, Does.Contain("orchestration.answer"));
        Assert.That(msg, Does.Contain("position 1"));
    }

    [Test]
    public void Compare_WrongStepName_Diagnostic()
    {
        var sig = new Signature(1, "test", "x", "test",
        [
            new StepSignature("chat.request",        "completed", null),
            new StepSignature("orchestration.answer","completed", "skill.modes"),
        ]);

        var trace = ToTrace(
            ("chat.request",            null),
            ("orchestration.fallback",  null));

        var msg = Compare(trace, sig);
        Assert.That(msg, Is.Not.Null);
        Assert.That(msg!, Does.Contain("orchestration.answer"));
        Assert.That(msg, Does.Contain("orchestration.fallback"));
        Assert.That(msg, Does.Contain("position 1"));
    }

    [Test]
    public void Compare_NullAgentIdInSignature_SkipsAgentCheck()
    {
        // Framing steps (chat.request, response.emit) have no agent.id in
        // the signature; comparison must not require one from the trace.
        var sig = new Signature(1, "test", "x", "test",
        [
            new StepSignature("chat.request",  "completed", null),
            new StepSignature("response.emit", "completed", null),
        ]);

        var traceWithNoAttrs = ToTrace(
            ("chat.request",  null),
            ("response.emit", null));
        Assert.That(Compare(traceWithNoAttrs, sig), Is.Null);

        var traceWithIncidentalAgentId = ToTrace(
            ("chat.request",  new Dictionary<string, string?> { ["agent.id"] = "something" }),
            ("response.emit", null));
        Assert.That(Compare(traceWithIncidentalAgentId, sig), Is.Null,
            "signature with null agent.id must not constrain the trace's agent.id");
    }

    [Test]
    public void Compare_TraceLongerThanSignature_StillMatches()
    {
        // Extra steps beyond the signature length are NOT a regression by
        // themselves — they're informational. The signature only constrains
        // the prefix it covers.
        var sig = new Signature(1, "test", "x", "test",
        [
            new StepSignature("chat.request", "completed", null),
        ]);

        var trace = ToTrace(
            ("chat.request",         null),
            ("orchestration.answer", new Dictionary<string, string?> { ["agent.id"] = "skill.modes" }),
            ("response.emit",        null));

        Assert.That(Compare(trace, sig), Is.Null);
    }

    [Test]
    public void ResolveSignaturePath_MissingFile_ReturnsNull()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            var path = ResolveSignaturePath(tmp, "no such prompt");
            Assert.That(path, Is.Null);
        }
        finally
        {
            Directory.Delete(tmp, true);
        }
    }

    [Test]
    public void Load_ParsesGoldenSignature_RoundTrip()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            var slug = "what-is-locrian";
            var dir  = Path.Combine(tmp, slug);
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "_signature.json");
            File.WriteAllText(file, """
            {
              "schemaVersion": 1,
              "promptId": "what-is-locrian",
              "prompt": "What is Locrian",
              "category": "modes",
              "steps": [
                { "name": "chat.request",         "status": "completed", "agentId": null },
                { "name": "orchestration.answer", "status": "completed", "agentId": "skill.modes" },
                { "name": "response.emit",        "status": "completed", "agentId": null }
              ]
            }
            """);

            var resolved = ResolveSignaturePath(tmp, "What is Locrian");
            Assert.That(resolved, Is.EqualTo(file));

            var sig = Load(resolved!);
            Assert.That(sig.PromptId, Is.EqualTo("what-is-locrian"));
            Assert.That(sig.Steps.Count, Is.EqualTo(3));
            Assert.That(sig.Steps[1].AgentId, Is.EqualTo("skill.modes"));
        }
        finally
        {
            Directory.Delete(tmp, true);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────
    private static IReadOnlyList<(string Name, IDictionary<string, string?>? Attributes)> ToTrace(
        params (string Name, IDictionary<string, string?>? Attributes)[] steps) => steps;
}
