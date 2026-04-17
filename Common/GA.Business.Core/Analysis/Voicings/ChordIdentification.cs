namespace GA.Business.Core.Analysis.Voicings;

/// <summary>
///     Describes the identity of a chord matched against a voicing. The positional constructor
///     parameters are preserved for backward compatibility; <b>new consumers should read the
///     Phase C structured fields</b> (<see cref="CanonicalName" />, <see cref="SlashSuffix" />,
///     <see cref="Alterations" />, etc.) which separate register-invariant identity from
///     voicing-specific slash notation.
/// </summary>
/// <remarks>
///     Phase C of the chord-recognition refactor
///     (see <c>docs/plans/2026-04-17-chord-recognition-architecture-plan.md</c>) introduced the
///     structured fields to fix cross-instrument label inconsistency. The invariant identity
///     (<see cref="CanonicalName" />) is what flows into the OPTIC-K SYMBOLIC partition;
///     slash notation (<see cref="SlashSuffix" />) is a voicing-level property rendered only
///     for human display.
/// </remarks>
public record ChordIdentification(
    string ChordName,
    string RootPitchClass,
    string HarmonicFunction,
    bool IsNaturallyOccurring,
    string FunctionalDescription,
    string Quality,
    object? SlashChordInfo,
    object? ExtensionInfo,
    string? ClosestKey = null)
{
    /// <summary>Alternate name for the same chord (e.g. enharmonic respelling).</summary>
    public string? AlternateName { get; init; }

    // =====================================================================
    // Phase C — structured chord identity (register-invariant)
    // =====================================================================

    /// <summary>
    ///     Register-invariant chord identity (e.g. "C Major 7", "Dm7b5", "F#13b9").
    ///     Same pitch-class set → same CanonicalName on every instrument.
    ///     <b>Use this — not <see cref="ChordName" /> — when feeding the SYMBOLIC
    ///     embedding partition, clustering, or any comparison that should be
    ///     invariant to voicing register.</b>
    /// </summary>
    public string? CanonicalName { get; init; }

    /// <summary>
    ///     Voicing-specific bass-note suffix ("/E"). Null when bass equals root,
    ///     no bass hint was provided, or the chord has no well-defined root.
    ///     <b>Register-dependent — do not use for invariant comparisons.</b>
    /// </summary>
    public string? SlashSuffix { get; init; }

    /// <summary>
    ///     Extension label ("triad", "6th", "7th", "9th", "11th", "13th", "add").
    ///     Null when the chord is a dyad, unison, or unnamed set class.
    /// </summary>
    public string? Extension { get; init; }

    /// <summary>
    ///     Non-canonical alterations relative to the extension base. E.g.
    ///     <c>["#9"]</c> for a 7#9 chord, <c>["b5", "#5"]</c> for a 7b5#5 spread.
    /// </summary>
    public string[] Alterations { get; init; } = [];

    /// <summary>
    ///     Kebab-case pattern identifier from
    ///     <see cref="GA.Domain.Core.Theory.Harmony.CanonicalChordPatternCatalog" />
    ///     (e.g. "dominant-7-sharp-9"). Null when no canonical pattern matched
    ///     (Forte-number fallback in effect).
    /// </summary>
    public string? PatternName { get; init; }

    /// <summary>
    ///     Match quality indicator: sum of (missing + extra) intervals relative
    ///     to the chosen pattern. 0 = exact match. Positive = approximate match
    ///     (e.g. 1 = one interval dropped or added). -1 = no pattern matched,
    ///     Forte fallback used.
    /// </summary>
    public int? MatchDistance { get; init; }

    /// <summary>
    ///     Full human display name: <see cref="CanonicalName" /> + <see cref="SlashSuffix" />
    ///     when both are present; falls back to legacy <see cref="ChordName" /> otherwise.
    /// </summary>
    public string DisplayName =>
        CanonicalName is null
            ? ChordName
            : SlashSuffix is null
                ? CanonicalName
                : $"{CanonicalName}{SlashSuffix}";

    /// <summary>
    ///     True when the structured Phase C fields are populated (i.e. the
    ///     <see cref="CanonicalChordRecognizer" /> was used to produce this
    ///     identity). Existing consumers can branch on this to opt in.
    /// </summary>
    public bool HasCanonicalIdentity => CanonicalName is not null;
}
