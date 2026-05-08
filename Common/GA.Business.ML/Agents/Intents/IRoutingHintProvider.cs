namespace GA.Business.ML.Agents.Intents;

/// <summary>
/// Deterministic routing hint provider. Returns score deltas keyed by
/// <see cref="IIntent.Id"/> that <see cref="SemanticIntentRouter"/> applies
/// AFTER cosine scoring and BEFORE sorting, so high-precision surface
/// patterns (chord-symbol literals, "fret span", "what key is", etc.)
/// can break ties when adjacent intent centroids overlap.
/// </summary>
/// <remarks>
/// <para>
/// Pure function of the normalized query string. No LLM call, no embedding
/// call, no learned weights, no corpus lookup, no per-request state. Codex
/// CLI 2026-05-08 design call (P1 #6 follow-up #2) — chosen over
/// <see cref="IIntent"/>-bound boost-token bags (which invite each skill to
/// add ambiguous broad words like "key" / "scale" / "chord") and over
/// per-skill <see cref="IIntent.ExamplePrompts"/> curation (which can't
/// fix overlap when adjacent centroids are intrinsically close).
/// </para>
/// <para>
/// Implementations should keep deltas small (codex-recommended band
/// <c>+0.05</c> to <c>+0.08</c>; default impl uses <c>+0.06</c>). A
/// magnitude that would override legitimate semantic wins is the wrong
/// shape — promote the rule to a hard pre-route in
/// <c>ProductionOrchestrator.TrySelectDeterministicAgent</c> instead.
/// </para>
/// </remarks>
public interface IRoutingHintProvider
{
    /// <summary>
    /// Returns intent-id → boost-score deltas for the given query.
    /// Empty dictionary means no hints fired. Boosts are added — never
    /// subtracted; the router shouldn't depend on negative boosts as a
    /// signal.
    /// </summary>
    IReadOnlyDictionary<string, float> GetDeltas(string query);
}
