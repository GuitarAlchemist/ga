namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Search;
using GA.Domain.Services.Fretboard.Voicings.Core;

/// <summary>
///     The metadata-filter <b>predicate</b> parity tripwire (ADR-0002). The CPU and GPU strategies must
///     admit/reject the same voicings <i>at the filter step</i>, because both now cross the same shared
///     seams (<see cref="VoicingFilterEngine"/> + <see cref="VoicingComfortFilter"/>). This guards against
///     a strategy re-growing a private, drifted filter copy — the original sin this refactor killed.
///     <para>
///         <b>Scope — what this does NOT test, by design.</b> This is <i>predicate</i> parity, not
///         result-set parity. The strategies <i>score</i> differently (CPU: <c>TextEmbedding ?? Embedding</c>
///         + symbolic boosting; GPU: musical <c>Embedding</c>), so a score-ordered <c>Take(limit)</c>
///         returns different top-K subsets once survivors exceed <c>limit</c>. We therefore run at
///         <c>limit ≥ corpus</c> so truncation never bites, and assert the <b>filter-pass set</b> — not
///         ranking, scoring, or truncation (asserting result-set parity under truncation would assert
///         behaviour that does not hold). Filtering also happens in managed C# <i>before</i> the ILGPU
///         kernel, so kernel-vs-host scoring divergence is irrelevant here and out of scope (on CPU-only
///         CI the GPU strategy falls back to a managed loop anyway).
///     </para>
///     We assert both strategies equal an <i>independently computed</i> expected set (not just each other),
///     so the tripwire also catches both paths drifting together.
/// </summary>
[TestFixture]
public class CpuGpuFilterParityTests
{
    private const int EmbeddingDim = 216; // OPTIC-K v1.6 length — routes through partition cosine.

    public static IEnumerable<TestCaseData> FilterMatrix()
    {
        yield return new TestCaseData(new VoicingSearchFilters()).SetName("empty");
        yield return new TestCaseData(new VoicingSearchFilters(Difficulty: "easy")).SetName("difficulty");
        yield return new TestCaseData(new VoicingSearchFilters(VoicingType: "drop2")).SetName("voicingType_contains");
        yield return new TestCaseData(new VoicingSearchFilters(Tags: ["jazz"])).SetName("tags_single");
        yield return new TestCaseData(new VoicingSearchFilters(Tags: ["jazz", "shell"])).SetName("tags_all_must_match");
        yield return new TestCaseData(new VoicingSearchFilters(RequireBarreChord: true)).SetName("barre");
        yield return new TestCaseData(new VoicingSearchFilters(IsRootless: true)).SetName("rootless_phase3");
        yield return new TestCaseData(new VoicingSearchFilters(Difficulty: "easy", Tags: ["jazz"])).SetName("combined_metadata");
        yield return new TestCaseData(new VoicingSearchFilters(MinComfortScore: 0.5)).SetName("comfort_min_score");
        yield return new TestCaseData(new VoicingSearchFilters(MustBeErgonomic: true)).SetName("comfort_ergonomic");
    }

    [TestCaseSource(nameof(FilterMatrix))]
    public async Task Cpu_And_Gpu_Admit_The_Same_Set(VoicingSearchFilters filters)
    {
        var cpu = new CpuVoicingSearchStrategy();
        using var gpu = new GpuVoicingSearchStrategy();
        Assume.That(gpu.IsAvailable, Is.True,
            "GPU strategy (ILGPU CPU accelerator) unavailable in this environment; skipping parity check.");

        var cpuSet = await SurvivorsAsync(cpu, filters);
        var gpuSet = await SurvivorsAsync(gpu, filters);

        // Independently computed expectation: a voicing passes iff the shared metadata predicate AND the
        // shared comfort predicate accept it. Asserting against this (not just CPU==GPU) catches both
        // strategies drifting together, and pins the seam each is supposed to route through.
        var expected = Corpus()
            .Where(v => VoicingFilterEngine.Matches(v, filters) && VoicingComfortFilter.Matches(v.Diagram, filters))
            .Select(v => v.Id)
            .ToHashSet();

        Assert.Multiple(() =>
        {
            Assert.That(cpuSet, Is.EquivalentTo(expected),
                "CPU filter-pass set diverged from the shared VoicingFilterEngine + VoicingComfortFilter predicate.");
            Assert.That(gpuSet, Is.EquivalentTo(expected),
                "GPU filter-pass set diverged from the shared predicate — a strategy has re-grown a private " +
                "filter that bypasses the shared seam (ADR-0002).");
        });
    }

    private static async Task<HashSet<string>> SurvivorsAsync(IVoicingSearchStrategy strategy, VoicingSearchFilters filters)
    {
        await strategy.InitializeAsync(Corpus());
        // limit deliberately >= corpus size so the score-ordered Take never truncates — this is a
        // predicate test; truncation/scoring divergence is out of scope (see class doc + ADR-0002).
        var results = await strategy.HybridSearchAsync(new double[EmbeddingDim], filters, limit: 100);
        return results.Select(r => r.Document.Id).ToHashSet();
    }

    // Small, deliberately varied corpus so the filters actually discriminate.
    private static List<VoicingEmbedding> Corpus() =>
    [
        V("v-jazz-drop2", voicingType: "drop2 closed", tags: ["jazz", "shell"], difficulty: "easy",   diagram: "x35453"),
        V("v-jazz-open",  voicingType: "open",         tags: ["jazz"],          difficulty: "easy",   diagram: "x32010"),
        V("v-rock-barre", voicingType: "barre",        tags: ["rock"],          difficulty: "hard",   diagram: "133211", barre: true),
        V("v-folk-open",  voicingType: "open",         tags: ["folk"],          difficulty: "easy",   diagram: "320003"),
        V("v-rootless",   voicingType: "drop3",        tags: ["jazz"],          difficulty: "hard",   diagram: "x54535", rootless: true),
        V("v-shell",      voicingType: "shell",        tags: ["jazz", "shell"], difficulty: "medium", diagram: "x35430"),
    ];

    private static VoicingEmbedding V(
        string id, string voicingType, string[] tags, string difficulty, string diagram,
        bool barre = false, bool rootless = false) => new(
        Id: id, ChordName: "Cmaj7", VoicingType: voicingType, Position: "open", Difficulty: difficulty,
        ModeName: null, ModalFamily: null, PossibleKeys: [], SemanticTags: tags, PrimeFormId: "4-27",
        TranslationOffset: 0, Diagram: diagram, MidiNotes: [48, 52, 55, 59], PitchClassSet: "{0,4,7,11}",
        IntervalClassVector: "<101220>", MinFret: 0, MaxFret: 5, BarreRequired: barre, HandStretch: 2,
        StackingType: "tertian", RootPitchClass: 0, MidiBassNote: 48, HarmonicFunction: "tonic",
        IsNaturallyOccurring: true, ConsonanceScore: 0.8, BrightnessScore: 0.5, IsRootless: rootless,
        HasGuideTones: true, Inversion: 0, TopPitchClass: 11, TexturalDescription: "warm",
        DoubledTones: [], AlternateNames: [], OmittedTones: [], CagedShape: "C", Description: "test",
        Embedding: new double[EmbeddingDim], TextEmbedding: null);
}
