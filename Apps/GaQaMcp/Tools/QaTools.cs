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
    [Description("Score quality-snapshot drift over a window. Phase 0 returns null delta.")]
    public static string ScoreQualityDrift(
        [Description("Metric name as it appears in state/quality/*.json.")] string metric,
        [Description("Window length in days.")] int windowDays)
    {
        var evidence = new QaEvidence
        {
            Kind = "quality_snapshot",
            Name = metric,
            DriftSummary = $"Phase 0 skeleton — no time-series read yet (window={windowDays}d)."
        };
        return JsonSerializer.Serialize(evidence, QaVerdictJson.Options);
    }

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
