// Namespace deliberately NOT `GA.Business.ML.Tests.Providers` — see the comment
// at the top of InceptionProviderTests.cs for the shadow-resolution gotcha.
namespace GA.Business.ML.Tests.Unit.ProviderTests;

using System.Diagnostics;
using GA.Business.ML.Providers;
using GA.Business.ML.Search;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
///     End-to-end smoke test that drives a real Mercury 2 call through
///     <see cref="LlmMusicalQueryExtractor"/> — the exact code path that
///     production uses when <c>Inception:EnableForQueryExtraction = true</c>.
///     <para>
///     Skips automatically when <c>INCEPTION_API_KEY</c> is not in the
///     environment. CI workflows wire the GitHub secret into the env so this
///     test runs there; developers without an Inception key see it skipped
///     locally. Categorized <c>RequiresInception</c> so the standard CI
///     filter (<c>TestCategory!=Integration&amp;TestCategory!=RequiresModel</c>)
///     doesn't pick it up — the Mercury job opts in explicitly.
///     </para>
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("RequiresInception")]
public class InceptionLiveSmokeTests
{
    [Test]
    public async Task LlmMusicalQueryExtractor_OnMercury_ParsesFuzzyQueryStructure()
    {
        var apiKey = Environment.GetEnvironmentVariable(InceptionProvider.ApiKeyEnvVar);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Assert.Ignore(
                $"{InceptionProvider.ApiKeyEnvVar} not set — skipping live Mercury smoke. " +
                "In CI, the GitHub repo secret of the same name is mapped to the env var.");
        }

        var chatClient = InceptionProvider.CreateChatClient(apiKey!);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var extractor = new LlmMusicalQueryExtractor(
            chatClient,
            cache,
            NullLogger<LlmMusicalQueryExtractor>.Instance);

        // "warm jazz" is the canonical fuzzy-fallback query — typed extractor
        // can't parse it (no chord anchor), so production routes here. Should
        // resolve to chord:null, tags including "jazz", "warm" dropped.
        var sw = Stopwatch.StartNew();
        var result = await extractor.ExtractAsync("warm jazz");
        sw.Stop();

        TestContext.Out.WriteLine(
            $"Mercury latency: {sw.ElapsedMilliseconds} ms | chord={result.ChordSymbol} " +
            $"mode={result.ModeName} tags=[{string.Join(",", result.Tags ?? [])}]");

        Assert.That(result, Is.Not.Null);
        Assert.That(result.ChordSymbol, Is.Null,
            "'warm jazz' has no chord anchor; chord should be null.");
        Assert.That(result.Tags, Is.Not.Null.And.Not.Empty,
            "tags should at minimum contain 'jazz' (the only vocabulary-recognized token).");
        Assert.That(result.Tags!.Any(t => t.Contains("jazz", StringComparison.OrdinalIgnoreCase)),
            Is.True, "tags must include the recognized style word.");

        // 2026-05-13 benchmark: median 369 ms, p95 729 ms over 25 queries.
        // 5 s allows for CI runner variance + cold-start while still catching
        // a regression where Mercury becomes Sonnet-class slow.
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(5000),
            $"Mercury extraction took {sw.ElapsedMilliseconds} ms — well above the " +
            "benchmark p95 of 729 ms. Check rate-limiting / network egress.");
    }

    [Test]
    public async Task LlmMusicalQueryExtractor_OnMercury_ChordQueryReturnsValidStructure()
    {
        var apiKey = Environment.GetEnvironmentVariable(InceptionProvider.ApiKeyEnvVar);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Assert.Ignore($"{InceptionProvider.ApiKeyEnvVar} not set — skipping.");
        }

        var chatClient = InceptionProvider.CreateChatClient(apiKey!);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var extractor = new LlmMusicalQueryExtractor(
            chatClient,
            cache,
            NullLogger<LlmMusicalQueryExtractor>.Instance);

        var result = await extractor.ExtractAsync("Cmaj7 drop2 jazz");

        TestContext.Out.WriteLine(
            $"chord={result.ChordSymbol} mode={result.ModeName} " +
            $"tags=[{string.Join(",", result.Tags ?? [])}]");

        Assert.That(result.ChordSymbol, Is.EqualTo("Cmaj7"),
            "Chord field should carry the canonical symbol.");
        Assert.That(result.PitchClasses, Is.EquivalentTo(new[] { 0, 4, 7, 11 }),
            "ChordPitchClasses.TryParse should resolve Cmaj7 to C major-7 PCs.");
        Assert.That(result.Tags, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Tags!.Any(t => t == "jazz"), Is.True);
        Assert.That(result.Tags!.Any(t => t.Contains("drop", StringComparison.OrdinalIgnoreCase)),
            Is.True, "tags must include 'drop2' (or 'drop-2-voicings' if dehyphenated alias hits).");
    }
}
