namespace GA.Business.ML.Tests.Corpus;

using System.Text.Json;
using System.Text.Json.Serialization;
using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Skills;
using GA.Domain.Core.Primitives.Notes;

/// <summary>
///     Runs the held-out corpus against the deterministic seams that exist
///     today and reports a pass/fail matrix, without touching production
///     behaviour.
/// </summary>
/// <remarks>
///     <para>
///         The corpus states what a musically correct system must answer. The
///         product does not answer all of it yet - #614, #567 and #554 are open.
///         So this fixture does two separate jobs, and keeping them separate is
///         what lets the expectations stay strict:
///     </para>
///     <list type="number">
///         <item>
///             It computes the matrix and writes it as machine-readable evidence
///             (<c>state/quality/progression-corpus/progression-corpus-matrix.json</c>),
///             recording every check as pass, fail or blocked with the issue it
///             is blocked on.
///         </item>
///         <item>
///             It gates on <em>movement</em> against the committed matrix: a
///             check that passes today and fails tomorrow fails the build. A
///             check that is failing today keeps failing without breaking the
///             build, because the honest answer is "the product is wrong here",
///             not "the expectation was too strict".
///         </item>
///     </list>
///     <para>
///         No key detection or harmonic analysis is re-implemented here. Each
///         check calls one production seam and compares its output to corpus
///         data. Where no seam exists yet, the check is recorded as
///         <c>blocked</c> rather than quietly omitted.
///     </para>
///     <para>
///         Regenerate the artifact after an intentional change with
///         <c>GA_CORPUS_WRITE_MATRIX=1 dotnet test --filter
///         FullyQualifiedName~ProgressionCorpusMatrixTests</c>, then commit the
///         updated file.
///     </para>
/// </remarks>
[TestFixture]
public class ProgressionCorpusMatrixTests
{
    private const string Pass = "pass";
    private const string Fail = "fail";
    private const string Blocked = "blocked";

    private const string WriteEnvVar = "GA_CORPUS_WRITE_MATRIX";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>Corpus quality label to the quality the improvisation seam should infer.</summary>
    private static readonly IReadOnlyDictionary<string, ImprovisationSkill.QualityKind> ExpectedQualityKind =
        new Dictionary<string, ImprovisationSkill.QualityKind>
        {
            ["major-triad"] = ImprovisationSkill.QualityKind.Major,
            ["minor-triad"] = ImprovisationSkill.QualityKind.Minor,
            ["dominant-7"] = ImprovisationSkill.QualityKind.Dominant7,
            ["major-7"] = ImprovisationSkill.QualityKind.Major7,
            ["minor-7"] = ImprovisationSkill.QualityKind.Minor7,
            ["half-diminished-7"] = ImprovisationSkill.QualityKind.HalfDiminished
        };

    // ── The matrix ───────────────────────────────────────────────────────────

    [Test]
    public void Matrix_IsDeterministic()
    {
        var first = JsonSerializer.Serialize(BuildMatrix(), WriteOptions);
        var second = JsonSerializer.Serialize(BuildMatrix(), WriteOptions);

        Assert.That(second, Is.EqualTo(first),
            "the matrix must depend only on the corpus and the seams, never on ambient state");
    }

    [Test]
    public void Matrix_CoversEveryCaseAndEveryDeclaredCheck()
    {
        var matrix = BuildMatrix();
        var corpus = ProgressionCorpus.Load();

        Assert.Multiple(() =>
        {
            Assert.That(matrix.Cases.Select(c => c.Id),
                Is.EqualTo(corpus.Cases.Select(c => c.Id)),
                "every corpus case must appear in the matrix, in corpus order");

            foreach (var c in matrix.Cases)
                Assert.That(c.Checks, Is.Not.Empty, $"{c.Id}: no checks recorded");

            Assert.That(matrix.Totals.Pass + matrix.Totals.Fail + matrix.Totals.Blocked,
                Is.EqualTo(matrix.Cases.Sum(c => c.Checks.Count)), "totals must reconcile");
        });
    }

    /// <summary>
    ///     Writes the evidence artifact. Opt-in, so an ordinary test run never
    ///     dirties the working tree.
    /// </summary>
    [Test]
    public void Matrix_WriteEvidenceArtifact()
    {
        if (Environment.GetEnvironmentVariable(WriteEnvVar) != "1")
        {
            Assert.Ignore($"set {WriteEnvVar}=1 to regenerate {ProgressionCorpus.MatrixPath}");
            return;
        }

        System.IO.Directory.CreateDirectory(ProgressionCorpus.EvidenceDirectory);
        File.WriteAllText(
            ProgressionCorpus.MatrixPath,
            JsonSerializer.Serialize(BuildMatrix(), WriteOptions) + Environment.NewLine);

        TestContext.Out.WriteLine($"wrote {ProgressionCorpus.MatrixPath}");
    }

    /// <summary>
    ///     The regression gate. Anything that passes in the committed matrix must
    ///     still pass; anything that starts passing is reported so the artifact
    ///     can be refreshed, but does not fail the build.
    /// </summary>
    [Test]
    public void Matrix_HasNotRegressedAgainstTheCommittedArtifact()
    {
        if (!File.Exists(ProgressionCorpus.MatrixPath))
        {
            Assert.Fail(
                $"no committed matrix at {ProgressionCorpus.MatrixPath}. " +
                $"Generate it with {WriteEnvVar}=1 and commit the result.");
        }

        var committed = JsonSerializer.Deserialize<Matrix>(
                            File.ReadAllText(ProgressionCorpus.MatrixPath), ReadOptions)
                        ?? throw new InvalidOperationException("committed matrix deserialised to null");

        var current = BuildMatrix();

        var committedByKey = Flatten(committed);
        var currentByKey = Flatten(current);

        var missing = committedByKey.Keys.Except(currentByKey.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var added = currentByKey.Keys.Except(committedByKey.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        var regressions = committedByKey
            .Where(kv => kv.Value == Pass &&
                         currentByKey.TryGetValue(kv.Key, out var now) && now != Pass)
            .Select(kv => $"{kv.Key}: pass -> {currentByKey[kv.Key]}")
            .Order(StringComparer.Ordinal)
            .ToList();

        var improvements = committedByKey
            .Where(kv => kv.Value != Pass &&
                         currentByKey.TryGetValue(kv.Key, out var now) && now == Pass)
            .Select(kv => $"{kv.Key}: {kv.Value} -> pass")
            .Order(StringComparer.Ordinal)
            .ToList();

        foreach (var improvement in improvements)
            TestContext.Out.WriteLine($"IMPROVED {improvement}");

        if (improvements.Count > 0)
        {
            TestContext.Out.WriteLine(
                $"{improvements.Count} check(s) now pass. Refresh the artifact with {WriteEnvVar}=1.");
        }

        Assert.Multiple(() =>
        {
            Assert.That(regressions, Is.Empty,
                "checks that used to pass now fail:" + Environment.NewLine +
                string.Join(Environment.NewLine, regressions));
            Assert.That(missing, Is.Empty,
                $"checks in the committed matrix vanished (regenerate with {WriteEnvVar}=1): " +
                string.Join(", ", missing));
            Assert.That(added, Is.Empty,
                $"new checks are not in the committed matrix (regenerate with {WriteEnvVar}=1): " +
                string.Join(", ", added));
        });
    }

    /// <summary>
    ///     Reports the current state as readable output, so a run of this fixture
    ///     is a usable status report and not only a gate.
    /// </summary>
    [Test]
    public void Matrix_ReportCurrentState()
    {
        var matrix = BuildMatrix();

        TestContext.Out.WriteLine(
            $"progression corpus {matrix.CorpusVersion} - " +
            $"{matrix.Totals.Pass} pass / {matrix.Totals.Fail} fail / {matrix.Totals.Blocked} blocked");

        foreach (var c in matrix.Cases)
        {
            TestContext.Out.WriteLine($"  {c.Id} [{c.Category}]");
            foreach (var check in c.Checks)
            {
                var suffix = check.BlockedOn is { Count: > 0 }
                    ? $" (blocked on {string.Join(", ", check.BlockedOn.Select(i => "#" + i))})"
                    : string.Empty;
                TestContext.Out.WriteLine($"    {check.Status,-7} {check.CheckName}{suffix} :: {check.Observed}");
            }
        }

        Assert.Pass();
    }

    // ── Matrix construction ──────────────────────────────────────────────────

    private static Dictionary<string, string> Flatten(Matrix matrix) =>
        matrix.Cases
            .SelectMany(c => c.Checks.Select(ch => (Key: $"{c.Id}/{ch.CheckName}", ch.Status)))
            .ToDictionary(x => x.Key, x => x.Status, StringComparer.Ordinal);

    private static Matrix BuildMatrix()
    {
        var corpus = ProgressionCorpus.Load();
        var cases = corpus.Cases.Select(BuildCase).ToList();
        var all = cases.SelectMany(c => c.Checks).ToList();

        return new Matrix(
            Artifact: "progression-corpus-matrix",
            ArtifactVersion: "1.0.0",
            Issue: corpus.Issue,
            ParentIssue: corpus.ParentIssue,
            CorpusId: corpus.CorpusId,
            CorpusVersion: corpus.CorpusVersion,
            SchemaVersion: corpus.SchemaVersion,
            Note: "Deterministic: no clock, no network, no randomness. Regenerate with " +
                  $"{WriteEnvVar}=1. 'blocked' means no deterministic seam exists in this " +
                  "assembly yet, not that the expectation is optional.",
            Seams: Seams,
            Totals: new Totals(
                all.Count(c => c.Status == Pass),
                all.Count(c => c.Status == Fail),
                all.Count(c => c.Status == Blocked)),
            Cases: cases);
    }

    private static readonly IReadOnlyList<Seam> Seams =
    [
        new("key.identify", "GA.Business.ML.Agents.KeyIdentificationService.Identify",
            "Scores all 30 major/minor keys on root + triad quality. Cannot express modal centres."),
        new("improvisation.quality", "GA.Business.ML.Agents.Skills.ImprovisationSkill.InferQuality",
            "Classifies chord quality from the written symbol."),
        new("improvisation.arpeggio", "GA.Business.ML.Agents.Skills.ImprovisationSkill.ArpeggioFor",
            "Builds the canonical arpeggio symbol for a root plus quality."),
        new("tuning.parse", "GA.Domain.Core.Primitives.Notes.PitchCollection.Parse",
            "Parses scientific-pitch open strings into domain pitches.")
    ];

    private static MatrixCase BuildCase(ProgressionCase c)
    {
        var checks = new List<CheckResult>();
        checks.AddRange(KeyChecks(c));
        checks.AddRange(ImprovisationChecks(c));
        checks.AddRange(SpellingChecks(c));
        checks.Add(TuningCheck(c));
        checks.AddRange(BlockedChecks());

        return new MatrixCase(c.Id, c.Category, checks.OrderBy(x => x.CheckName, StringComparer.Ordinal).ToList());
    }

    private static IEnumerable<CheckResult> KeyChecks(ProgressionCase c)
    {
        var candidates = KeyIdentificationService.Identify(c.Input.CanonicalChords);
        var topScore = candidates.Count == 0 ? 0 : candidates.Max(x => x.MatchCount);
        var topTied = candidates.Where(x => x.MatchCount == topScore)
            .Select(x => x.Key).Order(StringComparer.Ordinal).ToList();
        var observed = topTied.Count == 0 ? "(no candidate)" : string.Join(" | ", topTied);

        yield return new CheckResult(
            "key.top_candidate_acceptable", "key.identify",
            topTied.Intersect(c.Expected.AcceptableTonalCenters, StringComparer.OrdinalIgnoreCase).Any()
                ? Pass : Fail,
            $"top-tied: {observed}",
            $"one of: {string.Join(" | ", c.Expected.AcceptableTonalCenters)}",
            BlockedOn: null);

        var forbiddenHits = topTied
            .Intersect(c.Forbidden.TonalCenters, StringComparer.OrdinalIgnoreCase).ToList();

        yield return new CheckResult(
            "key.forbidden_center_excluded", "key.identify",
            forbiddenHits.Count == 0 ? Pass : Fail,
            forbiddenHits.Count == 0 ? $"top-tied: {observed}" : $"forbidden in top-tied: {string.Join(" | ", forbiddenHits)}",
            $"none of: {string.Join(" | ", c.Forbidden.TonalCenters)}",
            BlockedOn: null);

        if (c.Uncertainty.ExpectedBehavior == "confident")
        {
            yield return new CheckResult(
                "key.single_reading_when_confident", "key.identify",
                topTied.Count == 1 ? Pass : Fail,
                $"{topTied.Count} tied candidate(s): {observed}",
                "exactly 1 top candidate",
                BlockedOn: null);
        }
        else
        {
            var offersAll = c.Expected.AcceptableTonalCenters
                .All(a => topTied.Contains(a, StringComparer.OrdinalIgnoreCase));

            yield return new CheckResult(
                "key.alternatives_when_ambiguous", "key.identify",
                topTied.Count >= c.Uncertainty.MinAlternatives && offersAll ? Pass : Fail,
                $"{topTied.Count} tied candidate(s): {observed}",
                $"at least {c.Uncertainty.MinAlternatives}, including all of: " +
                string.Join(" | ", c.Expected.AcceptableTonalCenters),
                BlockedOn: null);
        }
    }

    private static IEnumerable<CheckResult> ImprovisationChecks(ProgressionCase c)
    {
        var qualityMismatches = new List<string>();
        var forbiddenArpeggios = new List<string>();

        for (var i = 0; i < c.Input.CanonicalChords.Count; i++)
        {
            var symbol = c.Input.CanonicalChords[i];
            var quality = ImprovisationSkill.InferQuality(symbol);
            var expectedKind = ExpectedQualityKind[c.Expected.RequiredChordTones[i].Quality];

            if (quality.Kind != expectedKind)
                qualityMismatches.Add($"{symbol}: {quality.Kind} != {expectedKind}");

            var arpeggio = ImprovisationSkill.ArpeggioFor(ImprovisationSkill.ExtractRoot(symbol), quality);
            var hit = c.Forbidden.Facts.FirstOrDefault(f =>
                arpeggio.Contains(f, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
                forbiddenArpeggios.Add($"{symbol} -> '{arpeggio}' contains forbidden '{hit}'");
        }

        yield return new CheckResult(
            "improvisation.quality_from_written_symbol", "improvisation.quality",
            qualityMismatches.Count == 0 ? Pass : Fail,
            qualityMismatches.Count == 0 ? "all chord qualities classified from the written symbol"
                : string.Join("; ", qualityMismatches),
            "each chord's inferred quality matches the quality written in the symbol",
            BlockedOn: null);

        yield return new CheckResult(
            "improvisation.arpeggio_symbol_is_canonical", "improvisation.arpeggio",
            forbiddenArpeggios.Count == 0 ? Pass : Fail,
            forbiddenArpeggios.Count == 0 ? "no arpeggio symbol matched a forbidden fact"
                : string.Join("; ", forbiddenArpeggios),
            "no arpeggio symbol contains a forbidden string (e.g. the #567 'Amm7' concatenation)",
            BlockedOn: null);
    }

    private static IEnumerable<CheckResult> SpellingChecks(ProgressionCase c)
    {
        if (c.Input.SpellingEquivalenceGroups.Count == 0) yield break;

        var disagreements = new List<string>();

        foreach (var group in c.Input.SpellingEquivalenceGroups)
        {
            var reference = Fingerprint(group.Canonical);
            foreach (var spelling in group.Spellings.Where(s => s != group.Canonical))
            {
                var actual = Fingerprint(spelling);
                if (actual != reference)
                    disagreements.Add($"'{spelling}' -> {actual} != '{group.Canonical}' -> {reference}");
            }
        }

        yield return new CheckResult(
            "spelling.equivalent_spellings_agree", "key.identify",
            disagreements.Count == 0 ? Pass : Fail,
            disagreements.Count == 0 ? "every spelling resolved identically to its canonical form"
                : string.Join("; ", disagreements),
            "spelled-out and shorthand accidentals resolve identically (GuitarAlchemist/ga#554)",
            BlockedOn: disagreements.Count == 0 ? null : [554]);

        static string Fingerprint(string chordSymbol)
        {
            var candidates = KeyIdentificationService.Identify([chordSymbol]);
            if (candidates.Count == 0) return "(unparsed)";

            var top = candidates.Max(x => x.MatchCount);
            return string.Join(",", candidates.Where(x => x.MatchCount == top)
                .Select(x => x.Key).Order(StringComparer.Ordinal));
        }
    }

    private static CheckResult TuningCheck(ProgressionCase c)
    {
        var text = string.Join(" ", c.Input.Tuning.OpenStrings);
        var expected = c.Input.Tuning.OpenStrings.Select(CorpusPitchMath.MidiOf).ToList();

        if (!PitchCollection.TryParse(text, null, out var parsed))
        {
            return new CheckResult("tuning.domain_parses_open_strings", "tuning.parse", Fail,
                $"PitchCollection.Parse rejected '{text}'", $"MIDI {string.Join(",", expected)}", null);
        }

        var actual = parsed.Select(p => p.MidiNote.Value).ToList();

        return new CheckResult(
            "tuning.domain_parses_open_strings", "tuning.parse",
            actual.SequenceEqual(expected) ? Pass : Fail,
            $"'{text}' -> MIDI {string.Join(",", actual)}",
            $"MIDI {string.Join(",", expected)}",
            BlockedOn: null);
    }

    /// <summary>
    ///     Expectations the corpus states but no seam in this assembly can answer
    ///     yet. Recorded rather than dropped, so the artifact shows the real size
    ///     of the remaining work instead of a flattering subset.
    /// </summary>
    private static IEnumerable<CheckResult> BlockedChecks()
    {
        yield return BlockedCheck("analysis.roman_numerals",
            "no seam produces Roman numerals for a progression", [623, 614]);
        yield return BlockedCheck("analysis.chord_functions",
            "no seam assigns harmonic function per chord", [623, 614]);
        yield return BlockedCheck("analysis.scale_families",
            "no seam produces per-chord scale choices in the context of a key", [623, 567]);
        yield return BlockedCheck("explanation.required_facts",
            "explanation grading needs the #623 orchestration path plus a judge", [623]);
        yield return BlockedCheck("uncertainty.behavior",
            "no seam reports alternatives, warnings or abstention for a progression", [623]);
        yield return BlockedCheck("voicing.product_generates_valid_path",
            "no seam generates a voicing path for a progression in a given tuning", [623]);

        static CheckResult BlockedCheck(string name, string why, int[] blockedOn) =>
            new(name, null, Blocked, why, "see the corpus expectation for this case", blockedOn);
    }

    // ── Artifact shape ───────────────────────────────────────────────────────

    public sealed record Matrix(
        string Artifact,
        string ArtifactVersion,
        string Issue,
        string ParentIssue,
        string CorpusId,
        string CorpusVersion,
        string SchemaVersion,
        string Note,
        IReadOnlyList<Seam> Seams,
        Totals Totals,
        IReadOnlyList<MatrixCase> Cases);

    public sealed record Seam(string Id, string Implementation, string Notes);

    public sealed record Totals(int Pass, int Fail, int Blocked);

    public sealed record MatrixCase(string Id, string Category, IReadOnlyList<CheckResult> Checks);

    /// <summary>One seam probe against one case. <c>CheckName</c> serialises as <c>check</c>.</summary>
    public sealed record CheckResult(
        [property: JsonPropertyName("check")] string CheckName,
        string? Seam,
        string Status,
        string Observed,
        string Expected,
        IReadOnlyList<int>? BlockedOn);
}
