namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;
using GA.Business.DSL.Parsers;
using GA.Business.DSL.Types;

/// <summary>
/// Domain-backed Grothendieck DSL parse skill. Accepts category-theory DSL
/// expressions and returns the parsed AST type plus a human-readable gloss.
/// Reuses the same F# parser
/// (<see cref="GrothendieckOperationsParser.parse"/>) that the
/// <c>/test/grothendieck-dsl</c> demo page calls.
/// Surface forms:
/// <list type="bullet">
/// <item><b>"Parse C ⊗ G"</b></item>
/// <item><b>"What does Transpose(C ⊗ G) mean"</b></item>
/// <item><b>"Parse Transpose ∘ Invert"</b></item>
/// <item><b>"Parse pullback(Cmaj7, Transpose, Gmaj7)"</b></item>
/// </list>
/// Zero LLM calls in the happy path — F# parser runs in-process and the
/// gloss is generated from the AST type name. Confidence = 1.0 on parse
/// success, 0.3 on parse failure (with the parser's own error message).
/// </summary>
/// <remarks>
/// Built 2026-05-14, stolen-from-demo per user request. References
/// <c>GA.Business.DSL</c> (already a transitive dep via Skills wiring).
/// </remarks>
public sealed class GrothendieckParseSkill(ILogger<GrothendieckParseSkill> logger) : IOrchestratorSkill
{
    public string Name => "GrothendieckParse";
    public string Description =>
        "Parses Grothendieck category-theory DSL expressions (tensor ⊗, " +
        "direct sum ⊕, product ×, coproduct +, exponential ^, functors, " +
        "limits/colimits, topos operators, sheaves) and returns the AST " +
        "category plus a natural-language gloss. Same parser as the " +
        "/test/grothendieck-dsl demo page; exposed conversationally.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "parse C ⊗ G",
        "what does Transpose(C ⊗ G) mean",
        "parse Transpose ∘ Invert",
        "parse pullback(Cmaj7, Transpose, Gmaj7)",
        "parse Cmaj7 + Gmaj7 + Fmaj7",
        "parse functor Transpose: Chords -> Chords",
        "what is Hom(Cmaj7, Gmaj7)",
        "explain power(Cmaj7)",
        "parse equalizer Transpose Invert",
        "parse Cmaj7 ⊕ Gmaj7",
    ];

    public bool CanHandle(string message) => false;  // semantic-routing only

    // Routing anchor: explicit "parse" / "what does X mean" with a
    // Grothendieck-flavored payload (category symbol, functor verb, limit
    // keyword) so we don't catch all "parse X" prompts.
    private static readonly Regex GrothendieckSurface =
        new(@"(⊗|⊕|×\s*[A-Z]|∘|η\(|\^[A-Z]|" +
            @"\b(?:functor|pullback|pushout|equalizer|coequalizer|coproduct|tensor[\s-]+product|direct[\s-]+sum|natural[\s-]+transformation|truth[_\s-]*value|subobject|power|hom)\b)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AnchorPattern =
        new(@"\b(?:parse|what\s+does|explain|interpret|meaning\s+of)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var msg = message ?? string.Empty;
        if (!AnchorPattern.IsMatch(msg) || !GrothendieckSurface.IsMatch(msg))
            return Task.FromResult(CannotHandle());

        // Extract the expression — heuristic: everything after the anchor token.
        // Users typically say "parse C ⊗ G" or "what does Transpose(C ⊗ G) mean".
        // We strip the anchor tokens and trim, then send to the parser.
        var expr = ExtractExpression(msg);
        if (string.IsNullOrWhiteSpace(expr))
            return Task.FromResult(CannotHandle());

        return Task.FromResult(ParseAndAnswer(expr));
    }

    private static string ExtractExpression(string msg)
    {
        // Cheap heuristic: drop leading anchor phrase, drop trailing "mean".
        var stripped = Regex.Replace(msg,
            @"^\s*(?:parse|what\s+does|explain|interpret|meaning\s+of)\s+",
            string.Empty,
            RegexOptions.IgnoreCase);
        stripped = Regex.Replace(stripped, @"\s+(?:mean|do)\s*\??$", string.Empty, RegexOptions.IgnoreCase);
        return stripped.Trim().Trim('?', '.');
    }

    private AgentResponse ParseAndAnswer(string expression)
    {
        try
        {
            var result = GrothendieckOperationsParser.parse(expression);

            if (result.IsOk)
            {
                var op = result.ResultValue;
                var category = DescribeOperationCategory(op);
                var gloss = GlossFromCategory(category, expression);

                var sb = new StringBuilder();
                sb.AppendLine($"**Parsed**: `{expression}`");
                sb.AppendLine();
                sb.AppendLine($"- **AST category**: `{category}`");
                sb.AppendLine($"- **Gloss**: {gloss}");
                sb.AppendLine();
                sb.AppendLine(
                    "This is the same parser the `/test/grothendieck-dsl` demo " +
                    "uses (`GA.Business.DSL.Parsers.GrothendieckOperationsParser`). " +
                    "Open the demo page to interact with the full AST tree visualisation.");

                return Result(sb.ToString(), $"grothendieck-parse(ok, {category})");
            }

            return new AgentResponse
            {
                AgentId    = AgentIds.Theory,
                Result     = $"Couldn't parse `{expression}` as a Grothendieck operation. Parser said: {result.ErrorValue}",
                Confidence = 0.3f,
                Evidence   = [$"Source: GrothendieckParseSkill (parse failure)", $"input: {expression}", $"error: {result.ErrorValue}"],
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Grothendieck parse threw: {Expression}", expression);
            return new AgentResponse
            {
                AgentId    = AgentIds.Theory,
                Result     = $"Parser raised an exception on `{expression}`: {ex.Message}",
                Confidence = 0.2f,
                Evidence   = [$"Source: GrothendieckParseSkill (parser exception)", $"input: {expression}"],
            };
        }
    }

    /// <summary>
    /// Read the discriminated-union case name off the parsed operation. We
    /// don't need to fully destructure every case — the category name carries
    /// the educational payload and the user can open the demo for full AST.
    /// </summary>
    private static string DescribeOperationCategory(GrammarTypes.GrothendieckOperation op)
    {
        // F# DU instances expose their case via Tag/GetType().Name.
        // Use the type-name approach since it's stable across F# versions.
        return op.GetType().Name.Replace("GrothendieckOperation+", "");
    }

    private static string GlossFromCategory(string category, string expression) => category switch
    {
        "TensorProduct" => "Tensor product (⊗) — combines two musical objects into a joint structure where every relation in one is paired with every relation in the other. In category theory, the universal bilinear map.",
        "DirectSum"     => "Direct sum (⊕) — disjoint union with both projection and inclusion maps. Lets you treat two chords as components of a larger system that you can decompose back into either piece.",
        "Product"       => "Categorical product (×) — the universal object with projection maps to each component. Stronger than direct sum: morphisms into the product correspond to tuples of morphisms into each factor.",
        "Coproduct"     => "Coproduct (+) — dual of product, with inclusion maps from each component. Morphisms out of the coproduct correspond to tuples of morphisms out of each factor.",
        "Exponential"   => "Exponential (^) — the internal hom object Y^X representing morphisms X → Y as an object in the category itself. Curry/uncurry lives here.",
        "DefineFunctor" => "Functor definition — declares a structure-preserving map between two categories (the source-category objects/morphisms get mapped to target-category objects/morphisms).",
        "ApplyFunctor"  => "Functor application — invokes a declared functor on a specific object. Same surface as calling a function but the functor preserves composition and identities.",
        "ComposeFunctors" => "Functor composition (∘) — chains two functors left-to-right (or right-to-left, depending on convention). Composition of functors is itself a functor.",
        "NaturalTransformation" => "Natural transformation (η) — a family of morphisms between two functors that commutes with their action on morphisms. The 'second-order' morphism of category theory.",
        "Pullback"      => "Pullback — the universal cone over a cospan. Roughly: the part of A and B that agrees under a shared map. In music: shared common structure between two chords via a transformation.",
        "Pushout"       => "Pushout — dual of pullback: the universal cocone under a span. The 'gluing' of A and B along their shared piece.",
        "Equalizer"     => "Equalizer — the universal subobject where two parallel morphisms agree. The pieces of an object where two transformations produce the same result.",
        "Coequalizer"   => "Coequalizer — dual of equalizer: the universal quotient where two parallel morphisms become equal. Identifies elements until two maps coincide.",
        "PowerObject"   => "Power object (P) — the object of all 'subobjects' of X, the topos analog of the power set. In musical context, all subsets of a chord/scale's pitch-class collection.",
        "SubobjectClassifier" => "Subobject classifier (Ω) — the universal object that classifies all subobjects via characteristic morphisms. The topos analog of {0,1}.",
        "TruthValue"    => "Truth value — the membership characteristic of an element with respect to a subobject. Boolean in classical logic; in a topos it ranges over Ω.",
        "HomFunctor"    => "Hom functor — Hom(A, B) is the set of morphisms from A to B (made into an object/sheaf when the category is closed). Tells you 'all the ways A can become B'.",
        "Restriction"   => "Restriction (|) — restricts a sheaf or presheaf to a smaller open set. In music: viewing a chord/progression through a narrower lens (a sub-scale, a single key center).",
        _               => $"Operation of category `{category}` parsed successfully. Hit the demo at /test/grothendieck-dsl for the full AST view of `{expression}`.",
    };

    private AgentResponse Result(string text, string evidence)
    {
        logger.LogDebug("GrothendieckParseSkill: {Evidence}", evidence);
        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = text,
            Confidence = 1.0f,
            Evidence   = ["Source: GrothendieckParseSkill (in-process F# GrothendieckOperationsParser)", evidence],
        };
    }

    private AgentResponse CannotHandle() => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = "Ask me to parse a Grothendieck-DSL expression, e.g. \"parse C ⊗ G\", \"what does Transpose(C ⊗ G) mean\", or \"parse pullback(Cmaj7, Transpose, Gmaj7)\".",
        Confidence = 0.1f,
        Evidence   = ["GrothendieckParseSkill: no parse anchor + Grothendieck surface in query"],
    };
}
