namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Explains how to make a chord progression sound <b>darker</b> (or brighter) —
/// zero LLM calls, returns a curated set of harmonic-mood techniques.
/// </summary>
/// <remarks>
/// Triggers on queries like "how do I make this progression sound darker",
/// "make it sadder", "moodier chords", etc. The skill returns techniques
/// (parallel minor, modal interchange from Phrygian / Aeolian / Dorian,
/// borrowed bVII / bVI / bIII / iv) rather than computing a transformation
/// on a specific progression — that level of analysis would need the LLM
/// path. The deterministic answer is grounded music-theory pedagogy. When the
/// request names a key ("in D minor"), the examples are spelled in that key and
/// a darken request also gets a concrete progression in it.
/// </remarks>
[GuitarAlchemist.Registry.GaSkill("ProgressionMood", "progression")]
public sealed class ProgressionMoodSkill : IOrchestratorSkill
{
    public string Name        => "ProgressionMood";
    public string Description =>
        "Explains how to darken or brighten a chord progression via parallel-minor " +
        "swaps, modal interchange (Phrygian / Aeolian / Dorian), and borrowed bVII / " +
        "bVI / bIII / iv chords. Educational, no LLM call.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "How do I make this progression sound darker?",
        "Make this progression sound moodier",
        "How can I make my chords sound sadder?",
        "What can I do to make a song sound more melancholy?",
        "How to add a darker feel to a chord progression",
        "Techniques to make a major progression minor-sounding",
        "Make my song sound brighter",
        "How to make a progression more uplifting",
        // "Brighten up [thing] tune" idiom — was losing to ChordInfoSkill
        // because "minor key" pulled the embedding toward chord territory.
        // The verb "brighten up" + "tune" anchors mood. Added 2026-05-12
        // to close pm-4 misroute.
        "Brighten up a minor key tune",
        "Brighten this minor song",
        "How do I lift the mood of a minor progression?",
        // "[mode] flavor brighten/darken [thing]" pattern — was losing to
        // GenreEssentialsSkill because "rock progressions" pulled the
        // embedding toward genre territory and outscored mood. The
        // discriminator is the "flavor/color/feel" + brighten/darken
        // verb combo: those words signal mood transformation, not
        // genre vocabulary. Added 2026-05-12 to close adv-4 misroute
        // (PR #188 follow-up).
        "How does Mixolydian flavor brighten rock progressions?",
        "Use Lydian color to brighten a major progression",
        "Phrygian flavor to darken a progression",
        "What mode adds the most brightness to a major key tune?",
    ];

    public bool CanHandle(string message) => false; // semantic-routing only

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var lower = message.ToLowerInvariant();
        var brighten = lower.Contains("brighter") || lower.Contains("brighten") || lower.Contains("uplift") ||
                       lower.Contains("happier");
        var key = RequestedKey.TryParse(message);

        return Task.FromResult(brighten
            ? BrightenAnswer(key)
            : DarkenAnswer(key));
    }

    private static AgentResponse DarkenAnswer(RequestedKey? key)
    {
        // Without a requested key the techniques are illustrated in C major, as before.
        var t = key?.Tonic ?? Tonic.C;
        var (i, iv, v) = (t.Spell(1, 0), t.Spell(4, 5), t.Spell(5, 7));
        var (flatII, flatVI, flatVII) = (t.Spell(2, 1), t.Spell(6, 8), t.Spell(7, 10));

        var sb = new StringBuilder();
        List<string> evidence = [];
        if (key is not null)
        {
            var (progression, romans) = AppendConcreteProgression(sb, key);
            evidence.Add($"Requested key: {key.Name}");
            evidence.Add($"Progression in {key.Name}: {progression} ({romans})");
            sb.AppendLine();
        }

        var where = key is null ? "" : $" in {key.Name}";
        sb.AppendLine($"Here are five reliable ways to darken a progression{where}. Pick one or stack them — each shifts the harmonic mood without abandoning the underlying key.");
        sb.AppendLine();
        sb.AppendLine($"1. **Swap to parallel minor** — replace the tonic's major chord with its parallel minor ({i} → {i}m). Pulls everything that follows toward minor-mode resolution.");
        sb.AppendLine($"2. **Borrow from Aeolian (natural minor)** — substitute IV → iv, vi → bVI, V → v. So `{i} {iv} {v}` → `{i} {iv}m {v}` or `{i} {flatVI} {v}`. Standard pop-ballad move.");
        sb.AppendLine($"3. **Borrow from Phrygian** — drop in bII ({flatII} in {i} major) or use bII–I as a final resolution. The half-step approach gives a Spanish/cinematic colour.");
        sb.AppendLine("4. **Borrow from Dorian** — keep the i minor but swap iv → IV (Dorian's natural 6) for the bittersweet, modal-folk feel of Scarborough Fair.");
        sb.AppendLine($"5. **Replace V with bVII** — `{i} {flatVII} {iv}` instead of `{i} {v} {iv}`. Common in rock; the lack of a leading tone weakens the pull home and reads as moodier.");
        sb.AppendLine();
        sb.Append("Combine 1 + 2 for a strong sad-pop transformation; 3 alone for film-score gravity; 4 alone for folk melancholy.");

        evidence.AddRange(
        [
            "Technique 1: parallel minor (I → i)",
            "Technique 2: Aeolian modal interchange (IV → iv, vi → bVI, V → v)",
            "Technique 3: Phrygian modal interchange (bII)",
            "Technique 4: Dorian modal interchange (iv → IV)",
            "Technique 5: bVII substitution for V (drop the leading tone)",
        ]);

        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString(),
            Confidence = 1.0f,
            Evidence   = evidence,
            Assumptions = key is null
                ? ["Caller wants a general technique catalog, not a transformation of a specific named progression"]
                : [$"Key taken from the request: {key.Name}; techniques are spelled from its tonic"],
        };
    }

    /// <summary>Writes a concrete progression in the requested key and why it sounds sad.</summary>
    private static (string Progression, string Romans) AppendConcreteProgression(StringBuilder sb, RequestedKey key)
    {
        var t = key.Tonic;
        if (key.IsMinor)
        {
            // i – bVI – iv – V7, with V7 borrowing the leading tone from harmonic minor.
            var (i, flatIII, iv, v) = (t.Spell(1, 0), t.Spell(3, 3), t.Spell(4, 5), t.Spell(5, 7));
            var (flatVI, leadingTone) = (t.Spell(6, 8), t.Spell(7, 11));
            var progression = $"{i}m – {flatVI} – {iv}m – {v}7";
            const string romans = "i – bVI – iv – V7";
            sb.AppendLine($"**A sad progression in {key.Name}:** `{progression}` ({romans}).");
            sb.AppendLine();
            sb.AppendLine("Why it sounds sad:");
            sb.AppendLine($"- {i}m, the tonic, has a minor third ({flatIII}) over the root — the core of the minor-key colour.");
            sb.AppendLine($"- {flatVI} (bVI) puts the flat sixth in the bass; {flatVI} sits a half step above {v}, the fifth of the key, and sighs down onto it — the classic lament gesture.");
            sb.AppendLine($"- {iv}m (iv) keeps the subdominant minor, so even the chord that moves away from home stays dark.");
            sb.AppendLine($"- {v}7 (V7) borrows {leadingTone} from harmonic minor; that leading tone pulls hard back to {i}m, so every cycle lands on the minor tonic again.");
            return (progression, romans);
        }
        else
        {
            // I – vi – iv – I: the relative minor, then the subdominant borrowed from the parallel minor.
            var (i, vi, iv, v, flatVI) = (t.Spell(1, 0), t.Spell(6, 9), t.Spell(4, 5), t.Spell(5, 7), t.Spell(6, 8));
            var progression = $"{i} – {vi}m – {iv}m – {i}";
            const string romans = "I – vi – iv – I";
            sb.AppendLine($"**A sad progression in {key.Name}:** `{progression}` ({romans}).");
            sb.AppendLine();
            sb.AppendLine("Why it sounds sad:");
            sb.AppendLine($"- {vi}m (vi), the relative minor, turns the progression toward the key's darkest diatonic chord.");
            sb.AppendLine($"- {iv}m (iv) is borrowed from {i} minor; its {flatVI} (the flat sixth) leans a half step down onto {v}, the fifth, so the major key is darkened from the inside.");
            sb.AppendLine($"- Returning to {i} after {iv}m (the minor plagal cadence) resolves softly, with a wistful rather than triumphant arrival.");
            return (progression, romans);
        }
    }

    private static AgentResponse BrightenAnswer(RequestedKey? key)
    {
        // Without a requested key the parallel-major swap is illustrated from A minor, as before.
        var tonic = (key?.Tonic ?? Tonic.A).Spell(1, 0);
        var where = key is null ? "" : $" in {key.Name}";

        var sb = new StringBuilder();
        sb.AppendLine($"Here are four reliable ways to brighten a progression{where} that feels too dark or static.");
        sb.AppendLine();
        sb.AppendLine($"1. **Swap to parallel major** — flip a minor tonic to its parallel major ({tonic}m → {tonic}). Strongest mood-flip available.");
        sb.AppendLine("2. **Borrow from Lydian** — raise IV → #IV (#iv° actually), or hold a IV with a #11 colour. Floating, dreamy lift.");
        sb.AppendLine("3. **Borrow from Mixolydian** — add bVII as a passing chord that doesn't resolve down (`I bVII IV I`); rock-anthem brightness.");
        sb.AppendLine("4. **Reinforce V → I** — make sure the dominant resolves cleanly back to tonic. Add a V7 or V/V to strengthen pull.");
        sb.AppendLine();
        sb.Append("Combine 1 + 4 for a definitive lift from minor to major; 2 alone for ethereal/cinematic brightness.");

        List<string> evidence = key is null ? [] : [$"Requested key: {key.Name}"];
        evidence.AddRange(
        [
            "Technique 1: parallel major (i → I)",
            "Technique 2: Lydian modal interchange (#IV / IV#11)",
            "Technique 3: Mixolydian bVII passing chord",
            "Technique 4: strengthen V → I cadence",
        ]);

        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString(),
            Confidence = 1.0f,
            Evidence   = evidence,
            Assumptions = key is null
                ? ["Caller wants a general technique catalog, not a transformation of a specific named progression"]
                : [$"Key taken from the request: {key.Name}"],
        };
    }

    /// <summary>A key named in the request, e.g. "in D minor", "in the key of F#", "in Ebm", "in B flat".</summary>
    private sealed record RequestedKey(Tonic Tonic, bool IsMinor)
    {
        // The introducing words match in any case ("In D minor, ..."), but the letter must be upper-case so
        // "in a song" is not read as A major.
        private static readonly Regex KeyMention = new(
            @"\b(?i:in|key\s+of)\s+(?i:the\s+key\s+of\s+)?(?<letter>[A-G])(?:(?<accidental>[#♯b♭])|[\s-]+(?<accidental>(?i:flat|sharp))\b)?" +
            @"(?:\s*(?<mode>(?i:major|minor|maj|min))\b|(?<mode>m)\b|(?=[\s,.;:!?)]|$))",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public string Name => $"{Tonic.Spell(1, 0)} {(IsMinor ? "minor" : "major")}";

        public static RequestedKey? TryParse(string message)
        {
            var match = KeyMention.Match(message);
            if (!match.Success)
            {
                return null;
            }

            var accidental = match.Groups["accidental"].Value switch
            {
                "#" or "♯" => 1,
                "b" or "♭" => -1,
                var word when word.Equals("sharp", StringComparison.OrdinalIgnoreCase) => 1,
                var word when word.Equals("flat", StringComparison.OrdinalIgnoreCase) => -1,
                _ => 0,
            };
            var mode = match.Groups["mode"].Value.ToLowerInvariant();
            return new(new(match.Groups["letter"].Value[0], accidental), mode is "minor" or "min" or "m");
        }
    }

    /// <summary>
    ///     A tonic letter plus accidental that spells scale degrees letter-first: degree <c>n</c> always uses the
    ///     <c>n</c>-th letter above the tonic, so bVI of D is Bb (not A#) and bII of C is Db.
    /// </summary>
    private sealed record Tonic(char Letter, int Accidental)
    {
        private const string Letters = "CDEFGAB";
        private static readonly int[] NaturalPitchClasses = [0, 2, 4, 5, 7, 9, 11];

        public static Tonic C { get; } = new('C', 0);
        public static Tonic A { get; } = new('A', 0);

        /// <summary>Spells the note <paramref name="semitones" /> above the tonic on scale degree <paramref name="degree" />.</summary>
        public string Spell(int degree, int semitones)
        {
            var tonicIndex = Letters.IndexOf(Letter);
            var letterIndex = (tonicIndex + degree - 1) % 7;
            var target = (NaturalPitchClasses[tonicIndex] + Accidental + semitones + 24) % 12;
            var offset = (target - NaturalPitchClasses[letterIndex] + 18) % 12 - 6;
            var accidental = offset switch
            {
                -2 => "bb",
                -1 => "b",
                1 => "#",
                2 => "##",
                _ => "",
            };
            return $"{Letters[letterIndex]}{accidental}";
        }
    }
}
