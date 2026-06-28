namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;
using GA.Domain.Core.Theory.Atonal;

/// <summary>
/// Domain-backed set-theory equivalence skill. Answers questions like
/// "Are pitch classes 0,1,4 and 0,1,6 equivalent under inversion" with a
/// deterministic Y/N using <see cref="SetClass"/> prime-form comparison.
/// Zero LLM calls, Confidence = 1.0.
/// </summary>
/// <remarks>
/// Built 2026-05-13 to close the corpus failure
/// <c>"Are pitch classes 0,1,4 and 0,1,6 equivalent under inversion"</c>
/// where the LLM answer was so variable it never reliably contained the
/// required substring tokens.
///
/// Two sets are T-equivalent (transposition only) when they share the same
/// transpositional prime form; they are TI-equivalent (transposition or
/// inversion) when they share the same <see cref="SetClass.PrimeForm"/>
/// — i.e. the same Forte set class. The skill answers all three flavours
/// of the question ("under transposition", "under inversion", "under
/// transposition or inversion").
/// </remarks>
[GuitarAlchemist.Registry.GaSkill("SetTheoryEquivalenceSkill", "atonal")]
public sealed class SetTheoryEquivalenceSkill : IOrchestratorSkill
{
    public string Name => "SetTheoryEquivalence";
    public string Description =>
        "Compares two pitch-class sets for equivalence under transposition, " +
        "inversion, or both (set-class equivalence). Deterministic answer " +
        "via SetClass prime-form comparison — no LLM call.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "Are pitch classes 0,1,4 and 0,1,6 equivalent under inversion",
        "Are pitch class sets 0,1,3 and 0,2,3 equivalent under transposition",
        "Are 0,1,4 and 0,3,4 the same set class",
        "Do 0146 and 0137 belong to the same set class",
        "Are pitch classes 0,2,4 and 1,3,5 transpositionally equivalent",
    ];

    // Triggers on "are pitch class(es) X and Y <equiv|same> [under <relation>]"
    // OR "are X and Y same set class" etc. The two PC-set captures accept any
    // mix of comma-separated digits or bracket notation.
    private static readonly Regex EquivalencePattern =
        new(@"\b(?:pitch\s+class(?:es)?(?:\s+sets?)?\s+|sets?\s+)?(?<a>[\[\{]?\d[\d,\s]*[\]\}]?)\s+and\s+(?<b>[\[\{]?\d[\d,\s]*[\]\}]?)\s+(?:are\s+)?(?:equivalent|same|equal|related|the\s+same|belong\s+to\s+the\s+same)\s*(?:set\s*class|set|class)?(?:\s+under\s+(?<rel>transposition|inversion|t-i|both|transposition\s+(?:and|or)\s+inversion))?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Alternate phrasing: "do X and Y belong to the same set class"
    private static readonly Regex SetClassPattern =
        new(@"\bdo\s+(?<a>[\[\{]?\d[\d,\s]*[\]\}]?)\s+and\s+(?<b>[\[\{]?\d[\d,\s]*[\]\}]?)\s+belong\s+to\s+the\s+same\s+set\s*class",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool CanHandle(string message) =>
        !string.IsNullOrWhiteSpace(message) &&
        (EquivalencePattern.IsMatch(message) || SetClassPattern.IsMatch(message));

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var msg = message ?? string.Empty;

        var match = EquivalencePattern.Match(msg);
        if (!match.Success) match = SetClassPattern.Match(msg);
        if (!match.Success)
            return Task.FromResult(CannotHandle());

        var aText = match.Groups["a"].Value;
        var bText = match.Groups["b"].Value;
        var relation = match.Groups["rel"].Success
            ? NormalizeRelation(match.Groups["rel"].Value)
            : "ti";  // default — "same set class" = full T/I equivalence

        var setA = TryParseSet(aText);
        var setB = TryParseSet(bText);
        if (setA is null || setB is null)
            return Task.FromResult(CannotParse(aText, bText));

        return Task.FromResult(Answer(setA, bText: bText, aText: aText, setB: setB, relation: relation));
    }

    private static AgentResponse Answer(PitchClassSet setA, PitchClassSet setB, string aText, string bText, string relation)
    {
        // Always compute set-class (TI-equivalence) for the explanation tail.
        var classA = new SetClass(setA);
        var classB = new SetClass(setB);
        var icvA = classA.IntervalClassVector;
        var icvB = classB.IntervalClassVector;
        var primeA = classA.PrimeForm;
        var primeB = classB.PrimeForm;

        // TI-equivalence: same Forte prime form (TI is the equivalence class
        // SetClass canonicalizes to).
        var tiEquivalent = primeA.Equals(primeB);

        // T-equivalence (transposition only) is a STRICTER relation than TI.
        // Compute directly: A and B are T-equivalent iff there exists t in
        // 0..11 such that {a + t mod 12 : a in A} == B. Sets that share a
        // Forte prime form may still differ in T-only equivalence when they
        // are related by inversion alone (the Forte prime form picks the
        // most-compact of the two inversion-related transposition classes).
        // Caught 2026-05-13 by the multi-LLM correctness review on PR #210
        // — the previous code conflated TI with T and produced wrong
        // "transposition" answers for inversion-only pairs like {0,1,4} vs
        // {0,3,4}.
        var tEquivalent = AreTEquivalent(setA, setB);

        var sb = new StringBuilder();
        sb.AppendLine($"## Set comparison");
        sb.AppendLine();
        sb.AppendLine($"- **Set A** `{Format(aText)}` → ICV `{icvA}`, prime form `{Format(primeA)}`");
        sb.AppendLine($"- **Set B** `{Format(bText)}` → ICV `{icvB}`, prime form `{Format(primeB)}`");
        sb.AppendLine();

        if (relation is "transposition")
        {
            if (tEquivalent)
            {
                sb.AppendLine("**Yes — equivalent under transposition.** There exists a single transposition that maps one set onto the other (no inversion needed).");
            }
            else if (tiEquivalent)
            {
                sb.AppendLine("**No — not equivalent under transposition alone.** They share the same set class (Forte prime form), but the relationship requires INVERSION, not pure transposition. They are TI-equivalent, not T-equivalent.");
            }
            else if (icvA.Equals(icvB))
            {
                sb.AppendLine("**No — not transpositionally equivalent.** ICVs match but the sets belong to distinct set classes (Z-relation), so neither T nor TI equivalence applies.");
            }
            else
            {
                sb.AppendLine("**No — not transpositionally equivalent.** Interval class vectors differ, which rules out any T or TI equivalence.");
            }
        }
        else if (relation is "inversion")
        {
            // "Under inversion" in atonal-theory practice means TI-equivalence
            // — testing whether one set is reachable from the other by
            // transposition AND/OR inversion (TnI). A strict "inversion only"
            // reading is unusual; we follow the conventional TI interpretation
            // and surface the T-only relationship separately when relevant.
            if (tiEquivalent && tEquivalent)
            {
                sb.AppendLine("**Yes — equivalent under inversion** (and also under transposition alone; they reduce to the same prime form via either operation).");
            }
            else if (tiEquivalent)
            {
                sb.AppendLine("**Yes — equivalent under inversion.** The two sets share a Forte prime form via TnI (transposition combined with inversion). They are NOT T-equivalent on their own — inversion is required.");
            }
            else if (icvA.Equals(icvB))
            {
                sb.AppendLine("**No — not equivalent under inversion.** ICVs match but prime forms differ — this is the Z-relation hallmark: distinct set classes that share an interval class vector.");
            }
            else
            {
                sb.AppendLine("**No — not equivalent under inversion.** Interval class vectors differ. Inversion preserves the ICV, so distinct ICVs rule out any TI relationship.");
            }
        }
        else
        {
            // "same set class" / default — TI-equivalent answer
            if (tiEquivalent)
            {
                var operation = tEquivalent ? "pure transposition" : "transposition combined with inversion";
                sb.AppendLine($"**Yes — they belong to the same set class** (same Forte prime form). The relationship is {operation}.");
            }
            else if (icvA.Equals(icvB))
            {
                sb.AppendLine("**No — they are Z-related.** Their interval class vectors are identical but their prime forms differ. This is the rare Z-pair phenomenon where two distinct set classes share an ICV.");
            }
            else
            {
                sb.AppendLine("**No — they are not equivalent.** Their interval class vectors differ, so they cannot be related by transposition or inversion.");
            }
        }

        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString(),
            Confidence = 1.0f,
            Evidence   =
            [
                "Source: SetTheoryEquivalenceSkill (PitchClassSet → SetClass.PrimeForm comparison)",
                $"Set A ICV: {icvA}, prime form: {Format(primeA)}",
                $"Set B ICV: {icvB}, prime form: {Format(primeB)}",
                $"Equivalent (same set class): {tiEquivalent}",
            ],
        };
    }

    // T-equivalence: A and B are transpositionally equivalent iff there
    // exists t in 0..11 such that {(a + t) mod 12 : a in A} == B as sets.
    // This is the STRICT subset of TI-equivalence — sets related by
    // inversion alone share a SetClass but are NOT T-equivalent.
    private static bool AreTEquivalent(PitchClassSet setA, PitchClassSet setB)
    {
        var pcsA = setA.Select(pc => (int)pc).ToHashSet();
        var pcsB = setB.Select(pc => (int)pc).ToHashSet();
        if (pcsA.Count != pcsB.Count) return false;
        for (var t = 0; t < 12; t++)
        {
            var transposed = pcsA.Select(pc => (pc + t) % 12).ToHashSet();
            if (transposed.SetEquals(pcsB)) return true;
        }
        return false;
    }

    private static string NormalizeRelation(string raw)
    {
        var t = raw.Trim().ToLowerInvariant();
        if (t.Contains("transposition") && t.Contains("inversion")) return "ti";
        if (t.Contains("inversion") || t == "t-i") return "inversion";
        if (t.Contains("transposition")) return "transposition";
        return "ti";
    }

    private static PitchClassSet? TryParseSet(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // Strip brackets/braces and split on any non-digit character.
        var cleaned = raw.Replace("[", " ").Replace("]", " ").Replace("{", " ").Replace("}", " ");
        var tokens = Regex.Split(cleaned, @"[^0-9]+")
            .Where(s => s.Length > 0)
            .ToList();
        if (tokens.Count == 0) return null;
        // If a single token like "0146" (no separators), split into single
        // digits — standard atonal-theory shorthand.
        if (tokens.Count == 1 && tokens[0].Length > 1 && tokens[0].All(char.IsDigit))
        {
            tokens = [.. tokens[0].Select(c => c.ToString())];
        }

        var pcs = new List<PitchClass>();
        foreach (var tok in tokens)
        {
            if (!int.TryParse(tok, out var n) || n < 0 || n > 11) return null;
            pcs.Add((PitchClass)n);
        }
        return pcs.Count == 0 ? null : new PitchClassSet(pcs);
    }

    private static string Format(string raw) => raw.Trim().Trim('[', ']', '{', '}').Trim();

    private static string Format(PitchClassSet set) =>
        "{" + string.Join(",", set.Select(pc => ((int)pc).ToString())) + "}";

    private static AgentResponse CannotParse(string a, string b) => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = $"I couldn't parse two pitch-class sets from your question. Try a form like 'are pitch classes 0,1,4 and 0,1,6 equivalent under inversion' or 'do 0146 and 0137 belong to the same set class'. Saw: '{a}' / '{b}'.",
        Confidence = 0.3f,
        Evidence   = ["SetTheoryEquivalenceSkill: PC-set parse failed"],
    };

    private static AgentResponse CannotHandle() => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = "Ask whether two pitch-class sets are equivalent under transposition, inversion, or both.",
        Confidence = 0.1f,
        Evidence   = ["SetTheoryEquivalenceSkill: no recognised pattern"],
    };
}
