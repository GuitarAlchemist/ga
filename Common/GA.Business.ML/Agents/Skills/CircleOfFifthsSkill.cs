namespace GA.Business.ML.Agents.Skills;

/// <summary>
/// Walks through the circle of fifths — key signatures, perfect-fifth
/// relationships, the order of sharps and flats, and the practical use for
/// navigation between keys. Catalog skill (see <see cref="CatalogSkillBase"/>):
/// body is loaded from <c>skills/circle-of-fifths/SKILL.md</c>, zero LLM calls.
/// </summary>
public sealed class CircleOfFifthsSkill(ILogger<CircleOfFifthsSkill> logger) : CatalogSkillBase(logger)
{
    public override string Name        => "CircleOfFifths";
    public override string Description =>
        "Explains the circle of fifths: key signatures, the order of sharps " +
        "(F C G D A E B) and flats (B E A D G C F), enharmonic equivalents at " +
        "the bottom, and practical use for modulation. Pure catalog lookup, no LLM call.";

    public override IReadOnlyList<string> ExamplePrompts =>
    [
        "Explain the circle of fifths",
        "How do key signatures work?",
        "What's the order of sharps in key signatures?",
        "Why do some keys have flats and others sharps?",
        "How many sharps does D major have?",
        "Walk me through the circle of fourths",
        // "Across the circle from [key]" / "[direction] around the circle"
        // pattern — was losing to KeyIdentificationSkill because the
        // tail "from D" read as identifying a key. Added 2026-05-12 to
        // close co-4 misroute. The "circle" word is the discriminator —
        // these examples make sure it dominates the embedding.
        "What key is across the circle from D?",
        "Across the circle from G major",
        "What's opposite C on the circle of fifths?",
        "Move three positions clockwise on the circle",
    ];

    protected override string FolderName => "circle-of-fifths";
    protected override string Fallback   =>
        "The circle of fifths arranges the twelve major keys so each clockwise step is a perfect fifth up. Each step adds one sharp (clockwise) or one flat (counter-clockwise). C major sits at the top with no sharps or flats.";
}
