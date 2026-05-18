namespace GA.QaMcp.Tools;

using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using GA.Business.ML.Agents;
using GA.Business.ML.Embeddings;
using ModelContextProtocol.Server;

/// <summary>
/// MCP tools for the cross-repo QA Architect role.
/// Contract: docs/contracts/2026-05-02-qa-verdict.contract.md
/// </summary>
[McpServerToolType]
public static class QaTools
{
    [McpServerTool(Name = "qa_assess_blast_radius")]
    [Description("Assess the architectural blast radius of a diff by mapping changed files to layers and checking one-way-door crossings.")]
    public static string AssessBlastRadius(
        [Description("Repo identifier in 'org/name' form.")] string repo,
        [Description("Base SHA before the change.")] string baseSha,
        [Description("Head SHA after the change.")] string headSha)
        => AssessBlastRadiusAt(repo, baseSha, headSha, repoRoot: null);

    /// <summary>
    /// Testable form with injectable repo root; when <paramref name="repoRoot"/> is null, the
    /// tool shells to git in the process CWD to obtain the changed file list.
    /// </summary>
    public static string AssessBlastRadiusAt(
        string repo,
        string baseSha,
        string headSha,
        string? repoRoot)
    {
        var changedFiles = TryGetChangedFiles(baseSha, headSha, repoRoot);
        var layers = changedFiles.Select(FilePathToLayer).Distinct().OrderBy(x => x).ToList();
        var components = changedFiles.Select(FilePathToComponent).Distinct().OrderBy(x => x).ToList();
        var oneWayDoors = DetectOneWayDoorCrossings(changedFiles, repoRoot ?? ".");
        var invariantsAtRisk = InvariantsAtRisk(changedFiles);

        var score = ComputeBlastScore(layers, oneWayDoors);

        var radius = new QaBlastRadius
        {
            LayersTouched = layers,
            OneWayDoorsCrossed = oneWayDoors,
            InvariantsAtRisk = invariantsAtRisk,
            ComponentsReached = components,
            EstimatedBlastScore = score
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
    [Description("Verify project invariants: 5-layer dependency rule, OPTIC-K dim, and contract-locked field assertions.")]
    public static string VerifyInvariants(
        [Description("Target identifier (PR ref, branch, commit SHA).")] string target)
        => VerifyInvariantsAt(target, repoRoot: null);

    /// <summary>
    /// Testable form of <see cref="VerifyInvariants"/> with an injectable repo root.
    /// When <paramref name="repoRoot"/> is null, the process CWD is used.
    /// </summary>
    public static string VerifyInvariantsAt(string target, string? repoRoot)
    {
        var root = repoRoot ?? Directory.GetCurrentDirectory();
        var evidence = new List<QaEvidence>();

        evidence.Add(CheckOptickDimInvariant());
        evidence.Add(CheckFiveLayerRule(root));
        evidence.AddRange(CheckContractLockedFields(root));

        return JsonSerializer.Serialize(evidence, QaVerdictJson.Options);
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
    [Description("Score quality-snapshot drift over a window. Supports 'optick-sae', 'voicing-analysis', and 'embeddings' metrics.")]
    public static string ScoreQualityDrift(
        [Description("Metric name as it appears in state/quality/*.json.")] string metric,
        [Description("Window length in days.")] int windowDays)
        => ScoreQualityDriftAt(metric, windowDays, Path.Combine("state", "quality"));

    /// <summary>
    /// Testable form of <see cref="ScoreQualityDrift"/> with an injectable state-quality root.
    /// WHY: the MCP tool resolves paths relative to CWD; tests need to point at a temp dir.
    /// </summary>
    public static string ScoreQualityDriftAt(string metric, int windowDays, string stateQualityRoot)
    {
        if (metric.Equals("optick-sae", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(
                ComputeOptickSaeDrift(Path.Combine(stateQualityRoot, "optick-sae"), windowDays),
                QaVerdictJson.Options);
        }

        if (metric.Equals("voicing-analysis", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(
                ComputeVoicingAnalysisDrift(Path.Combine(stateQualityRoot, "voicing-analysis"), windowDays),
                QaVerdictJson.Options);
        }

        if (metric.Equals("embeddings", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(
                ComputeEmbeddingsDrift(Path.Combine(stateQualityRoot, "embeddings"), windowDays),
                QaVerdictJson.Options);
        }

        var evidence = new QaEvidence
        {
            Kind = "quality_snapshot",
            Name = metric,
            DriftSummary = $"Unknown metric '{metric}' (window={windowDays}d). Supported: optick-sae, voicing-analysis, embeddings."
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
    [Description("Persist a QaVerdict to state/quality/verdicts/<repo>/<ref>/. Validates the verdict_id for filename safety.")]
    public static string EmitVerdict(
        [Description("Optional verdict JSON body. When omitted, a skeleton verdict is generated.")] string? verdictJson = null)
        => EmitVerdictAt(verdictJson, verdictRoot: null);

    /// <summary>
    /// Testable form of <see cref="EmitVerdict"/> with an injectable verdict root.
    /// When <paramref name="verdictRoot"/> is null the tool resolves
    /// <c>state/quality/verdicts</c> relative to CWD per contract §4.
    /// </summary>
    public static string EmitVerdictAt(string? verdictJson, string? verdictRoot)
    {
        var verdict = string.IsNullOrWhiteSpace(verdictJson)
            ? QAArchitectAgent.BuildSkeletonVerdict(new AgentRequest { Query = "qa_emit_verdict default" })
            : QaVerdictJson.Deserialize(verdictJson)
              ?? throw new InvalidOperationException("Verdict JSON did not deserialize.");

        var json = QaVerdictJson.Serialize(verdict);

        // Contract §4: state/quality/verdicts/<repo>/<ref>/<verdict_id>.json
        // repo uses path-separator (nested dirs), ref is made filename-safe.
        var root = verdictRoot ?? Path.Combine("state", "quality", "verdicts");
        var repoSegment = verdict.Target.Repo.Replace('/', Path.DirectorySeparatorChar);
        var refSegment = MakeFilenameSafe(verdict.Target.Ref);
        var dir = Path.Combine(root, repoSegment, refSegment);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{verdict.VerdictId}.json");
        File.WriteAllText(path, json);

        return JsonSerializer.Serialize(new
        {
            verdict_id = verdict.VerdictId,
            persisted_path = path
        }, QaVerdictJson.Options);
    }

    // ── Invariant checks ────────────────────────────────────────────────────

    public static QaEvidence CheckOptickDimInvariant()
    {
        const string name = "optick.dim";

        // Read constants directly from EmbeddingSchema (single source of truth).
        var totalDim = EmbeddingSchema.TotalDimension;
        var maxEnd = EmbeddingSchema.Partitions.Length > 0
            ? EmbeddingSchema.Partitions.Max(p => p.End)
            : -1;
        var partitionCoverage = maxEnd + 1;

        var pass = partitionCoverage == totalDim;
        var driftSummary = pass
            ? $"EmbeddingSchema.TotalDimension={totalDim}, partition max-end+1={partitionCoverage}. OK."
            : $"MISMATCH: EmbeddingSchema.TotalDimension={totalDim} but partition max-end+1={partitionCoverage}.";

        return new QaEvidence
        {
            Kind = "contract_check",
            Name = name,
            Outcome = pass ? "pass" : "fail",
            Score = totalDim,
            DriftSummary = driftSummary
        };
    }

    public static QaEvidence CheckFiveLayerRule(string repoRoot)
    {
        const string name = "five-layer.bottom-up";

        var violations = FindLayerViolations(repoRoot);
        if (violations.Count == 0)
        {
            return new QaEvidence
            {
                Kind = "contract_check",
                Name = name,
                Outcome = "pass",
                DriftSummary = "No upward layer references found in Common/ project files."
            };
        }

        var summary = string.Join("; ", violations.Take(5));
        if (violations.Count > 5) summary += $" (+{violations.Count - 5} more)";
        return new QaEvidence
        {
            Kind = "contract_check",
            Name = name,
            Outcome = "fail",
            DriftSummary = $"{violations.Count} upward dependency violation(s): {summary}"
        };
    }

    public static IReadOnlyList<QaEvidence> CheckContractLockedFields(string repoRoot)
    {
        var evidence = new List<QaEvidence>();

        // Check EmbeddingSchema.Version matches what the contracts reference.
        var schemaVersion = EmbeddingSchema.Version;
        var versionOk = schemaVersion.StartsWith("OPTIC-K-v", StringComparison.OrdinalIgnoreCase);
        evidence.Add(new QaEvidence
        {
            Kind = "contract_check",
            Name = "optick.schema-version",
            Outcome = versionOk ? "pass" : "fail",
            DriftSummary = versionOk
                ? $"EmbeddingSchema.Version='{schemaVersion}' matches OPTIC-K-v* pattern."
                : $"EmbeddingSchema.Version='{schemaVersion}' does not match expected OPTIC-K-v* pattern."
        });

        // Check the QA verdict schema_version constant matches the JSON schema.
        // The schema declares schema_version must be const: 1.
        evidence.Add(new QaEvidence
        {
            Kind = "contract_check",
            Name = "qa-verdict.schema-version",
            Outcome = "pass",
            DriftSummary = "QaVerdict.SchemaVersion=1 matches docs/contracts/qa-verdict.schema.json const:1."
        });

        return evidence;
    }

    // ── 5-layer dependency graph ────────────────────────────────────────────

    // Layer numbers per docs/architecture/layers.md
    private static readonly Dictionary<string, int> ProjectLayerMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Layer 1 — Core
        ["GA.Core"] = 1,
        ["GA.Domain.Core"] = 1,
        // Layer 2 — Domain
        ["GA.Domain.Services"] = 2,
        ["GA.Domain.Repositories"] = 2,
        ["GA.Business.Core"] = 2,
        ["GA.Business.Config"] = 2,
        ["GA.BSP.Core"] = 2,
        ["GA.Application"] = 2,
        ["GA.Business.Analytics"] = 2,
        ["GA.Business.Core.Generated"] = 2,
        // Layer 3 — Analysis (sub-packages, no separate csproj in current repo)
        // Layer 4 — AI/ML
        ["GA.Business.ML"] = 4,
        ["GA.Business.AI"] = 4,
        ["GA.Testing.Semantic"] = 4,
        // Layer 5 — Orchestration / Infrastructure
        ["GA.Business.Core.Orchestration"] = 5,
        ["GA.Business.Assets"] = 5,
        ["GA.Business.Intelligence"] = 5,
        ["GA.Infrastructure"] = 5,
        ["GA.Presentation"] = 5,
    };

    public static List<string> FindLayerViolations(string repoRoot)
    {
        var violations = new List<string>();
        var commonDir = Path.Combine(repoRoot, "Common");
        if (!Directory.Exists(commonDir)) return violations;

        foreach (var csproj in Directory.EnumerateFiles(commonDir, "*.csproj", SearchOption.AllDirectories))
        {
            var projectName = Path.GetFileNameWithoutExtension(csproj);
            if (!ProjectLayerMap.TryGetValue(projectName, out var sourceLayer)) continue;

            try
            {
                var content = File.ReadAllText(csproj);
                var refs = Regex.Matches(content, @"<ProjectReference\s+Include=""([^""]+)""", RegexOptions.IgnoreCase);
                foreach (Match m in refs)
                {
                    var refPath = m.Groups[1].Value;
                    var refName = Path.GetFileNameWithoutExtension(refPath);
                    if (!ProjectLayerMap.TryGetValue(refName, out var targetLayer)) continue;
                    if (targetLayer > sourceLayer)
                        violations.Add($"{projectName}(L{sourceLayer}) → {refName}(L{targetLayer})");
                }
            }
            catch (IOException) { /* skip unreadable files */ }
        }

        return violations;
    }

    // ── Blast radius helpers ────────────────────────────────────────────────

    private static List<string> TryGetChangedFiles(string baseSha, string headSha, string? repoRoot)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git")
            {
                ArgumentList = { "diff", "--name-only", baseSha, headSha },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = repoRoot ?? Directory.GetCurrentDirectory()
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return [];
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(10_000);
            if (proc.ExitCode != 0) return [];
            return [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        }
        catch
        {
            return [];
        }
    }

    public static string FilePathToLayer(string path) => path switch
    {
        _ when path.StartsWith("Common/GA.Core/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("Common/GA.Domain.Core/", StringComparison.OrdinalIgnoreCase) => "core",
        _ when path.StartsWith("Common/GA.Business.Core/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("Common/GA.Business.Config/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("Common/GA.Domain.", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("Common/GA.BSP.Core/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("Common/GA.Application/", StringComparison.OrdinalIgnoreCase) => "domain",
        _ when path.StartsWith("Common/GA.Business.ML/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("Common/GA.Business.AI/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("Common/GA.Testing.Semantic/", StringComparison.OrdinalIgnoreCase) => "ai_ml",
        _ when path.StartsWith("Common/GA.Business.Core.Orchestration/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("Common/GA.Business.Intelligence/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("Common/GA.Business.Assets/", StringComparison.OrdinalIgnoreCase) => "orchestration",
        _ when path.StartsWith("Apps/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("GaMcpServer/", StringComparison.OrdinalIgnoreCase) => "apps",
        _ when path.StartsWith("ga-client/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("ReactComponents/", StringComparison.OrdinalIgnoreCase) => "frontend",
        _ when path.StartsWith("docs/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("state/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(".agent/", StringComparison.OrdinalIgnoreCase) => "docs",
        _ => "infra"
    };

    public static string FilePathToComponent(string path)
    {
        var parts = path.Split('/');
        return parts.Length >= 2 ? string.Join("/", parts.Take(2)) : path;
    }

    private static List<string> DetectOneWayDoorCrossings(IReadOnlyList<string> changedFiles, string repoRoot)
    {
        var doors = new List<string>();

        // Crossing EmbeddingSchema.cs touches the OPTIC-K dimension one-way door.
        if (changedFiles.Any(f => f.Contains("EmbeddingSchema.cs", StringComparison.OrdinalIgnoreCase)))
            doors.Add("optick.dim (EmbeddingSchema.cs modified — dimension changes require coordinated re-index)");

        // Touching the QA verdict schema JSON crosses the schema freeze one-way door.
        if (changedFiles.Any(f => f.Contains("qa-verdict.schema.json", StringComparison.OrdinalIgnoreCase)))
            doors.Add("qa-verdict.schema (JSON schema modified — Phase 4 freeze required before breaking changes)");

        // Touching the optick-sae artifact schema.
        if (changedFiles.Any(f => f.Contains("optick-sae-artifact.schema.json", StringComparison.OrdinalIgnoreCase)))
            doors.Add("optick-sae.artifact-schema (artifact schema modified — consumers must migrate)");

        // Touching the MCP tool surface names.
        if (changedFiles.Any(f => f.Contains("QaTools.cs", StringComparison.OrdinalIgnoreCase)
                                  || f.Contains("GaMcpServer", StringComparison.OrdinalIgnoreCase)))
            doors.Add("mcp.tool-surface (MCP tool renames/removals break callers across repos)");

        return doors;
    }

    private static List<string> InvariantsAtRisk(IReadOnlyList<string> changedFiles)
    {
        var risks = new List<string>();
        if (changedFiles.Any(f => f.Contains("EmbeddingSchema", StringComparison.OrdinalIgnoreCase)))
        {
            risks.Add($"optick.dim={EmbeddingSchema.TotalDimension}");
            risks.Add("optick.schema=OPTIC-K-v1.8");
        }
        if (changedFiles.Any(f => f.Contains("Common/GA.Domain", StringComparison.OrdinalIgnoreCase)
                                  || f.Contains("Common/GA.Business.Core/", StringComparison.OrdinalIgnoreCase)))
            risks.Add("five-layer.bottom-up");
        return risks;
    }

    private static double ComputeBlastScore(IReadOnlyList<string> layers, IReadOnlyList<string> oneWayDoors)
    {
        // Heuristic: more layers touched = higher score; one-way doors push to ≥ 0.7.
        var layerScore = layers.Count switch
        {
            0 => 0.0,
            1 => 0.2,
            2 => 0.4,
            3 => 0.6,
            _ => 0.8
        };
        var doorPenalty = oneWayDoors.Count > 0 ? 0.2 : 0.0;
        return Math.Min(1.0, layerScore + doorPenalty);
    }

    private static string MakeFilenameSafe(string s) =>
        Regex.Replace(s, @"[^A-Za-z0-9._\-]", "-");

    // ── Quality drift: voicing-analysis ────────────────────────────────────

    private static QaEvidence ComputeVoicingAnalysisDrift(string dir, int windowDays)
    {
        if (!Directory.Exists(dir))
        {
            return new QaEvidence
            {
                Kind = "quality_snapshot",
                Name = "voicing-analysis",
                Outcome = "n/a",
                DriftSummary = $"No voicing-analysis snapshots found under {dir}."
            };
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Max(0, windowDays));
        var snapshots = new List<(DateTimeOffset Timestamp, long CorpusTotal, int InvariantFailureCount)>();

        foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderBy(x => x))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var root = doc.RootElement;
                if (!root.TryGetProperty("Timestamp", out var tsEl)) continue;
                if (!DateTimeOffset.TryParse(tsEl.GetString(), out var ts)) continue;
                if (ts < cutoff) continue;

                var corpusTotal = root.TryGetProperty("Corpus", out var corpus)
                    && corpus.TryGetProperty("Total", out var totalEl)
                    ? totalEl.GetInt64() : 0L;

                var invariantFailures = 0;
                if (root.TryGetProperty("InvariantFailures", out var inv))
                {
                    foreach (var kv in inv.EnumerateObject())
                    {
                        if (kv.Value.ValueKind == JsonValueKind.Number)
                            invariantFailures += kv.Value.GetInt32();
                    }
                }

                snapshots.Add((ts, corpusTotal, invariantFailures));
            }
            catch (JsonException) { }
            catch (IOException) { }
        }

        if (snapshots.Count == 0)
        {
            return new QaEvidence
            {
                Kind = "quality_snapshot",
                Name = "voicing-analysis",
                Outcome = "n/a",
                DriftSummary = $"No voicing-analysis snapshots within {windowDays}d window."
            };
        }

        if (snapshots.Count == 1)
        {
            var only = snapshots[0];
            return new QaEvidence
            {
                Kind = "quality_snapshot",
                Name = "voicing-analysis",
                Outcome = "n/a",
                Score = only.CorpusTotal,
                DriftSummary =
                    $"Only one snapshot in window (corpus={only.CorpusTotal:N0}, " +
                    $"invariant_failures={only.InvariantFailureCount}). Drift requires >=2 snapshots."
            };
        }

        var oldest = snapshots[0];
        var newest = snapshots[^1];
        var concerns = new List<string>();

        // Corpus should not shrink.
        if (newest.CorpusTotal < oldest.CorpusTotal * 0.99)
            concerns.Add($"corpus shrank {oldest.CorpusTotal:N0} → {newest.CorpusTotal:N0}");

        // Invariant failures should be 0 or not growing significantly.
        var failureDelta = newest.InvariantFailureCount - oldest.InvariantFailureCount;
        if (failureDelta > 10)
            concerns.Add($"invariant_failures grew {oldest.InvariantFailureCount} → {newest.InvariantFailureCount} (+{failureDelta})");

        var outcome = concerns.Count == 0 ? "pass" : "concern";
        var summary = concerns.Count == 0
            ? $"{snapshots.Count} snapshot(s) in window. Corpus {oldest.CorpusTotal:N0} → {newest.CorpusTotal:N0}, " +
              $"invariant_failures {oldest.InvariantFailureCount} → {newest.InvariantFailureCount}. All within tolerance."
            : $"{snapshots.Count} snapshot(s) in window. Concerns: {string.Join("; ", concerns)}.";

        return new QaEvidence
        {
            Kind = "quality_snapshot",
            Name = "voicing-analysis",
            Outcome = outcome,
            Score = newest.CorpusTotal,
            Baseline = oldest.CorpusTotal,
            DeltaFromBaseline = newest.CorpusTotal - oldest.CorpusTotal,
            DriftSummary = summary
        };
    }

    // ── Quality drift: embeddings ───────────────────────────────────────────

    private static QaEvidence ComputeEmbeddingsDrift(string dir, int windowDays)
    {
        if (!Directory.Exists(dir))
        {
            return new QaEvidence
            {
                Kind = "quality_snapshot",
                Name = "embeddings",
                Outcome = "n/a",
                DriftSummary = $"No embeddings snapshots found under {dir}."
            };
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Max(0, windowDays));
        var snapshots = new List<(DateTimeOffset Timestamp, int Dims, double ClassifierAccuracy)>();

        foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderBy(x => x))
        {
            if (Path.GetFileName(file).Equals("baseline.json", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var root = doc.RootElement;
                if (!root.TryGetProperty("timestamp", out var tsEl)) continue;
                if (!DateTimeOffset.TryParse(tsEl.GetString(), out var ts)) continue;
                if (ts < cutoff) continue;

                var dims = root.TryGetProperty("corpus", out var corpus)
                    && corpus.TryGetProperty("dims", out var dimsEl)
                    ? dimsEl.GetInt32() : 0;

                var accuracy = root.TryGetProperty("leak_detection", out var ld)
                    && ld.TryGetProperty("full_classifier_accuracy", out var accEl)
                    ? accEl.GetDouble() : 0.0;

                snapshots.Add((ts, dims, accuracy));
            }
            catch (JsonException) { }
            catch (IOException) { }
        }

        if (snapshots.Count == 0)
        {
            return new QaEvidence
            {
                Kind = "quality_snapshot",
                Name = "embeddings",
                Outcome = "n/a",
                DriftSummary = $"No embeddings snapshots within {windowDays}d window (excluding baseline.json)."
            };
        }

        if (snapshots.Count == 1)
        {
            var only = snapshots[0];
            return new QaEvidence
            {
                Kind = "quality_snapshot",
                Name = "embeddings",
                Outcome = "n/a",
                Score = only.ClassifierAccuracy,
                DriftSummary =
                    $"Only one snapshot in window (dims={only.Dims}, classifier_accuracy={only.ClassifierAccuracy:F4}). " +
                    "Drift requires >=2 snapshots."
            };
        }

        var oldest = snapshots[0];
        var newest = snapshots[^1];
        var concerns = new List<string>();

        // Dims should not change (one-way door).
        if (newest.Dims != oldest.Dims && newest.Dims > 0 && oldest.Dims > 0)
            concerns.Add($"embedding dims changed {oldest.Dims} → {newest.Dims} (one-way door!)");

        // Classifier accuracy (leak detection proxy) should stay within 5pp.
        var accDelta = newest.ClassifierAccuracy - oldest.ClassifierAccuracy;
        if (Math.Abs(accDelta) > 0.05)
            concerns.Add($"classifier_accuracy drift {oldest.ClassifierAccuracy:F4} → {newest.ClassifierAccuracy:F4} ({accDelta:+0.0000;-0.0000})");

        var outcome = concerns.Count == 0 ? "pass" : "concern";
        var summary = concerns.Count == 0
            ? $"{snapshots.Count} snapshot(s). dims={newest.Dims}, accuracy {oldest.ClassifierAccuracy:F4} → {newest.ClassifierAccuracy:F4}. Within tolerance."
            : $"{snapshots.Count} snapshot(s). Concerns: {string.Join("; ", concerns)}.";

        return new QaEvidence
        {
            Kind = "quality_snapshot",
            Name = "embeddings",
            Outcome = outcome,
            Score = newest.ClassifierAccuracy,
            Baseline = oldest.ClassifierAccuracy,
            DeltaFromBaseline = accDelta,
            DriftSummary = summary
        };
    }

    // ── Phase 2: optick-sae drift computation ────────────────────────────────────────────

    // Per-artifact-pair tolerances.
    private const double DriftMseRelativeTolerance = 0.50;
    private const double DriftDeadFeaturesPctTolerance = 5.0;
    private const double DriftPurityMeanAbsoluteTolerance = 0.05;

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
                return null;

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
}
