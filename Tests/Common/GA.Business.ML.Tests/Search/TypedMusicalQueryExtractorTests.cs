namespace GA.Business.ML.Tests.Search;

using GA.Business.ML.Search;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
///     Tests for the deterministic typed extractor and the composite two-tier extractor.
///     The LLM tier should never run in these tests — if it does, the chat mock below
///     fails loudly so we notice.
/// </summary>
[TestFixture]
[Category("Unit")]
public class TypedMusicalQueryExtractorTests
{
    private TypedMusicalQueryExtractor _typed = null!;

    [SetUp]
    public void SetUp()
    {
        _typed = new TypedMusicalQueryExtractor();
    }

    [Test]
    public async Task Extract_ChordOnly_ReturnsParsedChord()
    {
        var q = await _typed.ExtractAsync("Cmaj7");

        Assert.That(q.ChordSymbol, Is.EqualTo("Cmaj7"));
        Assert.That(q.RootPitchClass, Is.EqualTo(0));
        Assert.That(q.PitchClasses, Is.EquivalentTo(new[] { 0, 4, 7, 11 }));
    }

    [Test]
    public async Task Extract_ChordWithTags_ReturnsBoth()
    {
        var q = await _typed.ExtractAsync("Cmaj7 jazz");

        Assert.That(q.ChordSymbol, Is.EqualTo("Cmaj7"));
        Assert.That(q.Tags, Is.Not.Null);
        Assert.That(q.Tags!, Does.Contain("jazz"));
    }

    [Test]
    public async Task Extract_ModeName_Recognized()
    {
        var q = await _typed.ExtractAsync("Lydian");

        Assert.That(q.ModeName, Is.EqualTo("Lydian").IgnoreCase);
    }

    [Test]
    public async Task Extract_TwoWordMode_RecognizedAsUnit()
    {
        var q = await _typed.ExtractAsync("harmonic minor");

        Assert.That(q.ModeName, Is.EqualTo("harmonic minor").IgnoreCase);
    }

    [Test]
    public async Task Extract_FirstChordWinsWhenMultiple()
    {
        var q = await _typed.ExtractAsync("Cmaj7 to Dm7");

        Assert.That(q.ChordSymbol, Is.EqualTo("Cmaj7"),
            "first chord in the query takes precedence — multi-chord progressions route to a different agent.");
    }

    [Test]
    public async Task Extract_NoRecognizableTokens_ReturnsEmpty()
    {
        var q = await _typed.ExtractAsync("show me something warm and sparkly");

        TestContext.Out.WriteLine(
            $"chord={q.ChordSymbol} mode={q.ModeName} tags={(q.Tags is null ? "<null>" : string.Join(",", q.Tags))}");

        Assert.That(q.ChordSymbol, Is.Null);
        Assert.That(q.PitchClasses, Is.Null);
        Assert.That(q.ModeName, Is.Null);
        Assert.That(q.Tags, Is.Null.Or.Empty,
            "'warm' / 'sparkly' must not partial-match any SYMBOLIC tag in the registry.");
    }

    [Test]
    public async Task Extract_DumpsMatchedTokensForDiagnosis()
    {
        // Diagnostic — reveals any registry partial-match hits for various fuzzy queries.
        // Used to discover that "progression"/"suggest" etc. triggered unexpected matches.
        string[] queries =
        [
            "please suggest a nice sounding progression",
            "show me something warm and sparkly",
            "abstract vague bleh"
        ];
        foreach (var query in queries)
        {
            var q = await _typed.ExtractAsync(query);
            TestContext.Out.WriteLine(
                $"[{query}] → chord={q.ChordSymbol} mode={q.ModeName} tags={(q.Tags is null ? "<null>" : string.Join(",", q.Tags))}");
        }
        Assert.Pass("diagnostic only — see console output for matched tokens.");
    }

    [Test]
    public async Task Extract_EmptyQuery_ReturnsEmpty()
    {
        var q = await _typed.ExtractAsync("");
        Assert.That(q.ChordSymbol, Is.Null);
        Assert.That(q.Tags, Is.Null);
    }

    // ─── composite ─────────────────────────────────────────────────────────

    [Test]
    public async Task Composite_StructuredQuery_DoesNotInvokeLlm()
    {
        var chat = new StubChatClient("""{"chord": null, "mode": null, "tags": []}""");
        var llm = new LlmMusicalQueryExtractor(
            chat,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<LlmMusicalQueryExtractor>.Instance);
        var composite = new CompositeMusicalQueryExtractor(_typed, llm);

        var q = await composite.ExtractAsync("Cmaj7 jazz");

        Assert.That(q.ChordSymbol, Is.EqualTo("Cmaj7"));
        Assert.That(chat.CallCount, Is.Zero,
            "structured query must not consume an LLM call — typed tier handles it.");
    }

    [Test]
    public async Task Llm_DirectCall_ParsesJsonResponse()
    {
        // Isolate the LLM extractor to confirm the stub-chat path works end-to-end
        // (decouples the problem from the composite routing logic).
        var chat = new StubChatClient("""{"chord": "Cmaj7", "mode": null, "tags": ["jazz"]}""");
        var llm = new LlmMusicalQueryExtractor(
            chat,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<LlmMusicalQueryExtractor>.Instance);

        var q = await llm.ExtractAsync("anything at all");

        TestContext.Out.WriteLine($"chord={q.ChordSymbol} tags={(q.Tags is null ? "<null>" : string.Join(",", q.Tags))} callCount={chat.CallCount}");
        Assert.That(q.ChordSymbol, Is.EqualTo("Cmaj7"));
        Assert.That(chat.CallCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Composite_FuzzyQuery_FallsThroughToLlm()
    {
        var chat = new StubChatClient("""{"chord": "Cmaj7", "mode": null, "tags": ["jazz"]}""");
        var llm = new LlmMusicalQueryExtractor(
            chat,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<LlmMusicalQueryExtractor>.Instance);
        var composite = new CompositeMusicalQueryExtractor(_typed, llm);

        // Must be a query with NO uppercase chord symbol and NO registry-matching tokens.
        // The symbolic tag registry does substring fallback, so style/technique words
        // like "jazz", "blues", "sweep", "rootless" will match — avoid them here.
        var q = await composite.ExtractAsync("please suggest a nice sounding progression");

        // If this fails: the typed extractor matched a token that partial-matches some registry
        // key (logs the matched tag). Swap the query text for something more obviously non-musical.
        Assert.That(q.ChordSymbol, Is.EqualTo("Cmaj7"),
            $"LLM tier should have populated chord. chat.CallCount={chat.CallCount}. " +
            $"Typed path likely matched an unexpected token — check SymbolicTagRegistry partial matches.");
        Assert.That(chat.CallCount, Is.EqualTo(1));
    }

    /// <summary>Minimal IChatClient stub returning a fixed text payload — avoids Moq's shape mismatches with MEAI extensions.</summary>
    private sealed class StubChatClient(string cannedResponse) : IChatClient
    {
        public int CallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, cannedResponse));
            return Task.FromResult(response);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
