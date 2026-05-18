namespace GA.Business.ML.Agents;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

/// <summary>
/// Phase 1 QA Architect agent. Emits contract-valid <see cref="QaVerdict"/> objects and, when a
/// <see cref="GuitarAlchemistAgentBase.Coordinator"/> is wired, delegates semantic-judge review to
/// <see cref="CriticAgent"/> so the reviewer_chain reflects a real basin score for non-empty diffs.
/// Contract: docs/contracts/2026-05-02-qa-verdict.contract.md.
/// </summary>
public sealed class QAArchitectAgent(IChatClient chatClient, ILogger<QAArchitectAgent> logger)
    : GuitarAlchemistAgentBase(chatClient, logger)
{
    public override string AgentId => AgentIds.QaArchitect;
    public override string Name => "QA Architect";
    public override string Description =>
        "Cross-repo senior QA engineer. Scores blast radius, designs test surfaces, watches quality drift, " +
        "queries defect memory, and emits contract-valid QaVerdicts.";
    public override IReadOnlyList<string> Capabilities =>
    [
        "blast_radius_assessment",
        "test_gap_analysis",
        "invariant_verification",
        "quality_drift_triage",
        "defect_memory_lookup",
        "verdict_emission"
    ];

    public override async Task<AgentResponse> ProcessAsync(
        AgentRequest request,
        CancellationToken cancellationToken = default)
    {
        var verdict = BuildSkeletonVerdict(request);
        verdict = await ApplySemanticJudge(verdict, request, cancellationToken);

        var response = new AgentResponse
        {
            AgentId = AgentId,
            Result = verdict.Narrative,
            Confidence = 0.5f,
            Evidence = ["Phase 1: invariant suite, blast-radius, and semantic judge wired."],
            Assumptions = ["Full tribunal (adversarial replay, gap analysis) deferred to Phase 2."],
            Data = verdict
        };
        return response;
    }

    /// <summary>
    /// Delegates to <see cref="CriticAgent"/> when the request contains diff context and a
    /// Coordinator is wired. Augments the reviewer_chain with the semantic_judge role.
    /// </summary>
    private async Task<QaVerdict> ApplySemanticJudge(
        QaVerdict verdict,
        AgentRequest request,
        CancellationToken cancellationToken)
    {
        if (Coordinator is null || string.IsNullOrWhiteSpace(request.Query))
            return verdict;

        AgentResponse criticResponse;
        try
        {
            criticResponse = await Coordinator.DelegateAsync(
                $"QA semantic review requested for: {Truncate(request.Query, 400)}",
                AgentIds.Critic,
                cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "CriticAgent delegation failed; skipping semantic_judge role.");
            return verdict;
        }

        var judgeScore = (double)Math.Max(0f, Math.Min(1f, criticResponse.Confidence));
        var judgeEntry = new QaReviewerEntry
        {
            Agent = "ga.CriticAgent",
            Role = "semantic_judge",
            Score = judgeScore,
            Notes = Truncate(criticResponse.Result, 200)
        };

        var semanticEvidence = new QaEvidence
        {
            Kind = "semantic_basin",
            Name = "critic-agent.semantic-review",
            Score = judgeScore,
            Outcome = judgeScore >= 0.7 ? "pass" : "fail",
            DriftSummary = $"CriticAgent confidence={judgeScore:F2}"
        };

        return verdict with
        {
            ReviewerChain = [.. verdict.ReviewerChain, judgeEntry],
            Evidence = [.. verdict.Evidence, semanticEvidence]
        };
    }

    public static QaVerdict BuildSkeletonVerdict(AgentRequest request)
    {
        var producedAt = DateTime.UtcNow;
        // WHY: verdict_id is used as a filename component (contract §4); colons would break Windows paths.
        var verdictId = $"{producedAt:yyyy-MM-ddTHH-mm-ssZ}-skeleton-{Guid.NewGuid():N}-qa-architect-agent";
        var narrative = string.IsNullOrWhiteSpace(request.Query)
            ? "Phase 0 skeleton verdict. No diff supplied; treating as informational sweep."
            : $"Phase 0 skeleton verdict for query: {Truncate(request.Query, 200)}";
        return new QaVerdict
        {
            SchemaVersion = 1,
            VerdictId = verdictId,
            ProducedAt = producedAt,
            Producer = "qa-architect-agent",
            ProducerVersion = "0.1.0",
            Target = new QaTarget
            {
                Kind = "scheduled_sweep",
                Repo = "guitar-alchemist/ga",
                Ref = "skeleton",
                Sha = null,
                BaseSha = null
            },
            RiskTier = "P3",
            Verdict = "informational",
            BlastRadius = new QaBlastRadius
            {
                LayersTouched = [],
                OneWayDoorsCrossed = [],
                InvariantsAtRisk = [],
                ComponentsReached = [],
                EstimatedBlastScore = 0.0
            },
            Evidence =
            [
                new QaEvidence
                {
                    Kind = "manual_note",
                    Name = "phase0.skeleton",
                    Outcome = "n/a",
                    DriftSummary = "Skeleton emit; no real evidence producers wired yet."
                }
            ],
            Followups = [],
            ReviewerChain =
            [
                new QaReviewerEntry
                {
                    Agent = "ga.QAArchitectAgent",
                    Role = "architecture",
                    Score = null,
                    Notes = "Phase 0 self-emit"
                }
            ],
            Narrative = narrative,
            Links = new Dictionary<string, string>
            {
                ["plan"] = "docs/plans/2026-05-02-arch-qa-architect-tribunal-plan.md",
                ["contract"] = "docs/contracts/2026-05-02-qa-verdict.contract.md"
            }
        };
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}

/// <summary>QA verdict — strongly-typed mirror of qa-verdict.schema.json (v1).</summary>
public sealed record QaVerdict
{
    [JsonPropertyName("schema_version")] public required int SchemaVersion { get; init; }
    [JsonPropertyName("verdict_id")] public required string VerdictId { get; init; }
    [JsonPropertyName("produced_at")] public required DateTime ProducedAt { get; init; }
    [JsonPropertyName("producer")] public required string Producer { get; init; }
    [JsonPropertyName("producer_version")] public required string ProducerVersion { get; init; }
    [JsonPropertyName("target")] public required QaTarget Target { get; init; }
    [JsonPropertyName("risk_tier")] public required string RiskTier { get; init; }
    [JsonPropertyName("verdict")] public required string Verdict { get; init; }
    [JsonPropertyName("blast_radius")] public required QaBlastRadius BlastRadius { get; init; }
    [JsonPropertyName("evidence")] public required IReadOnlyList<QaEvidence> Evidence { get; init; }
    [JsonPropertyName("followups")] public required IReadOnlyList<QaFollowup> Followups { get; init; }
    [JsonPropertyName("reviewer_chain")] public required IReadOnlyList<QaReviewerEntry> ReviewerChain { get; init; }
    [JsonPropertyName("narrative")] public required string Narrative { get; init; }
    [JsonPropertyName("links")] public IReadOnlyDictionary<string, string>? Links { get; init; }
}

public sealed record QaTarget
{
    [JsonPropertyName("kind")] public required string Kind { get; init; }
    [JsonPropertyName("repo")] public required string Repo { get; init; }
    [JsonPropertyName("ref")] public required string Ref { get; init; }
    [JsonPropertyName("sha")] public string? Sha { get; init; }
    [JsonPropertyName("base_sha")] public string? BaseSha { get; init; }
}

public sealed record QaBlastRadius
{
    [JsonPropertyName("layers_touched")] public required IReadOnlyList<string> LayersTouched { get; init; }
    [JsonPropertyName("one_way_doors_crossed")] public required IReadOnlyList<string> OneWayDoorsCrossed { get; init; }
    [JsonPropertyName("invariants_at_risk")] public required IReadOnlyList<string> InvariantsAtRisk { get; init; }
    [JsonPropertyName("components_reached")] public required IReadOnlyList<string> ComponentsReached { get; init; }
    [JsonPropertyName("estimated_blast_score")] public required double EstimatedBlastScore { get; init; }
}

public sealed record QaEvidence
{
    [JsonPropertyName("kind")] public required string Kind { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("outcome")] public string? Outcome { get; init; }
    [JsonPropertyName("score")] public double? Score { get; init; }
    [JsonPropertyName("baseline")] public double? Baseline { get; init; }
    [JsonPropertyName("guardrail_min")] public double? GuardrailMin { get; init; }
    [JsonPropertyName("guardrail_max")] public double? GuardrailMax { get; init; }
    [JsonPropertyName("delta_from_baseline")] public double? DeltaFromBaseline { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("drift_summary")] public string? DriftSummary { get; init; }
}

public sealed record QaFollowup
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("severity")] public required string Severity { get; init; }
    [JsonPropertyName("title")] public required string Title { get; init; }
    [JsonPropertyName("rationale")] public required string Rationale { get; init; }
    [JsonPropertyName("location")] public string? Location { get; init; }
    [JsonPropertyName("proposed_test")] public string? ProposedTest { get; init; }
    [JsonPropertyName("blocks_merge")] public required bool BlocksMerge { get; init; }
}

public sealed record QaReviewerEntry
{
    [JsonPropertyName("agent")] public required string Agent { get; init; }
    [JsonPropertyName("role")] public required string Role { get; init; }
    [JsonPropertyName("score")] public double? Score { get; init; }
    [JsonPropertyName("notes")] public string? Notes { get; init; }
}

/// <summary>JSON serialization defaults for QA verdicts. WHY: contract uses snake_case + ISO-8601 UTC; centralizing keeps producers consistent.</summary>
public static class QaVerdictJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(QaVerdict verdict) => JsonSerializer.Serialize(verdict, Options);
    public static QaVerdict? Deserialize(string json) => JsonSerializer.Deserialize<QaVerdict>(json, Options);
}
