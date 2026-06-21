namespace GA.Business.ML.Agents.Skills;

using System.Text;

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
/// path. The deterministic answer is grounded music-theory pedagogy.
/// </remarks>
public sealed class ProgressionMoodSkill(ILogger<ProgressionMoodSkill> logger) : IOrchestratorSkill
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
        var brighten = lower.Contains("brighter") || lower.Contains("uplift") || lower.Contains("happier");

        return Task.FromResult(brighten
            ? BrightenAnswer()
            : DarkenAnswer());
    }

    private static AgentResponse DarkenAnswer()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Here are five reliable ways to darken a progression. Pick one or stack them — each shifts the harmonic mood without abandoning the underlying key.");
        sb.AppendLine();
        sb.AppendLine("1. **Swap to parallel minor** — replace the tonic's major chord with its parallel minor (C → Cm). Pulls everything that follows toward minor-mode resolution.");
        sb.AppendLine("2. **Borrow from Aeolian (natural minor)** — substitute IV → iv, vi → bVI, V → v. So `C F G` → `C Fm G` or `C bA G`. Standard pop-ballad move.");
        sb.AppendLine("3. **Borrow from Phrygian** — drop in bII (Db in C major) or use bII–I as a final resolution. The half-step approach gives a Spanish/cinematic colour.");
        sb.AppendLine("4. **Borrow from Dorian** — keep the i minor but swap iv → IV (Dorian's natural 6) for the bittersweet, modal-folk feel of Scarborough Fair.");
        sb.AppendLine("5. **Replace V with bVII** — `C bB F` instead of `C G F`. Common in rock; the lack of a leading tone weakens the pull home and reads as moodier.");
        sb.AppendLine();
        sb.Append("Combine 1 + 2 for a strong sad-pop transformation; 3 alone for film-score gravity; 4 alone for folk melancholy.");

        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString(),
            Confidence = 1.0f,
            Evidence   =
            [
                "Technique 1: parallel minor (I → i)",
                "Technique 2: Aeolian modal interchange (IV → iv, vi → bVI, V → v)",
                "Technique 3: Phrygian modal interchange (bII)",
                "Technique 4: Dorian modal interchange (iv → IV)",
                "Technique 5: bVII substitution for V (drop the leading tone)",
            ],
            Assumptions = ["Caller wants a general technique catalog, not a transformation of a specific named progression"],
        };
    }

    private static AgentResponse BrightenAnswer()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Here are four reliable ways to brighten a progression that feels too dark or static.");
        sb.AppendLine();
        sb.AppendLine("1. **Swap to parallel major** — flip a minor tonic to its parallel major (Am → A). Strongest mood-flip available.");
        sb.AppendLine("2. **Borrow from Lydian** — raise IV → #IV (#iv° actually), or hold a IV with a #11 colour. Floating, dreamy lift.");
        sb.AppendLine("3. **Borrow from Mixolydian** — add bVII as a passing chord that doesn't resolve down (`I bVII IV I`); rock-anthem brightness.");
        sb.AppendLine("4. **Reinforce V → I** — make sure the dominant resolves cleanly back to tonic. Add a V7 or V/V to strengthen pull.");
        sb.AppendLine();
        sb.Append("Combine 1 + 4 for a definitive lift from minor to major; 2 alone for ethereal/cinematic brightness.");

        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString(),
            Confidence = 1.0f,
            Evidence   =
            [
                "Technique 1: parallel major (i → I)",
                "Technique 2: Lydian modal interchange (#IV / IV#11)",
                "Technique 3: Mixolydian bVII passing chord",
                "Technique 4: strengthen V → I cadence",
            ],
            Assumptions = ["Caller wants a general technique catalog, not a transformation of a specific named progression"],
        };
    }
}
