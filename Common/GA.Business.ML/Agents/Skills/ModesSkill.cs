namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;
using GA.Business.Config;
using Microsoft.FSharp.Core;

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
public sealed partial class ModesSkill(ILogger<ModesSkill> logger) : IOrchestratorSkill
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
        // Bare diatonic-mode names without scale-family qualifiers.
        // Without these, "What is dorian" scored below the routing
        // threshold against the longer phrases and fell through to the
        // LLM cascade where it timed out (corpus regression #216,
        // 2026-05-16). Each canonical name needs its own embedding anchor.
        "What is Dorian",
        "What is Phrygian",
        "What is Lydian",
        "What is Mixolydian",
        "What is Aeolian",
        "What is Ionian",
        "What is Locrian",
        // Regional alt-names (Modes.yaml AlternateNames). Without these the
        // semantic router misroutes queries like "Tell me about Hijaz" to
        // fallback even though the alias is plumbed through to ModesSkill.
        "Tell me about Hijaz",
        "What is Maqam Hijaz",
        "What is Freygish",
        "What is Bhairavi",
        "What is the Byzantine scale",
        "What is the Spanish Gypsy scale",
        "What is Ahava Rabbah",
        "What modes have a major 7th",
        "Mixolydian versus Ionian differences",
        "Characteristics of Locrian",
        // Atonal-catalog queries (~200 families indexed by ICV)
        "What families have ICV <2 5 4 3 6 1>",
        "What is Forte number 7-29",
        "List symmetric families",
        "List atonal modal families",
        "List unnamed modal families",
        "What are modes of limited transposition",
    ];

    private static readonly Regex ModesPattern =
        new(@"\bmodes?\b|\bscale\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Deterministic fast-path: "what is <bareword>" / "tell me about <bareword>"
    // where the bareword is a known mode / mode-family name. The semantic
    // router used to handle these via embedding similarity, but bare queries
    // like "What is dorian" scored below threshold against ExamplePrompts
    // and fell through to the LLM cascade — which then timed out and
    // produced fallback-direct (corpus regression #216, 2026-05-16).
    public bool CanHandle(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var match = QuestionLeadInRegex().Match(message);
        if (!match.Success) return false;

        // Strip lead-in + trailing punctuation; treat remainder as a name query.
        var remainder = message
            .Substring(match.Length)
            .Trim()
            .TrimEnd('?', '.', '!')
            .Trim();
        if (remainder.Length == 0 || remainder.Length > 60) return false;

        // Chord/triad in the remainder signals chord-construction intent,
        // not mode intent — route to ChordInfoSkill instead. Without this,
        // "tell me about a major seventh chord" matches the YAML entry
        // "Major Seventh Chord Family" and ModesSkill answers a chord
        // question with a phantom mode family (#220).
        var lowerRemainder = remainder.ToLowerInvariant();
        if (lowerRemainder.Contains("chord", StringComparison.Ordinal) ||
            lowerRemainder.Contains("triad", StringComparison.Ordinal))
        {
            return false;
        }

        // Check known family + mode aliases (case-insensitive). Cheap —
        // ModesConfig is in-memory. Chord/triad families are filtered out
        // because they describe chord *construction*, not modal families.
        var families = GetTonalModalFamilies();
        if (families.Count == 0) return false;
        var lower = remainder.ToLowerInvariant();

        foreach (var family in families)
        {
            foreach (var alias in ExtractNameAliases(family.Name))
                if (lower == alias) return true;
            foreach (var mode in family.Modes)
                foreach (var alias in ExtractNameAliases(mode.Name))
                    if (lower == alias) return true;
        }
        return false;
    }

    [GeneratedRegex(@"^\s*(what\s+is|what's|whats|tell\s+me\s+about|describe)\s+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QuestionLeadInRegex();

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var query = (message ?? string.Empty).ToLowerInvariant();

        // Try the atonal-catalog path first when the query mentions
        // atonal-theory vocabulary or matches an ICV / Forte-number
        // pattern. This domain has ~200 families enumerated from
        // PitchClassSet.Items (vs ~31 named tonal families); the user
        // explicitly asked for coverage of "modes" here so atonal lookups
        // get routed to the same skill rather than a sibling.
        var atonalAnswer = TryAnswerAtonal(query, message ?? string.Empty);
        if (atonalAnswer is not null)
            return Task.FromResult(atonalAnswer);

        var families = GetTonalModalFamilies();
        if (families.Count == 0)
        {
            logger.LogWarning("ModesSkill: ModesConfig returned 0 families — falling back to a minimal answer");
            return Task.FromResult(MinimalFallback());
        }

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
        // Build a flat (alias, family, mode) list so we can search across both
        // the canonical name and any alt-names extracted from parens. Then
        // longest-alias-wins so "Phrygian Dominant" beats "Phrygian" and
        // "Double Harmonic (Byzantine)" can match a bare "byzantine" query.
        // Surfaced 2026-05-13 pilot smoke test: queries for "Byzantine" hit
        // the default branch because the canonical mode name is "Double
        // Harmonic (Byzantine)" and substring lookup couldn't see the
        // parenthesised alt.
        var aliasedModes = families
            .SelectMany(f => f.Modes
                .SelectMany(m => GetAllAliasesForMode(m).Select(a => (Alias: a, Family: f, Mode: m))))
            .OrderByDescending(t => t.Alias.Length)
            .ToList();

        foreach (var (alias, family, mode) in aliasedModes)
        {
            if (string.IsNullOrWhiteSpace(alias)) continue;
            if (lowerQuery.Contains(alias))
                return (family, mode);
        }
        return (null, null);
    }

    /// <summary>
    /// Union of every searchable label for a mode: canonical name, the
    /// inside of any parenthesised "Foo (Bar)" alt, plus every entry in
    /// the YAML <c>AlternateNames</c> list. The last bucket covers cross-
    /// tradition names that Western theory doesn't surface — Phrygian
    /// Dominant → ["Spanish Phrygian", "Jewish Scale"], Dorian #4 →
    /// ["Romanian", "Ukrainian Dorian"], etc. — and was previously hidden
    /// behind a different domain type that GetModalFamilies didn't expose.
    /// </summary>
    private static IEnumerable<string> GetAllAliasesForMode(ModesConfig.ModeData mode)
    {
        foreach (var alias in ExtractNameAliases(mode.Name))
            yield return alias;

        if (mode.AlternateNames is null) yield break;
        foreach (var alt in mode.AlternateNames)
        {
            if (string.IsNullOrWhiteSpace(alt)) continue;
            foreach (var inner in ExtractNameAliases(alt))
                yield return inner;
        }
    }

    /// <summary>
    /// Extract searchable aliases from a mode name. The YAML uses
    /// "Canonical (Alt)" to record an alternate label — e.g.
    /// "Double Harmonic (Byzantine)" or "Major Pentatonic (Yo)". We want
    /// both the full name AND the inside of the parens to be matchable, so
    /// a query for "byzantine" hits the right mode.
    /// </summary>
    private static IEnumerable<string> ExtractNameAliases(string modeName)
    {
        if (string.IsNullOrWhiteSpace(modeName)) yield break;
        var trimmed = modeName.Trim();
        yield return trimmed.ToLowerInvariant();

        var open = trimmed.IndexOf('(');
        var close = trimmed.IndexOf(')');
        if (open >= 0 && close > open)
        {
            var canonical = trimmed[..open].Trim();
            var alt       = trimmed[(open + 1)..close].Trim();
            if (canonical.Length > 0)
                yield return canonical.ToLowerInvariant();
            if (alt.Length > 0)
                yield return alt.ToLowerInvariant();
        }
    }

    private static ModesConfig.ModalFamilyInfo? TryFindFamilyByName(
        IReadOnlyList<ModesConfig.ModalFamilyInfo> families,
        string lowerQuery)
    {
        // Two-pass lookup so we can tolerate (a) parenthesised aliases in the
        // family name and (b) word-order variations between query and YAML
        // ("Bebop dominant" vs "Dominant Bebop"). Pass 1: longest exact-
        // substring match. Pass 2: all-tokens-present (fallback). Pass 1
        // never returns a shorter match than a longer-name alternative.
        ModesConfig.ModalFamilyInfo? bestSubstringMatch = null;
        var bestSubstringLen = 0;
        ModesConfig.ModalFamilyInfo? tokenMatch = null;
        var bestTokenLen = 0;

        foreach (var family in families)
        {
            foreach (var alias in ExtractNameAliases(StripFamilySuffix(family.Name)))
            {
                if (string.IsNullOrWhiteSpace(alias)) continue;

                if (lowerQuery.Contains(alias) && alias.Length > bestSubstringLen)
                {
                    bestSubstringMatch = family;
                    bestSubstringLen   = alias.Length;
                }
                else if (AllTokensPresent(lowerQuery, alias) && alias.Length > bestTokenLen)
                {
                    tokenMatch     = family;
                    bestTokenLen   = alias.Length;
                }
            }
        }

        return bestSubstringMatch ?? tokenMatch;
    }

    /// <summary>
    /// True iff every space-separated token of <paramref name="phrase"/> of
    /// length &gt; 2 appears somewhere in <paramref name="query"/>. Lets
    /// "Bebop dominant" match "Dominant Bebop". Tokens of length &le; 2
    /// ("of", "in") are skipped to avoid over-matching English filler.
    /// </summary>
    private static bool AllTokensPresent(string query, string phrase)
    {
        var tokens = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2) return false; // Single-word phrases already covered by substring.
        foreach (var token in tokens)
        {
            if (token.Length <= 2) continue;
            if (!query.Contains(token)) return false;
        }
        return true;
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
        {
            sb.Append($"; on C its notes are `{mode.Notes}`");
            var formula = ComputeFormulaFromNotes(mode.Notes);
            if (!string.IsNullOrEmpty(formula))
                sb.Append($" (formula `{formula}`)");
        }
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

    /// <summary>
    /// Returns ModesConfig families with chord/triad entries filtered out.
    /// Modes.yaml contains 4 entries that describe chord construction
    /// (Major/Diminished/Augmented Triad, Major Seventh Chord) rather than
    /// modal families. They were leaking into mode-query answers (#220) —
    /// e.g. "tell me about a major seventh chord" returned a fake mode
    /// family. Filtering at the skill layer keeps the YAML intact for any
    /// cross-references (AtonalModalFamiliesConfig.TonalSubfamilies).
    /// </summary>
    private static IReadOnlyList<ModesConfig.ModalFamilyInfo> GetTonalModalFamilies()
    {
        var all = ModesConfig.GetModalFamilies();
        return all
            .Where(f => !IsChordOrTriadFamilyName(f.Name))
            .ToList();
    }

    private static bool IsChordOrTriadFamilyName(string name) =>
        name.Contains("Chord Family", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Triad Family", StringComparison.OrdinalIgnoreCase);

    // ── Atonal path ──────────────────────────────────────────────────────────

    private static readonly Regex IcvPattern =
        new(@"<\s*\d+\s+\d+\s+\d+\s+\d+\s+\d+\s+\d+\s*>", RegexOptions.Compiled);
    private static readonly Regex FortePattern =
        new(@"\b\d{1,2}-Z?\d{1,3}\b", RegexOptions.Compiled);

    private static readonly string[] AtonalMarkers =
    [
        "atonal mode",
        "atonal famil",
        "atonal scale",
        "all atonal",
        "list atonal",
        "icv",
        "interval class vector",
        "forte number",
        "forte numbers",
        "unnamed famil",
        "without a name",
        "have no traditional name",
        "all 200",
        "every modal famil",
        "all modal famil",
    ];

    private AgentResponse? TryAnswerAtonal(string lowerQuery, string originalQuery)
    {
        // 1) Explicit ICV pattern in the query — look up that exact family.
        var icvMatch = IcvPattern.Match(originalQuery);
        if (icvMatch.Success)
        {
            var familyOpt = AtonalModalFamiliesConfig.TryGetByIntervalClassVector(icvMatch.Value);
            if (FSharpOption<AtonalModalFamiliesConfig.AtonalModalFamily>.get_IsSome(familyOpt))
                return FormatAtonalFamily(familyOpt.Value);
            // ICV pattern but no match — explain that's not a valid ICV.
            return new AgentResponse
            {
                AgentId    = AgentIds.Theory,
                Result     = $"No modal family with interval class vector `{icvMatch.Value}` exists in the atonal catalog. Valid ICVs have six non-negative integers separated by spaces inside angle brackets.",
                Confidence = 0.8f,
                Evidence   = [$"AtonalModalFamiliesConfig.TryGetByIntervalClassVector({icvMatch.Value}) = None"],
            };
        }

        // 2) Forte-number pattern — look up by that label. A "-Z" infix is
        //    valid for hexachord Z-pairs (e.g. "6-Z29").
        var forteMatch = FortePattern.Match(originalQuery);
        if (forteMatch.Success && ContainsAnyOf(lowerQuery, "forte", "atonal", "pitch class set", "pc set", "set class"))
        {
            var families = AtonalModalFamiliesConfig.TryGetByForteNumber(forteMatch.Value);
            if (families.Length > 0)
                return FormatAtonalForteResults(forteMatch.Value, families);
            return new AgentResponse
            {
                AgentId    = AgentIds.Theory,
                Result     = $"No modal family with Forte number `{forteMatch.Value}` exists in the atonal catalog.",
                Confidence = 0.8f,
                Evidence   = [$"AtonalModalFamiliesConfig.TryGetByForteNumber({forteMatch.Value}) = []"],
            };
        }

        // 3) "symmetric modes" / "Messiaen modes" → list symmetric families.
        if (ContainsAnyOf(lowerQuery, "symmetric mode", "symmetric famil", "messiaen mode", "modes of limited transposition"))
        {
            var symmetric = AtonalModalFamiliesConfig.GetSymmetricFamilies().ToList();
            return FormatAtonalSymmetricListing(symmetric);
        }

        // 4) "unnamed families" / "families with no name" — pedagogical
        //    curiosity; surfaces what the atonal catalog has beyond the
        //    tonal-tradition naming.
        if (ContainsAnyOf(lowerQuery, "unnamed famil", "no traditional name", "without a name", "forte-numbered famil"))
        {
            var unnamed = AtonalModalFamiliesConfig.GetUnnamedFamilies().Take(20).ToList();
            var total   = AtonalModalFamiliesConfig.GetUnnamedFamilies().Count();
            return FormatAtonalUnnamedListing(unnamed, total);
        }

        // 5) Generic "atonal" / "ICV" / "interval class vector" — summary stats.
        var saysAtonal = false;
        foreach (var marker in AtonalMarkers)
            if (lowerQuery.Contains(marker)) { saysAtonal = true; break; }
        if (saysAtonal)
        {
            var all       = AtonalModalFamiliesConfig.GetAll().ToList();
            var symmetric = all.Count(f => f.IsSymmetric);
            var named     = all.Count(f => !f.FamilyName.StartsWith("Family-", StringComparison.Ordinal));
            return FormatAtonalSummary(all.Count, named, symmetric);
        }

        return null;
    }

    private AgentResponse FormatAtonalFamily(AtonalModalFamiliesConfig.AtonalModalFamily family)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"**{family.FamilyName}** — ICV `{family.IntervalClassVector}`, {family.NoteCount}-note set, {family.DistinctModeCount} distinct mode{(family.DistinctModeCount == 1 ? "" : "s")}{(family.IsSymmetric ? " (symmetric — modes of limited transposition)" : "")}.");
        if (family.ForteNumbers.Any())
            sb.AppendLine($"Forte number{(family.ForteNumbers.Length == 1 ? "" : "s")}: {string.Join(", ", family.ForteNumbers)}.");
        if (family.TonalSubfamilies.Any())
            sb.AppendLine($"Tonal analogs: {string.Join(", ", family.TonalSubfamilies)}.");

        sb.AppendLine();
        sb.AppendLine($"Modes ({family.Modes.Length}):");
        var idx = 1;
        foreach (var mode in family.Modes.Take(12))
        {
            var label = FSharpOption<string>.get_IsSome(mode.TonalAnalog) ? mode.TonalAnalog.Value : $"(unnamed mode {mode.Position})";
            sb.Append($"  {idx}. {label}");
            if (!string.IsNullOrWhiteSpace(mode.PitchClasses))
                sb.Append($" — PCs: `{mode.PitchClasses}`");
            sb.AppendLine();
            idx++;
        }
        if (family.Modes.Length > 12)
            sb.AppendLine($"  … + {family.Modes.Length - 12} more");

        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString(),
            Confidence = 1.0f,
            Evidence   =
            [
                $"Source: AtonalModalFamiliesConfig",
                $"Family: {family.FamilyName}",
                $"ICV: {family.IntervalClassVector}",
                $"Modes: {family.Modes.Length}",
            ],
        };
    }

    private AgentResponse FormatAtonalForteResults(string forte, IReadOnlyList<AtonalModalFamiliesConfig.AtonalModalFamily> families)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Forte number **{forte}** matches {families.Count} famil{(families.Count == 1 ? "y" : "ies")}:");
        sb.AppendLine();
        foreach (var f in families)
        {
            sb.AppendLine($"- **{f.FamilyName}** — ICV `{f.IntervalClassVector}` — {f.NoteCount} notes, {f.DistinctModeCount} mode{(f.DistinctModeCount == 1 ? "" : "s")}{(f.IsSymmetric ? " (symmetric)" : "")}");
            if (f.TonalSubfamilies.Any())
                sb.AppendLine($"  Tonal analogs: {string.Join(", ", f.TonalSubfamilies)}");
        }
        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString(),
            Confidence = 1.0f,
            Evidence   = [$"AtonalModalFamiliesConfig.TryGetByForteNumber({forte}) returned {families.Count} match(es)"],
        };
    }

    private AgentResponse FormatAtonalSymmetricListing(IReadOnlyList<AtonalModalFamiliesConfig.AtonalModalFamily> symmetric)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"**{symmetric.Count} symmetric / modes-of-limited-transposition families** in the atonal catalog:");
        sb.AppendLine();
        foreach (var f in symmetric.OrderBy(f => f.NoteCount).ThenBy(f => f.IntervalClassVector))
        {
            sb.AppendLine($"- **{f.FamilyName}** — ICV `{f.IntervalClassVector}` — {f.NoteCount} notes" + (f.ForteNumbers.Any() ? $" (Forte {string.Join("/", f.ForteNumbers)})" : ""));
        }
        sb.AppendLine();
        sb.Append("These are Messiaen's classical category — scales that repeat under rotation by fewer than 12 transpositions. The whole-tone scale (2 transpositions), octatonic (3), and hexatonic (4) are the most common in jazz / film vocabulary.");
        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString(),
            Confidence = 1.0f,
            Evidence   = [$"AtonalModalFamiliesConfig.GetSymmetricFamilies() returned {symmetric.Count} families"],
        };
    }

    private AgentResponse FormatAtonalUnnamedListing(IReadOnlyList<AtonalModalFamiliesConfig.AtonalModalFamily> unnamed, int total)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"**{total} modal families have no traditional name** in the atonal catalog — they're labelled `Family-{{ForteNumber}}` and indexed by interval class vector. First {unnamed.Count}:");
        sb.AppendLine();
        foreach (var f in unnamed)
            sb.AppendLine($"- **{f.FamilyName}** — ICV `{f.IntervalClassVector}` — {f.NoteCount} notes, {f.DistinctModeCount} mode{(f.DistinctModeCount == 1 ? "" : "s")}");
        sb.AppendLine();
        sb.Append("Ask for a specific ICV (e.g. `<2 5 4 3 6 1>`) or Forte number (e.g. `6-Z29`) for full detail.");
        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString(),
            Confidence = 1.0f,
            Evidence   = [$"AtonalModalFamiliesConfig.GetUnnamedFamilies() count = {total}"],
        };
    }

    private AgentResponse FormatAtonalSummary(int total, int named, int symmetric)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"The **atonal modal-families catalog** has **{total} families** indexed by interval class vector (ICV):");
        sb.AppendLine();
        sb.AppendLine($"- **{named}** have traditional names from Modes.yaml (Major Scale, Melodic Minor, Whole Tone, etc.)");
        sb.AppendLine($"- **{total - named}** are labelled `Family-{{ForteNumber}}` — they exist mathematically but no tradition names them");
        sb.AppendLine($"- **{symmetric}** are symmetric (modes of limited transposition) — whole-tone, octatonic, augmented, etc.");
        sb.AppendLine();
        sb.Append("Try: \"what families have ICV `<2 5 4 3 6 1>`?\", \"what is Forte number 7-29?\", \"list symmetric families\", \"list unnamed families\".");
        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString(),
            Confidence = 1.0f,
            Evidence   =
            [
                $"AtonalModalFamiliesConfig: {total} families total",
                $"Named (tonal analog): {named}",
                $"Symmetric: {symmetric}",
            ],
        };
    }

    private static bool ContainsAnyOf(string s, params string[] markers)
    {
        foreach (var m in markers)
            if (s.Contains(m)) return true;
        return false;
    }

    // ── Formula derivation (notes → scale-degree formula like "1 2 3 #4 5 6 b7")
    // Lifted to skill-level since no domain helper exists yet for this; once the
    // domain-backed refactor sweeps Tier 1, this belongs in ModeFormula.

    private static readonly Dictionary<string, int> PitchSemitone =
        new(StringComparer.Ordinal)
        {
            { "C",   0 }, { "C#",  1 }, { "Db",  1 }, { "D",   2 },
            { "D#",  3 }, { "Eb",  3 }, { "E",   4 }, { "Fb",  4 },
            { "E#",  5 }, { "F",   5 }, { "F#",  6 }, { "Gb",  6 },
            { "G",   7 }, { "G#",  8 }, { "Ab",  8 }, { "A",   9 },
            { "A#", 10 }, { "Bb", 10 }, { "B",  11 }, { "Cb", 11 },
            { "B#",  0 },
            // Double accidentals — show up in some altered/symmetric scales
            { "Dbb", 0 }, { "Ebb", 2 }, { "Fbb", 3 }, { "Gbb", 5 },
            { "Abb", 7 }, { "Bbb", 9 }, { "Cbb", 10 },
            { "C##", 2 }, { "D##", 4 }, { "E##", 6 }, { "F##", 7 },
            { "G##", 9 }, { "A##", 11 }, { "B##", 1 },
        };

    // C major reference: semitone offset for each natural degree 1..7.
    private static readonly int[] DegreeSemitones = [0, 2, 4, 5, 7, 9, 11];

    private static string ComputeFormulaFromNotes(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return string.Empty;
        var tokens = notes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return string.Empty;

        var parts = new List<string>(tokens.Length);
        for (var i = 0; i < tokens.Length; i++)
        {
            if (!PitchSemitone.TryGetValue(tokens[i], out var semi))
                return string.Empty;  // unknown token — bail rather than guess
            // For scales of <=7 notes, position maps directly to degree slot.
            // For 8-note (Bebop) or longer scales, modulo 7 keeps the reference
            // sensible — the formula notation is still recognizable.
            var slot = i % 7;
            var expected = DegreeSemitones[slot];
            var diff = semi - expected;
            // Normalize across octave boundary for late notes in 8+-note scales.
            if (diff > 6) diff -= 12;
            if (diff < -6) diff += 12;
            var acc = diff switch
            {
                -2 => "bb",
                -1 => "b",
                  0 => "",
                  1 => "#",
                  2 => "##",
                  _ => string.Empty  // out of expected range — omit accidental rather than emit garbage
            };
            parts.Add($"{acc}{i + 1}");
        }
        return string.Join(" ", parts);
    }
}
