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

        // Chord-identification-from-notes — "what/which chord is/contains
        // <NOTE> <NOTE> <NOTE>...". Two or more capital-letter pitch tokens
        // with optional accidentals, separated by spaces or commas. Anchored
        // on the "what/which chord is/contains" prefix so it doesn't fire
        // on prose that happens to mention pitches. Added 2026-05-14 — bare
        // "what chord is F A C E" was misrouting to chordsubstitution
        // because chord-letter pairs F+A read as two chord roots.
        (new Regex(@"\b(?:what|which)\s+chord\s+(?:is|contains|has|uses)\b\s*(?:the\s+notes?\s+)?(?-i:[A-G])(?:#|b)?(?:\s*,?\s*(?-i:[A-G])(?:#|b)?){1,5}\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "skill.chordinfo"),

        // Grothendieck bundle (stolen-from-demo 2026-05-14). Five anchors:
        //
        // 1. ICV (interval-class vector) — "ICV of <chord>" / "interval-class
        //    vector of {pcs}". Token "ICV" or the bigram is rare and music-
        //    specific enough to hard-anchor.
        (new Regex(@"\b(?:icv|interval[\s-]*class[\s-]*vector|interval[\s-]*vector)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "skill.intervalclassvector"),

        // 2. Grothendieck delta / harmonic distance between two chords.
        // Tightened 2026-05-14: removed bare `\bdelta\s+(?:from|between|to)\b`
        // which matched non-music phrasings ("delta from Tuesday to Friday").
        // Require the music-domain qualifier (ICV / harmonic / Grothendieck).
        (new Regex(@"\bgrothendieck[\s-]*(?:delta|distance)\b|\bharmonic(?:ally)?\s+(?:distance|far|distant)\b|\b(?:harmonic|icv)\s+delta\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "skill.grothendieckdelta"),

        // 3. ICV neighbors — "neighbors of <chord>" / "harmonically close to".
        (new Regex(@"\b(?:icv\s+neighbors?|harmonic(?:ally)?\s+(?:close|near|adjacent)|harmonic(?:al)?\s+neighbors?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "skill.icvneighbors"),

        // 4. ICV shortest path — "shortest harmonic path", "PC-set path".
        (new Regex(@"\bshortest(?:[\s-]*harmonic)?[\s-]*(?:path|route)\b|\bharmonic[\s-]*(?:path|route)\b|\bPC[\s-]*set\s+path\b|\bICV\s+path\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "skill.icvshortestpath"),

        // 5. Grothendieck parse — DSL expression interpretation.
        // Tightened 2026-05-14: dropped bare `\bgrothendieck\b` (also fires
        // for the delta hint, double-boosting). Dropped bare `\bpower\b` —
        // covered nowhere in the alternation but "power chord" prose would
        // accidentally route here. Kept domain-specific tokens and symbols.
        (new Regex(@"\b(?:tensor[\s-]*product|direct[\s-]*sum|pullback|pushout|coequalizer|natural[\s-]+transformation|subobject|power[\s-]+object|sheaf|functor[\s-]+composition)\b|⊗|⊕|∘|Hom\s*\(",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "skill.grothendieckparse"),

        // Alternate tunings — added 2026-05-14 to close BACKLOG dealbreaker #2.
        // Named-tuning tokens are music-unambiguous: DADGAD, drop-D, open-G,
        // open-D, double-drop-D, DGCGCD. The "X step down" phrase is also a
        // tuning-specific idiom in guitar context.
        (new Regex(@"\b(?:dadgad|drop[\s-]?d|open[\s-]?g|open[\s-]?d|double[\s-]?drop[\s-]?d|dgcgcd|half[\s-]?step[\s-]?down|whole[\s-]?step[\s-]?down)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "skill.alternatetunings"),

        // Voice leading — added 2026-05-14 to close BACKLOG dealbreaker #4.
        // The phrase "voice leading" (or "voice-leading", or "smooth voicing"
        // followed by two chords) is unambiguous music-theory terminology.
        // Without this boost, "voice leading C to F" embedded close to
        // skill.transpose because both involve moving between chords.
        (new Regex(@"\bvoice[\s-]*lead\w*\b|\bsmooth(?:est)?\s+voic\w*\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "skill.voiceleading"),

        // Capo — added 2026-05-14 to close BACKLOG dealbreaker #3. Anchored on
        // the literal "capo" token (music-unambiguous — no English homonyms
        // that overlap meaningfully) plus a fret number or the "shape" keyword.
        // Without this boost, "song in E with capo 4" embedded close to
        // transpose ("transpose down 4 semitones") and the score gap was
        // tight enough that intent flicked between routes across runs. The
        // capo token is a stable discriminator.
        (new Regex(@"\bcapo\b\s*(?:on\s+|at\s+|fret\s+)?\d{1,2}\b|\bcapo\s+(?:on\s+|fret\s+|at\s+)?\d{1,2}\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "skill.capo"),

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
