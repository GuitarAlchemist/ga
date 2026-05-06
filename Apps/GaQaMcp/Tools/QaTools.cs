namespace GA.QaMcp.Tools;

using System.ComponentModel;
using System.Text.Json;
using GA.Business.ML.Agents;
using ModelContextProtocol.Server;

/// <summary>
/// Phase 0 skeleton MCP tools for the cross-repo QA Architect role. Each tool returns a stub
/// response (or a contract-valid <see cref="QaVerdict"/>) so consumers can wire the protocol
/// before Phase 1 lands the real producers (invariant suite, blast-radius static analyzer,
/// adversarial replay, snapshot drift).
/// Contract: docs/contracts/2026-05-02-qa-verdict.contract.md
/// </summary>
[McpServerToolType]
public static class QaTools
{
    [McpServerTool(Name = "qa_assess_blast_radius")]
    [Description("Assess the architectural blast radius of a diff. Phase 0 returns an empty radius with score 0.0.")]
    public static string AssessBlastRadius(
        [Description("Repo identifier in 'org/name' form.")] string repo,
        [Description("Base SHA before the change.")] string baseSha,
        [Description("Head SHA after the change.")] string headSha)
    {
        var radius = new QaBlastRadius
        {
            LayersTouched = [],
            OneWayDoorsCrossed = [],
            InvariantsAtRisk = [],
            ComponentsReached = [],
            EstimatedBlastScore = 0.0
        };
        return JsonSerializer.Serialize(radius, QaVerdictJson.Options);
    }

    [McpServerTool(Name = "qa_gap_analyze")]
    [Description("Analyze test coverage gaps in a diff. Phase 0 returns an empty followup list.")]
    public static string GapAnalyze(
        [Description("Unified diff text or path.")] string diff,
        [Description("Optional glob for test files to consider.")] string? testGlobs = null) =>
        JsonSerializer.Serialize(Array.Empty<QaFollowup>(), QaVerdictJson.Options);

    [McpServerTool(Name = "qa_propose_tests")]
    [Description("Propose test stubs for a component. Phase 0 returns an empty array.")]
    public static string ProposeTests(
        [Description("Component path or namespace.")] string component,
        [Description("Test kind: property | fuzz | contract | integration.")] string kind) =>
        JsonSerializer.Serialize(Array.Empty<object>(), QaVerdictJson.Options);

    [McpServerTool(Name = "qa_verify_invariants")]
    [Description("Verify project invariants (5-layer rule, OPTIC-K dim, schema-locked contracts). Phase 0 returns no breaches.")]
    public static string VerifyInvariants(
        [Description("Target identifier (PR ref, branch, commit SHA).")] string target)
    {
        var evidence = new QaEvidence
        {
            Kind = "contract_check",
            Name = "phase0.invariants.skeleton",
            Outcome = "n/a",
            DriftSummary = "Phase 0 skeleton — no invariants checked yet."
        };
        return JsonSerializer.Serialize(new[] { evidence }, QaVerdictJson.Options);
    }

    [McpServerTool(Name = "qa_replay_adversarial")]
    [Description("Replay the adversarial corpus against a target. Phase 0 returns a skipped result.")]
    public static string ReplayAdversarial(
        [Description("Target identifier (PR ref, branch, commit SHA).")] string target,
        [Description("Corpus to replay: 'ix' | 'ga' (default 'ga').")] string? corpus = null)
    {
        var evidence = new QaEvidence
        {
            Kind = "adversarial_replay",
            Name = $"phase0.adversarial.{corpus ?? "ga"}.skeleton",
            Outcome = "skipped"
        };
        return JsonSerializer.Serialize(evidence, QaVerdictJson.Options);
    }

    [McpServerTool(Name = "qa_score_quality_drift")]
    [Description("Score quality-snapshot drift over a window. For 'optick-sae' metric, computes reconstruction_mse / dead_features / partition_purity deltas across consecutive artifacts within the window.")]
    public static string ScoreQualityDrift(
        [Description("Metric name as it appears in state/quality/*.json.")] string metric,
        [Description("Window length in days.")] int windowDays)
        => ScoreQualityDriftAt(metric, windowDays, Path.Combine("state", "quality"));

    /// <summary>
    /// Testable form of <see cref="ScoreQualityDrift"/> with an injectable
    /// state-quality root. WHY: the MCP tool resolves paths relative to CWD
    /// (the MCP server's working dir at runtime); tests need to point at a
    /// temp dir without mutating process-wide CWD.
    /// </summary>
    public static string ScoreQualityDriftAt(string metric, int windowDays, string stateQualityRoot)
    {
        if (metric.Equals("optick-sae", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(
                ComputeOptickSaeDrift(Path.Combine(stateQualityRoot, "optick-sae"), windowDays),
                QaVerdictJson.Options);
        }

        var evidence = new QaEvidence
        {
            Kind = "quality_snapshot",
            Name = metric,
            DriftSummary = $"Phase 0 skeleton — no time-series read yet (window={windowDays}d)."
        };
        return JsonSerializer.Serialize(evidence, QaVerdictJson.Options);
    }

    // ── Phase 2: optick-sae drift computation ────────────────────────────────

    // Per-artifact-pair tolerances. Above any of these the evidence outcome
    // flips from "pass" to "concern". Calibrated against the 2026-05-03 →
    // 2026-05-04 supersede (synthetic → real-corpus): the synthetic baseline
    // had MSE 1e-5 with 0% dead, the real run had MSE 4e-7 with 23.7% dead —
    // a legitimate change that should surface as "concern" so a human reads it.
    private const double DriftMseRelativeTolerance = 0.50;        // +50% relative
    private const double DriftDeadFeaturesPctTolerance = 5.0;     // +5 pct points
    private const double DriftPurityMeanAbsoluteTolerance = 0.05; // -0.05 absolute

    private sealed record SaeArtifactSummary(
        string Path,
        string ArtifactId,
        DateTimeOffset TrainedAt,
        double ReconstructionMse,
        double DeadFeaturesPct,
        double PurityMean);

    private static QaEvidence ComputeOptickSaeDrift(string saeRoot, int windowDays)
    {
        if (!Directory.Exists(saeRoot))
        {
            return new QaEvidence
            {
                Kind = "quality_snapshot",
                Name = "optick-sae",
                Outcome = "n/a",
                DriftSummary = $"No SAE artifacts found under {saeRoot}."
            };
        }

        var summaries = LoadSaeSummariesInWindow(saeRoot, windowDays);

        if (summaries.Count == 0)
        {
            return new QaEvidence
            {
                Kind = "quality_snapshot",
                Name = "optick-sae",
                Outcome = "n/a",
                DriftSummary = $"No SAE artifacts within {windowDays}d window under {saeRoot}."
            };
        }

        if (summaries.Count == 1)
        {
            var only = summaries[0];
            return new QaEvidence
            {
                Kind = "quality_snapshot",
                Name = "optick-sae",
                Outcome = "n/a",
                Score = only.ReconstructionMse,
                DriftSummary =
                    $"Only one SAE artifact in window ({only.ArtifactId}, " +
                    $"mse={only.ReconstructionMse:F6}, dead={only.DeadFeaturesPct:F2}%). " +
                    "Drift requires ≥2 artifacts."
            };
        }

        // Compare newest vs oldest in window — surfaces cumulative drift over the window.
        var oldest = summaries[0];
        var newest = summaries[^1];

        var mseDelta = newest.ReconstructionMse - oldest.ReconstructionMse;
        var mseRelative = oldest.ReconstructionMse > 0
            ? mseDelta / oldest.ReconstructionMse
            : 0.0;
        var deadDelta = newest.DeadFeaturesPct - oldest.DeadFeaturesPct;
        var purityDelta = newest.PurityMean - oldest.PurityMean;

        var concerns = new List<string>();
        if (mseRelative > DriftMseRelativeTolerance)
            concerns.Add($"reconstruction_mse +{mseRelative * 100:F0}% (>+{DriftMseRelativeTolerance * 100:F0}%)");
        if (deadDelta > DriftDeadFeaturesPctTolerance)
            concerns.Add($"dead_features_pct +{deadDelta:F1}pp (>+{DriftDeadFeaturesPctTolerance:F1}pp)");
        if (-purityDelta > DriftPurityMeanAbsoluteTolerance)
            concerns.Add($"feature_partition_purity_mean {purityDelta:+0.000;-0.000} (drop >{DriftPurityMeanAbsoluteTolerance:F2})");

        var outcome = concerns.Count == 0 ? "pass" : "concern";
        var summary = concerns.Count == 0
            ? $"{summaries.Count} artifact(s) in window. " +
              $"mse {oldest.ReconstructionMse:F6} → {newest.ReconstructionMse:F6} ({mseRelative:+0.0%;-0.0%}), " +
              $"dead {oldest.DeadFeaturesPct:F2}% → {newest.DeadFeaturesPct:F2}% ({deadDelta:+0.00;-0.00}pp), " +
              $"purity {oldest.PurityMean:F3} → {newest.PurityMean:F3} ({purityDelta:+0.000;-0.000}). All within tolerance."
            : $"{summaries.Count} artifact(s) in window. Drift concerns: {string.Join("; ", concerns)}.";

        return new QaEvidence
        {
            Kind = "quality_snapshot",
            Name = "optick-sae",
            Outcome = outcome,
            Score = newest.ReconstructionMse,
            Baseline = oldest.ReconstructionMse,
            DeltaFromBaseline = mseDelta,
            GuardrailMax = 0.05,
            DriftSummary = summary
        };
    }

    private static List<SaeArtifactSummary> LoadSaeSummariesInWindow(string saeRoot, int windowDays)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Max(0, windowDays));
        var summaries = new List<SaeArtifactSummary>();

        foreach (var path in Directory.EnumerateFiles(saeRoot, "optick-sae-artifact.json", SearchOption.AllDirectories))
        {
            // Skip per-developer-machine experiment dirs (state/quality/optick-sae/<date>-local/).
            // Those are validation runs, not the shared timeline.
            var parentDir = Path.GetFileName(Path.GetDirectoryName(path));
            if (parentDir is not null && parentDir.EndsWith("-local", StringComparison.Ordinal))
                continue;

            var summary = TryParseSummary(path);
            if (summary is null) continue;
            if (summary.TrainedAt < cutoff) continue;
            summaries.Add(summary);
        }

        summaries.Sort((a, b) => a.TrainedAt.CompareTo(b.TrainedAt));
        return summaries;
    }

    private static SaeArtifactSummary? TryParseSummary(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            if (!root.TryGetProperty("artifact_id", out var idEl) ||
                !root.TryGetProperty("trained_at", out var tsEl) ||
                !root.TryGetProperty("metrics", out var metricsEl))
            {
                return null;
            }

            if (!DateTimeOffset.TryParse(tsEl.GetString(), out var trainedAt)) return null;

            return new SaeArtifactSummary(
                path,
                idEl.GetString() ?? "<unknown>",
                trainedAt,
                ReadDouble(metricsEl, "reconstruction_mse"),
                ReadDouble(metricsEl, "dead_features_pct"),
                ReadDouble(metricsEl, "feature_partition_purity_mean"));
        }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
    }

    private static double ReadDouble(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number
            ? el.GetDouble()
            : 0.0;

    [McpServerTool(Name = "qa_lookup_defect_memory")]
    [Description("Query the defect knowledge graph for past followups matching a pattern. Phase 0 returns an empty list.")]
    public static string LookupDefectMemory(
        [Description("Free-text query (component path, error pattern, or keyword).")] string query,
        [Description("Top-K results to return.")] int? k = null) =>
        JsonSerializer.Serialize(Array.Empty<QaFollowup>(), QaVerdictJson.Options);

    [McpServerTool(Name = "qa_emit_verdict")]
    [Description("Persist a QaVerdict to state/quality/verdicts/. Validates against the schema. Phase 0 emits a skeleton verdict to a temp directory if no verdict body is supplied.")]
    public static string EmitVerdict(
        [Description("Optional verdict JSON body. When omitted, a Phase 0 skeleton verdict is generated.")] string? verdictJson = null)
    {
        var verdict = string.IsNullOrWhiteSpace(verdictJson)
            ? QAArchitectAgent.BuildSkeletonVerdict(new AgentRequest { Query = "qa_emit_verdict default" })
            : QaVerdictJson.Deserialize(verdictJson)
              ?? throw new InvalidOperationException("Verdict JSON did not deserialize.");

        var json = QaVerdictJson.Serialize(verdict);
        var root = Path.Combine(Path.GetTempPath(), "ga-qa-verdicts");
        var dir = Path.Combine(root, verdict.Target.Repo.Replace('/', '_'), verdict.Target.Ref);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{verdict.VerdictId}.json");
        File.WriteAllText(path, json);

        return JsonSerializer.Serialize(new
        {
            verdict_id = verdict.VerdictId,
            persisted_path = path
        }, QaVerdictJson.Options);
    }
}
