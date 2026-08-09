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

    /// <summary>
    ///     Snapshot of the committed artifact taken before any test can rewrite
    ///     it, so <see cref="Matrix_WriteEvidenceArtifact" /> cannot influence the
    ///     regression gate depending on which runs first.
    /// </summary>
    private string? _committedMatrixText;

    [OneTimeSetUp]
    public void CaptureCommittedMatrix() =>
        _committedMatrixText = File.Exists(ProgressionCorpus.MatrixPath)
            ? File.ReadAllText(ProgressionCorpus.MatrixPath)
            : null;

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
        if (_committedMatrixText is null)
        {
            Assert.Fail(
                $"no committed matrix at {ProgressionCorpus.MatrixPath}. " +
                $"Generate it with {WriteEnvVar}=1 and commit the result.");
            return;
        }

        var committed = JsonSerializer.Deserialize<Matrix>(_committedMatrixText, ReadOptions)
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

    // ── Cells whose pass must mean what it says ──────────────────────────────

    /// <summary>
    ///     Negative control for the empty-seam-result path. "No forbidden centre
    ///     appeared in the top tie" is vacuously true when the seam produced no
    ///     candidate at all, so recording a pass there reports success for an
    ///     input the product could not answer. It is worse than an odd cell on
    ///     <c>pc-02</c>, whose sibling <c>key.top_candidate_acceptable</c> is
    ///     already failing: a total parse regression would move neither cell and
    ///     emit no gate signal at all.
    /// </summary>
    /// <remarks>
    ///     Replayed rather than provoked, because no corpus case makes the seam
    ///     return nothing today - which is why the false pass was invisible.
    /// </remarks>
    [Test]
    public void ForbiddenCenterCell_FailsClosedOnAnEmptySeamResult()
    {
        Assert.That(KeyIdentificationService.Identify(["N.C."]), Is.Empty,
            "the empty-result path must be reachable, or this control means nothing");

        Assert.Multiple(() =>
        {
            foreach (var c in ProgressionCorpus.Load().Cases)
            {
                var cell = ForbiddenCenterCell(c, []);

                Assert.That(cell.Status, Is.EqualTo(Fail),
                    $"{c.Id}: an empty candidate set must not pass key.forbidden_center_excluded");
                Assert.That(cell.Observed, Is.EqualTo("(no candidate)"),
                    $"{c.Id}: the cell must say the seam answered nothing");
            }
        });
    }

    /// <summary>
    ///     The discriminating half of the control above: failing closed must not
    ///     become failing always. A non-empty result that avoids every forbidden
    ///     centre still passes.
    /// </summary>
    [Test]
    public void ForbiddenCenterCell_StillPassesOnACleanNonEmptyResult() =>
        Assert.Multiple(() =>
        {
            foreach (var c in ProgressionCorpus.Load().Cases)
            {
                var acceptable = c.Expected.AcceptableTonalCenters[0];
                var cell = ForbiddenCenterCell(c, [Candidate(acceptable)]);

                Assert.That(cell.Status, Is.EqualTo(Pass),
                    $"{c.Id}: '{acceptable}' is not forbidden, so the cell must still pass");
            }
        });

    /// <summary>
    ///     #623 and #614 replace the relative-key scoring tie with a functional
    ///     reading. That collapse is the intended improvement, and it must not be
    ///     scorable as a <c>pass -&gt; fail</c> product regression on the corpus's
    ///     one ambiguity cell. Replays every tie width, including the collapsed
    ///     and empty ones no seam can produce today.
    /// </summary>
    [Test]
    public void AmbiguityCell_IsBlockedForEveryScoringTieWidth()
    {
        var ambiguous = ProgressionCorpus.Load().Cases
            .Where(c => c.Uncertainty.ExpectedBehavior != "confident").ToList();

        Assert.That(ambiguous, Is.Not.Empty, "#627 requires an intentionally ambiguous case");

        IReadOnlyList<KeyIdentificationService.KeyCandidate>[] replays =
        [
            [],                                                             // seam parsed nothing
            [Candidate("C major")],                                         // tie collapsed by the #623 fix
            [Candidate("A minor"), Candidate("C major")],                   // today's accidental tie
            [Candidate("A minor"), Candidate("C major"), Candidate("F major")]
        ];

        Assert.Multiple(() =>
        {
            foreach (var c in ambiguous)
            foreach (var candidates in replays)
            {
                var cell = KeyChecks(c, candidates)
                    .Single(x => x.CheckName == "key.alternatives_when_ambiguous");

                Assert.That(cell.Status, Is.EqualTo(Blocked),
                    $"{c.Id} with {candidates.Count} candidate(s): a bare scoring tie is not an " +
                    "explicit alternatives or abstention signal, so it cannot be scored either way");
                Assert.That(cell.BlockedOn, Is.EqualTo(new[] { 623 }),
                    $"{c.Id}: the ambiguity seam is the one #623 has to supply");
            }
        });
    }

    /// <summary>
    ///     The artifact must not contradict itself: a case cannot record
    ///     "no seam reports alternatives, warnings or abstention" and a passing
    ///     alternatives cell for the same seam in the same run.
    /// </summary>
    [Test]
    public void AmbiguityCell_AgreesWithTheSiblingUncertaintyCell()
    {
        var matrix = BuildMatrix();

        Assert.Multiple(() =>
        {
            foreach (var c in matrix.Cases)
            {
                var alternatives = c.Checks.FirstOrDefault(x => x.CheckName == "key.alternatives_when_ambiguous");
                if (alternatives is null) continue;

                var uncertainty = c.Checks.Single(x => x.CheckName == "uncertainty.behavior");

                Assert.That(alternatives.Status, Is.EqualTo(uncertainty.Status),
                    $"{c.Id}: key.alternatives_when_ambiguous and uncertainty.behavior describe the " +
                    "same missing capability and must report the same status");
                Assert.That(alternatives.BlockedOn, Is.EqualTo(uncertainty.BlockedOn),
                    $"{c.Id}: both cells wait on the same issue");
            }
        });
    }

    private static CheckResult ForbiddenCenterCell(
        ProgressionCase c, IReadOnlyList<KeyIdentificationService.KeyCandidate> candidates) =>
        KeyChecks(c, candidates).Single(x => x.CheckName == "key.forbidden_center_excluded");

    /// <summary>A seam result standing in for one scored key, for replay only.</summary>
    private static KeyIdentificationService.KeyCandidate Candidate(string key) =>
        new(key, RelativeKey: string.Empty, MatchCount: 1, TotalChords: 1, DiatonicSet: []);

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

    private static IEnumerable<CheckResult> KeyChecks(ProgressionCase c) =>
        KeyChecks(c, KeyIdentificationService.Identify(c.Input.CanonicalChords));

    /// <summary>
    ///     Scores the key cells from a seam result. The seam call is split out so
    ///     a test can replay a synthetic result - in particular the empty result
    ///     and the collapsed single reading - without waiting for the product to
    ///     produce one. Neither state is reachable from any corpus case today,
    ///     which is precisely why they went unscored.
    /// </summary>
    private static IEnumerable<CheckResult> KeyChecks(
        ProgressionCase c, IReadOnlyList<KeyIdentificationService.KeyCandidate> candidates)
    {
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

        // Fails closed on an empty candidate set: "no forbidden centre appeared"
        // is vacuously true when the seam answered nothing, and a pass there
        // would report success for a total parse failure. See
        // ForbiddenCenterCell_FailsClosedOnAnEmptySeamResult.
        yield return new CheckResult(
            "key.forbidden_center_excluded", "key.identify",
            topTied.Count > 0 && forbiddenHits.Count == 0 ? Pass : Fail,
            topTied.Count == 0 ? observed
                : forbiddenHits.Count == 0 ? $"top-tied: {observed}"
                : $"forbidden in top-tied: {string.Join(" | ", forbiddenHits)}",
            $"at least one candidate, and none of: {string.Join(" | ", c.Forbidden.TonalCenters)}",
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
            // #627 asks whether an ambiguous progression is answered with
            // explicit alternatives or an abstention. No seam in this assembly
            // reports either - which is what the sibling uncertainty.behavior
            // cell already records as blocked on #623. The only thing observable
            // here is a bare relative-key scoring tie, the same artefact that
            // fails eleven key.single_reading_when_confident cells; scoring it as
            // a pass would contradict that sibling cell and would turn the #623
            // fix, which collapses the tie to one reading, into a build-failing
            // pass -> fail regression. The tie width is deliberately kept out of
            // this cell. See AmbiguityCell_IsBlockedForEveryScoringTieWidth.
            yield return new CheckResult(
                "key.alternatives_when_ambiguous", null, Blocked,
                "no seam reports alternatives, warnings or abstention for a progression; " +
                "a bare relative-key scoring tie is not an ambiguity signal",
                $"at least {c.Uncertainty.MinAlternatives} explicit alternatives, including all of: " +
                string.Join(" | ", c.Expected.AcceptableTonalCenters),
                BlockedOn: [623]);
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
