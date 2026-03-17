namespace GA.Business.ML.Agents.Hooks;

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Hook that enforces Demerzel governance policies — truthfulness annotations,
/// proportionality checks, and confidence-based escalation from the alignment policy.
/// Degrades gracefully when governance files are not present.
/// </summary>
public sealed partial class GovernanceHook(ILogger<GovernanceHook> logger) : IChatHook
{
    private static readonly Lazy<GovernanceConfig?> Config = new(GovernanceConfig.Load);

    public Task<HookResult> OnRequestReceived(ChatHookContext ctx, CancellationToken ct = default)
    {
        var config = Config.Value;
        if (config is null) return Task.FromResult(HookResult.Continue);

        var message = ctx.CurrentMessage;
        var mutations = new List<string>();

        // Article 1 (Truthfulness): remind agent to state uncertainty
        mutations.Add("[governance:truthfulness] State uncertainty explicitly when confidence is below 0.7.");

        // Article 4 (Proportionality): flag scope-expanding requests
        if (message.Length > 200 && ProportionalityPattern().IsMatch(message))
        {
            mutations.Add("[governance:proportionality] This request has broad scope. Match response scope to what was actually asked.");
        }

        var enriched = string.Join("\n", mutations) + "\n\n" + message;
        logger.LogDebug("GovernanceHook: injected {Count} governance directives", mutations.Count);
        return Task.FromResult(HookResult.Mutate(enriched));
    }

    public Task<HookResult> OnResponseSent(ChatHookContext ctx, CancellationToken ct = default)
    {
        var config = Config.Value;
        if (config is null) return Task.FromResult(HookResult.Continue);
        if (ctx.Response is not { } response) return Task.FromResult(HookResult.Continue);

        var thresholds = config.Thresholds;
        string? annotation = response.Confidence switch
        {
            >= 0.9f => null,
            >= 0.7f => null, // debug-level only
            >= 0.5f => "*[Note: Moderate confidence]*",
            >= 0.3f => "*[Escalation recommended]*",
            _       => "*[WARNING: Human review recommended]*"
        };

        if (response.Confidence >= 0.7f && response.Confidence < 0.9f)
            logger.LogDebug("GovernanceHook: confidence {C:F2} — proceed with note", response.Confidence);

        if (annotation is not null)
        {
            ctx.Response = response with { Result = response.Result + "\n\n" + annotation };
            logger.LogDebug("GovernanceHook: appended confidence annotation for {C:F2}", response.Confidence);
        }

        return Task.FromResult(HookResult.Continue);
    }

    [GeneratedRegex(@"\b(refactor everything|change all|rewrite|redo all|replace all)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ProportionalityPattern();

    /// <summary>
    /// Lazily loaded governance configuration from the Demerzel submodule.
    /// </summary>
    private sealed class GovernanceConfig
    {
        public Dictionary<int, string> Articles { get; init; } = new();
        public AlignmentThresholds Thresholds { get; init; } = new();

        public static GovernanceConfig? Load()
        {
            try
            {
                var repoRoot = FindRepoRoot();
                if (repoRoot is null) return null;

                var constitutionPath = Path.Combine(repoRoot, "governance", "demerzel", "constitutions", "default.constitution.md");
                var policyPath = Path.Combine(repoRoot, "governance", "demerzel", "policies", "alignment-policy.yaml");

                if (!File.Exists(constitutionPath) || !File.Exists(policyPath))
                    return null;

                // Parse constitution articles
                var articles = new Dictionary<int, string>();
                var constitutionText = File.ReadAllText(constitutionPath);
                var articleMatches = Regex.Matches(constitutionText, @"### Article (\d+): (.+?)\n\n(.+?)(?=\n\n###|\n\n##|\z)", RegexOptions.Singleline);
                foreach (Match m in articleMatches)
                {
                    if (int.TryParse(m.Groups[1].Value, out var num))
                        articles[num] = m.Groups[3].Value.Trim();
                }

                // Parse alignment policy thresholds
                var policyYaml = File.ReadAllText(policyPath);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();
                var policy = deserializer.Deserialize<AlignmentPolicyYaml>(policyYaml);
                var thresholds = new AlignmentThresholds
                {
                    ProceedAutonomously = policy?.ConfidenceThresholds?.ProceedAutonomously ?? 0.9f,
                    ProceedWithNote = policy?.ConfidenceThresholds?.ProceedWithNote ?? 0.7f,
                    AskForConfirmation = policy?.ConfidenceThresholds?.AskForConfirmation ?? 0.5f,
                    EscalateToHuman = policy?.ConfidenceThresholds?.EscalateToHuman ?? 0.3f,
                };

                return new GovernanceConfig { Articles = articles, Thresholds = thresholds };
            }
            catch
            {
                return null;
            }
        }

        private static string? FindRepoRoot()
        {
            var dir = AppContext.BaseDirectory;
            for (var i = 0; i < 10; i++)
            {
                if (Directory.Exists(Path.Combine(dir, "governance", "demerzel")))
                    return dir;
                var parent = Directory.GetParent(dir)?.FullName;
                if (parent is null || parent == dir) break;
                dir = parent;
            }
            return null;
        }
    }

    private sealed class AlignmentThresholds
    {
        public float ProceedAutonomously { get; init; } = 0.9f;
        public float ProceedWithNote { get; init; } = 0.7f;
        public float AskForConfirmation { get; init; } = 0.5f;
        public float EscalateToHuman { get; init; } = 0.3f;
    }

    private sealed class AlignmentPolicyYaml
    {
        [YamlMember(Alias = "confidence_thresholds")]
        public ConfidenceThresholdsYaml? ConfidenceThresholds { get; set; }
    }

    private sealed class ConfidenceThresholdsYaml
    {
        [YamlMember(Alias = "proceed_autonomously")]
        public float ProceedAutonomously { get; set; } = 0.9f;

        [YamlMember(Alias = "proceed_with_note")]
        public float ProceedWithNote { get; set; } = 0.7f;

        [YamlMember(Alias = "ask_for_confirmation")]
        public float AskForConfirmation { get; set; } = 0.5f;

        [YamlMember(Alias = "escalate_to_human")]
        public float EscalateToHuman { get; set; } = 0.3f;
    }
}
