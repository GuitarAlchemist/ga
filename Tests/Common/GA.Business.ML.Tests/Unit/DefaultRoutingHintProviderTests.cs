namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Intents;

/// <summary>
/// Pins the deterministic regex rules in
/// <see cref="DefaultRoutingHintProvider"/>. Each rule must fire on the
/// positive examples from the routing-eval corpus and abstain on the
/// most plausible adjacent false-positive prompts.
/// </summary>
/// <remarks>
/// Tests are organized by intent. Adding a new rule here also requires a
/// false-positive guard test — silent boost spread across unrelated
/// intents is the main risk identified in the 2026-05-08 capability matrix
/// (and re-confirmed by the 2026-05-11 baseline analysis where
/// genreessentials F1=0.00 turned out to mean "everything else stole its
/// prompts").
/// </remarks>
[TestFixture]
public class DefaultRoutingHintProviderTests
{
    private readonly DefaultRoutingHintProvider _hints = new();

    [TestCase("transpose this progression down a half step")]
    [TestCase("transpose C-Am-F-G to G major")]
    [TestCase("shift this progression up a whole step")]
    [TestCase("bring D minor down to A minor")]
    [TestCase("transposing my song to D")]
    [TestCase("can you transpose this for me")]
    public void Transpose_PositiveExamples_BoostTransposeIntent(string query)
    {
        var deltas = _hints.GetDeltas(query);
        Assert.That(deltas.ContainsKey("skill.transpose"), Is.True,
            $"Expected skill.transpose boost for: \"{query}\". Got: {string.Join(", ", deltas.Keys)}");
        Assert.That(deltas["skill.transpose"], Is.EqualTo(DefaultRoutingHintProvider.BoostMagnitude));
    }

    [TestCase("shift focus to ear training")]
    [TestCase("bring back the bridge section")]
    [TestCase("move on to the next song")]
    [TestCase("what does that chord bring to the song")]
    // PR #178 review (correctness MED-1) regression pin: the prior
    // music-noun-anchored regex fired on idiomatic non-transpose phrases
    // because `song`/`piece`/`tune` ARE music-context nouns but appear in
    // everyday English with no music-direction marker. The tightened
    // regex requires `up|down|to+[A-G]` as a suffix; these must NOT fire.
    [TestCase("bring this song to a close")]
    [TestCase("move this song higher in the playlist")]
    [TestCase("shift this tune to the front")]
    [TestCase("bring this piece to a conclusion")]
    public void Transpose_AdjacentNonMusicShift_DoesNotBoost(string query)
    {
        var deltas = _hints.GetDeltas(query);
        Assert.That(deltas.ContainsKey("skill.transpose"), Is.False,
            $"Transpose rule should NOT fire for: \"{query}\" — false-positive guard. " +
            $"Got deltas: {string.Join(", ", deltas.Select(kv => $"{kv.Key}={kv.Value}"))}");
    }

    // ─── Regression coverage for existing rules — pin against accidental
    //     drift while editing the rule list.
    [TestCase("tritone sub for Cmaj7",        "skill.chordsubstitution")]
    [TestCase("what key is this progression", "skill.keyidentification")]
    [TestCase("playability of this voicing",  "skill.fretspan")]
    [TestCase("what's a perfect fifth",       "skill.interval")]
    [TestCase("notes in C major scale",       "skill.scaleinfo")]
    // Pre-existing chordinfo regex requires \b after [A-G], which a chord
    // symbol like "Cmaj7" violates (C is followed by m, no boundary). The
    // chord-tone surface still fires via the "chord tones" / "notes in"
    // clauses below. Anchor the test on a known-firing input.
    [TestCase("chord tones of Cmaj7",         "skill.chordinfo")]
    public void ExistingRules_PinPositiveExamples(string query, string expectedIntentId)
    {
        var deltas = _hints.GetDeltas(query);
        Assert.That(deltas.ContainsKey(expectedIntentId), Is.True,
            $"Expected {expectedIntentId} boost for: \"{query}\". Got: {string.Join(", ", deltas.Keys)}");
    }

    // Mode-name hint — explicit pitch-class names of the seven diatonic
    // modes should boost skill.modes. Added 2026-05-12 to close mo-3 in
    // the 2026-05-11 routing-eval corpus. The trick was that the embedder
    // scored skill.scaleinfo slightly higher on "what notes are in G
    // Mixolydian" — the mode name IS the discriminator and only a
    // deterministic hint can guarantee it dominates.
    [TestCase("what notes are in G Mixolydian")]
    [TestCase("notes of D Dorian")]
    [TestCase("explain the Lydian mode")]
    [TestCase("what is Phrygian")]
    [TestCase("compare Ionian and Aeolian")]
    [TestCase("Locrian half-diminished")]
    public void ModeName_PositiveExamples_BoostModesIntent(string query)
    {
        var deltas = _hints.GetDeltas(query);
        Assert.That(deltas.ContainsKey("skill.modes"), Is.True,
            $"Expected skill.modes boost for: \"{query}\". Got: {string.Join(", ", deltas.Keys)}");
        Assert.That(deltas["skill.modes"], Is.EqualTo(DefaultRoutingHintProvider.BoostMagnitude));
    }

    // False-positive guards — these prompts should NOT boost skill.modes
    // because the mode-name substring appears inside a non-musical context
    // (a name, a place, a brand) or is just absent.
    [TestCase("what's a perfect fifth")]
    [TestCase("transpose this progression")]
    [TestCase("alternative chord for Cmaj7")]
    [TestCase("brighten up a minor key tune")]
    public void ModeName_FalsePositiveGuards_DoNotBoost(string query)
    {
        var deltas = _hints.GetDeltas(query);
        Assert.That(deltas.ContainsKey("skill.modes"), Is.False,
            $"Did NOT expect skill.modes boost for: \"{query}\". Got: {string.Join(", ", deltas.Keys)}");
    }

    [Test]
    public void EmptyQuery_ReturnsNoDeltas()
    {
        Assert.That(_hints.GetDeltas(""),    Is.Empty);
        Assert.That(_hints.GetDeltas("   "), Is.Empty);
    }

    [Test]
    public void BoostMagnitude_IsStable()
    {
        // Pin the magnitude so a change is a deliberate edit, not a
        // typo. Codex 2026-05-08 specifically blessed +0.06 — if this
        // changes, expect the routing baseline F1 to shift across many
        // intents at once and re-eval before merging.
        Assert.That(DefaultRoutingHintProvider.BoostMagnitude, Is.EqualTo(0.06f));
    }
}
