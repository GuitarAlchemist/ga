namespace GA.Business.ML.Agents.Intents;

using System.Text.RegularExpressions;

/// <summary>
/// Default <see cref="IRoutingHintProvider"/> with a small, opinionated
/// rule set covering the high-precision surface patterns that the
/// embedding-similarity router was getting wrong on adjacent intent
/// centroids (the 2026-05-08 capability-matrix smoke surfaced six).
/// </summary>
/// <remarks>
/// <para>
/// Each rule is one regex + one target intent-id + a boost magnitude.
/// Boost magnitude is uniform <see cref="BoostMagnitude"/> for all rules
/// — codex CLI 2026-05-08 specifically rejected per-rule weighting on
/// the grounds that "if a rule needs +0.15, the rule should probably be
/// a hard pre-route, not a boost." Hard pre-routes belong in
/// <c>ProductionOrchestrator.TrySelectDeterministicAgent</c> (today's
/// example: explicit voicing requests with chord-literal + voicing
/// keyword).
/// </para>
/// <para>
/// Rules are intentionally narrow. They should match on multi-word
/// phrases or unambiguous tokens, not on common words like "key" or
/// "chord" alone. If a rule starts firing on prompts it shouldn't,
/// tighten the pattern rather than reducing the boost.
/// </para>
/// </remarks>
public sealed class DefaultRoutingHintProvider : IRoutingHintProvider
{
    /// <summary>
    /// Codex-recommended boost magnitude (+0.06). Sized so it can break
    /// ties between two cosine scores within ~0.1 but cannot override a
    /// dominant semantic win. Adjust at the constant — don't add a
    /// per-rule override field; that's the trapdoor codex warned about.
    /// </summary>
    public const float BoostMagnitude = 0.06f;

    /// <summary>
    /// Single source of truth for the rule set. Each entry pairs a
    /// compiled regex with the canonical <see cref="IIntent.Id"/> it
    /// hints toward. Matching is case-insensitive.
    /// </summary>
    /// <remarks>
    /// Intent IDs follow the <c>OrchestratorSkillIntent</c> convention
    /// of <c>$"skill.{skill.Name.ToLowerInvariant().Replace(' ', '-')}"</c>
    /// so e.g. <c>ChordSubstitutionSkill</c> → <c>skill.chordsubstitution</c>.
    /// Verified against the live registry by the 2026-05-08 capability
    /// matrix smoke.
    /// </remarks>
    private static readonly (Regex Pattern, string IntentId)[] Rules =
    [
        // Substitutions / reharmonization
        (new Regex(@"\b(substitut\w*|reharmoniz\w*|tritone\s+sub)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "skill.chordsubstitution"),

        // Key identification — "what key is X in", "key of <progression>"
        (new Regex(@"\b(what\s+key|key\s+is|identify\s+the\s+key)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "skill.keyidentification"),

        // Fret span / playability
        (new Regex(@"\b(fret\s+span|span\s+of\s+\w+\s+chord|playability)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "skill.fretspan"),

        // Interval names — anchored on the precise interval phrase so this
        // doesn't fire on every prompt that mentions "third" or "fifth"
        // in passing.
        (new Regex(@"\b(perfect\s+(fourth|fifth|octave|unison)|major\s+(second|third|sixth|seventh)|minor\s+(second|third|sixth|seventh)|tritone|augmented\s+\w+|diminished\s+\w+)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "skill.interval"),

        // Scale info — "<key> scale" patterns. Avoids bare "scale" which
        // overlaps with modes / scaleinfo / circle-of-fifths.
        (new Regex(@"\b(?:[A-G](?:#|b)?\s+(?:major|minor|natural\s+minor|melodic\s+minor|harmonic\s+minor)\s+scale|notes\s+in\s+(?:[A-G](?:#|b)?\s+)?(?:major|minor)\s+scale)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "skill.scaleinfo"),

        // Modes — explicit mode-name mention. Added 2026-05-12 to close
        // mo-3 ("what notes are in G mixolydian" → was misrouting to
        // skill.scaleinfo because "notes in <key>" overlapped both
        // skills' example sets). The mode name IS the discriminator;
        // boosting skill.modes by +0.06 tips the tie reliably.
        // Pattern is anchored on word boundaries so embedded substrings
        // (e.g. "Lydiansville") don't fire.
        (new Regex(@"\b(ionian|dorian|phrygian|lydian|mixolydian|aeolian|locrian)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "skill.modes"),

        // Chord info — chord-tone questions and quality-symbol mentions.
        // The "tell me about <chord>" / "what notes are in <chord>" shapes
        // were misrouting to modes / scale-info because of overlap on
        // "chord" / "notes" alone.
        (new Regex(@"\b(chord\s+tones?|notes\s+in\s+(?:[A-G](?:#|b)?(?:maj|min|m|dim|aug|sus|add|dom)?\d*)|tell\s+me\s+about\s+[A-G])\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "skill.chordinfo"),

        // Transpose — added post-baseline-2026-05-11 to close a 4/5 F1
        // hole. Failing prompts in the eval corpus included "transpose
        // this progression down a half step", "transpose C-Am-F-G to G
        // major", "shift this progression up a whole step", and "bring D
        // minor down to A minor".
        //
        // Pattern: bare "transpose<suffix>" (single-token,
        // music-unambiguous), OR shift/bring/move + 0-5 intervening
        // tokens + a MUSIC-DIRECTION suffix (up | down | to <key-letter>).
        // PR #178 review (correctness MED-1) tightened from the prior
        // music-noun-anchored version, which fired on idiomatic "bring
        // this song to a close" / "move this song higher in the playlist"
        // / "shift this tune to the front". The direction-suffix anchor
        // is stricter: "to a close" / "to the front" / "higher" don't
        // qualify, but legitimate transpose phrasings ("bring D minor
        // down to A minor" has "down"; "shift this progression up a
        // whole step" has "up") stay matched.
        //
        // English drops the trailing 'e' before -ing: "transposing" not
        // "transposeing". So we anchor on the 'transpos' prefix + at least
        // one trailing word char, which covers transpose / transposed /
        // transposes / transposing / transposition.
        // `(?-i:[A-G])` locally disables IgnoreCase for the key-letter group.
        // Without this, `to a close` and `to a conclusion` match because
        // lowercase `a` is treated as `[A-Ga-g]`. Music writers use uppercase
        // for key names (D minor, A major), so case-sensitivity here is a
        // legitimate signal. Lowercase-key prompts ("transpose to g major")
        // still hit via the `\btranspos\w+\b` prefix branch.
        (new Regex(@"\btranspos\w+\b|\b(shift|bring|move)\b\s+\S+(?:\s+\S+){0,5}?\s+(?:up|down|to\s+(?-i:[A-G]))\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "skill.transpose"),
    ];

    public IReadOnlyDictionary<string, float> GetDeltas(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new Dictionary<string, float>(0);
        }

        var deltas = new Dictionary<string, float>(StringComparer.Ordinal);

        // Each rule contributes at most one boost per intent — codex's
        // explicit "capped once per intent" guidance. Multiple matching
        // rules pointing at the same intent don't compound.
        foreach (var (pattern, intentId) in Rules)
        {
            if (!pattern.IsMatch(query)) continue;
            deltas[intentId] = BoostMagnitude;
        }

        return deltas;
    }
}
