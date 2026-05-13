namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;
using GA.Business.Config;

/// <summary>
/// Domain-backed modes skill — pulls families and modes from
/// <see cref="ModesConfig.GetModalFamilies"/> (which reads <c>Modes.yaml</c>
/// and exposes 31 mode families: Major Scale, Melodic Minor, Harmonic Minor,
/// Harmonic Major, Whole Tone, Diminished, Hungarian Major, Hirajoshi, In Sen,
/// Neapolitan, Bebop, Prometheus, Enigmatic, Double Harmonic, etc.).
/// Zero hardcoded music-theory content — the skill is a thin adapter that
/// classifies the query, calls the domain, and formats the result.
/// </summary>
/// <remarks>
/// <para>
/// This is the PILOT refactor for the Domain-Backed Skills architecture
/// (<c>docs/plans/2026-05-13-skills-domain-backed-refactor-plan.md</c>).
/// Predecessor: 7 hand-written diatonic rows in this same class plus a
/// frozen <c>skills/modes/SKILL.md</c> catalog body. Both were point-in-time
/// snapshots that ignored 24 of 31 mode families already modelled in
/// <c>GA.Business.Config/Modes.yaml</c>.
/// </para>
/// <para>
/// Surfaced live 2026-05-13 when the user asked "what are other famous modes?"
/// and got back only the 7 diatonic ones. This commit closes that gap by
/// querying the domain at request time so the skill's coverage is bounded
/// by the YAML config, not by what the skill author happened to write.
/// </para>
/// </remarks>
public sealed class ModesSkill(ILogger<ModesSkill> logger) : IOrchestratorSkill
{
    public string Name        => "Modes";
    public string Description =>
        "Modes across all configured families: diatonic (Ionian–Locrian), " +
        "melodic minor (incl. Lydian dominant, altered scale), harmonic minor " +
        "(incl. Phrygian dominant, Hungarian minor), harmonic major, symmetric " +
        "(whole-tone, diminished/octatonic, augmented), and folk/world (Hirajoshi, " +
        "In Sen, Prometheus, Enigmatic, etc.). Domain-backed — every answer is " +
        "sourced live from GA.Business.Config.ModesConfig.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "What are the modes of the major scale",
        "List the diatonic modes",
        "What are other famous modes",
        "What are the modes of melodic minor",
        "Modes of harmonic minor",
        "What is Lydian dominant",
        "What is Phrygian dominant",
        "What is the altered scale",
        "Tell me about Hungarian minor",
        "What is the whole tone scale",
        "What is the diminished scale",
        "What modes are non-diatonic",
        "Show me all the mode families",
        "What is Hirajoshi",
        "What modes have a major 7th",
        "Mixolydian versus Ionian differences",
        "Characteristics of Locrian",
    ];

    private static readonly Regex ModesPattern =
        new(@"\bmodes?\b|\bscale\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool CanHandle(string message) => false;  // semantic-routing only

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var families = ModesConfig.GetModalFamilies();
        if (families.Count == 0)
        {
            logger.LogWarning("ModesSkill: ModesConfig returned 0 families — falling back to a minimal answer");
            return Task.FromResult(MinimalFallback());
        }

        var query = (message ?? string.Empty).ToLowerInvariant();
        var asksForFamilyListing = AsksForFamilyListing(query);

        // Classify the query against the domain. Order matters — more
        // specific intents win.

        // 1) Family-listing pattern ("modes of X", "X modes", "the X family")
        //    + a recognised family name. Wins over single-mode match because
        //    "modes of melodic minor" contains "melodic minor" which is BOTH a
        //    mode name and a family-name prefix. Fixed 2026-05-13 after pilot
        //    smoke test showed "modes of melodic minor" returning one mode
        //    instead of seven.
        var familyHit = TryFindFamilyByName(families, query);
        if (familyHit is not null && asksForFamilyListing)
            return Task.FromResult(FormatFamily(familyHit, families.Count));

        // 2) Specific mode by name (e.g. "what is lydian dominant").
        var modeHit = TryFindModeByName(families, query);
        if (modeHit is var (modeFamily, modeData) && modeFamily is not null && modeData is not null)
            return Task.FromResult(FormatSingleMode(modeFamily, modeData));

        // 3) Family name without an explicit listing pattern — interpret as
        //    "tell me about that family" (e.g. "harmonic minor").
        if (familyHit is not null)
            return Task.FromResult(FormatFamily(familyHit, families.Count));

        // 4) "Other / more / non-diatonic / all" → cross-family summary.
        if (AsksForBroadOverview(query))
            return Task.FromResult(FormatAllFamilies(families));

        // 5) Default → diatonic family. Same behaviour as the legacy
        //    "what are the modes of the major scale" answer.
        var diatonic = families.FirstOrDefault(f =>
            f.Name.Contains("Major Scale", StringComparison.OrdinalIgnoreCase));
        if (diatonic is not null)
            return Task.FromResult(FormatFamily(diatonic, families.Count));

        // Last resort.
        return Task.FromResult(FormatAllFamilies(families));
    }

    private static readonly string[] FamilyListingMarkers =
    [
        "modes of",
        "modes in",
        " modes",       // matches "harmonic minor modes", "melodic minor modes"
        "scale modes",
        "scale family",
        "mode family",
    ];

    private static bool AsksForFamilyListing(string lowerQuery)
    {
        foreach (var marker in FamilyListingMarkers)
            if (lowerQuery.Contains(marker)) return true;
        return false;
    }

    // ── Query classification ─────────────────────────────────────────────────

    private static (ModesConfig.ModalFamilyInfo? Family, ModesConfig.ModeData? Mode) TryFindModeByName(
        IReadOnlyList<ModesConfig.ModalFamilyInfo> families,
        string lowerQuery)
    {
        // Longest-name-wins: "Phrygian Dominant" must beat "Phrygian" when both
        // appear as substrings of the query. Without ordering by descending
        // length the diatonic mode shadows the harmonic-minor-derived one.
        // Surfaced 2026-05-13 pilot smoke test.
        var allModes = families
            .SelectMany(f => f.Modes.Select(m => (Family: f, Mode: m)))
            .OrderByDescending(pair => pair.Mode.Name.Length)
            .ToList();

        foreach (var (family, mode) in allModes)
        {
            var name = mode.Name.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (lowerQuery.Contains(name))
                return (family, mode);
        }
        return (null, null);
    }

    private static ModesConfig.ModalFamilyInfo? TryFindFamilyByName(
        IReadOnlyList<ModesConfig.ModalFamilyInfo> families,
        string lowerQuery)
    {
        // Aliases: the human asks "modes of melodic minor" or "the harmonic minor
        // modes"; the YAML family is "Melodic Minor Family". Strip "Family" and
        // match on the prefix.
        foreach (var family in families)
        {
            var stripped = family.Name
                .Replace(" Family", "", StringComparison.OrdinalIgnoreCase)
                .ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(stripped)) continue;
            if (lowerQuery.Contains(stripped))
                return family;
        }
        return null;
    }

    private static readonly string[] BroadOverviewMarkers =
    [
        "other mode",
        "other famous mode",
        "non-diatonic",
        "non diatonic",
        "all mode",
        "every mode",
        "all the mode",
        "list mode familie",
        "mode familie",
        "what modes are",
        "show me all",
        "more modes",
        "different modes",
        "additional modes",
        "exotic mode",
    ];

    private static bool AsksForBroadOverview(string lowerQuery)
    {
        foreach (var marker in BroadOverviewMarkers)
            if (lowerQuery.Contains(marker)) return true;
        return false;
    }

    // ── Output formatting ────────────────────────────────────────────────────

    private AgentResponse FormatSingleMode(ModesConfig.ModalFamilyInfo family, ModesConfig.ModeData mode)
    {
        var sb = new StringBuilder();
        var degree = family.Modes.ToList().FindIndex(m => m.Name == mode.Name) + 1;
        sb.Append($"**{mode.Name}** is mode {degree} of the **{StripFamilySuffix(family.Name)}** family");
        if (!string.IsNullOrWhiteSpace(mode.Notes))
            sb.Append($"; on C its notes are `{mode.Notes}`");
        sb.Append('.');
        sb.AppendLine();
        if (mode.CharacteristicIntervals.Count > 0)
        {
            sb.AppendLine();
            sb.Append("Characteristic interval(s): ");
            sb.Append(string.Join(", ", mode.CharacteristicIntervals.Select(i => $"`{i}`")));
            sb.Append('.');
        }

        logger.LogDebug("ModesSkill: returning single-mode answer for {Family}/{Mode}",
            family.Name, mode.Name);

        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString(),
            Confidence = 1.0f,
            Evidence   =
            [
                $"Source: ModesConfig.GetModalFamilies()",
                $"Family: {family.Name} ({family.Modes.Count} modes)",
                $"Mode: {mode.Name} (degree {degree})",
            ],
        };
    }

    private AgentResponse FormatFamily(ModesConfig.ModalFamilyInfo family, int totalFamilies)
    {
        var sb = new StringBuilder();
        var familyLabel = StripFamilySuffix(family.Name);
        sb.AppendLine($"The **{familyLabel}** family has {family.Modes.Count} mode{(family.Modes.Count == 1 ? "" : "s")}:");
        sb.AppendLine();

        var idx = 1;
        foreach (var mode in family.Modes)
        {
            sb.Append($"{idx}. **{mode.Name}**");
            if (!string.IsNullOrWhiteSpace(mode.Notes))
                sb.Append($" — on C: `{mode.Notes}`");
            if (mode.CharacteristicIntervals.Count > 0)
                sb.Append($" — characteristic: {string.Join(", ", mode.CharacteristicIntervals.Select(i => $"`{i}`"))}");
            sb.AppendLine();
            idx++;
        }

        sb.AppendLine();
        sb.Append($"There are {totalFamilies} mode families in the catalog overall. Ask about a specific family (\"modes of melodic minor\", \"harmonic major modes\") or a single mode (\"what is Lydian dominant?\") for more, or \"what are other famous modes?\" for the cross-family overview.");

        logger.LogDebug("ModesSkill: returning family answer for {Family} ({Count} modes)",
            family.Name, family.Modes.Count);

        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString(),
            Confidence = 1.0f,
            Evidence   =
            [
                $"Source: ModesConfig.GetModalFamilies()",
                $"Family: {family.Name}",
                $"Modes returned: {family.Modes.Count}",
                $"Total families available: {totalFamilies}",
            ],
        };
    }

    private AgentResponse FormatAllFamilies(IReadOnlyList<ModesConfig.ModalFamilyInfo> families)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"The catalog has **{families.Count} mode families** total — these are the named scale collections from which modes are derived:");
        sb.AppendLine();

        foreach (var family in families)
        {
            var label = StripFamilySuffix(family.Name);
            var firstFew = family.Modes.Take(3).Select(m => m.Name);
            var more = family.Modes.Count > 3 ? $" + {family.Modes.Count - 3} more" : "";
            sb.AppendLine($"- **{label}** ({family.Modes.Count} mode{(family.Modes.Count == 1 ? "" : "s")}): {string.Join(", ", firstFew)}{more}");
        }

        sb.AppendLine();
        sb.Append("Ask about a specific family (e.g. \"modes of harmonic minor\", \"melodic minor modes\", \"whole tone scale\") or a specific mode (\"what is Phrygian dominant?\", \"what is the altered scale?\") for detail.");

        logger.LogDebug("ModesSkill: returning cross-family overview ({Count} families)", families.Count);

        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString(),
            Confidence = 1.0f,
            Evidence   =
            [
                $"Source: ModesConfig.GetModalFamilies()",
                $"Families returned: {families.Count}",
                $"Total modes: {families.Sum(f => f.Modes.Count)}",
            ],
        };
    }

    private static AgentResponse MinimalFallback() => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = "The mode catalog is unavailable in this environment. Try again later, or ask about a specific scale by name.",
        Confidence = 0.3f,
        Evidence   = ["Source: ModesConfig.GetModalFamilies() returned empty"],
    };

    private static string StripFamilySuffix(string familyName) =>
        familyName.Replace(" Family", "", StringComparison.OrdinalIgnoreCase);
}
