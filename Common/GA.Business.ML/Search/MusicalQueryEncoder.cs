namespace GA.Business.ML.Search;

using Embeddings;
using Embeddings.Services;
using Rag.Models;

/// <summary>
///     Structured intent parsed from a natural-language query. Each field is optional —
///     the encoder degrades gracefully when pieces are missing (partition cosine of a
///     zero partition is zero and contributes zero to the weighted score).
/// </summary>
public sealed record StructuredQuery(
    string? ChordSymbol,
    int? RootPitchClass,
    int[]? PitchClasses,
    string? ModeName,
    IReadOnlyList<string>? Tags);

/// <summary>
///     Composes a 112-dim OPTK v4 compact query vector from a <see cref="StructuredQuery"/>.
///     Uses the *same* vector services the corpus-side <c>MusicalEmbeddingGenerator</c>
///     uses for STRUCTURE/MODAL/SYMBOLIC partitions. Query and corpus therefore live in the
///     identical semantic space — no alignment training required.
///
///     The encoder produces a vector already pre-scaled by sqrt(partition weight) and
///     L2-normalized, matching the on-disk convention so dot product = cosine similarity.
/// </summary>
public sealed class MusicalQueryEncoder(
    TheoryVectorService theory,
    ModalVectorService modal,
    SymbolicVectorService symbolic)
{
    public double[] Encode(StructuredQuery q)
    {
        var raw = new double[EmbeddingSchema.TotalDimension];

        // STRUCTURE — from pitch classes (ideally derived from a parsed chord)
        var pcs = q.PitchClasses ?? (q.ChordSymbol is { } cs ? ChordPitchClasses.TryParse(cs, out var root, out var arr) ? arr : null : null);
        var root2 = q.RootPitchClass ?? (q.ChordSymbol is { } cs2 && ChordPitchClasses.TryParse(cs2, out var r2, out _) ? r2 : null);
        if (pcs is { Length: > 0 })
        {
            var structure = theory.ComputeEmbedding(pitchClasses: pcs, rootPitchClass: root2);
            Array.Copy(structure, 0, raw, EmbeddingSchema.StructureOffset, structure.Length);
        }

        // MODAL — requires a minimal ChordVoicingRagDocument stub (PitchClasses + RootPitchClass are enough)
        if (pcs is { Length: > 0 })
        {
            var stubDoc = new ChordVoicingRagDocument
            {
                PitchClasses = pcs,
                RootPitchClass = root2,
                SearchableText = "",
                AnalysisEngine = nameof(MusicalQueryEncoder),
                AnalysisVersion = "optk-v4",
                Jobs = [],
                TuningId = "Standard",
                PitchClassSetId = "",
                MidiNotes = [],
                Diagram = "",
                YamlAnalysis = "",
                IntervalClassVector = "",
                PitchClassSet = "",
                SemanticTags = [],
                PossibleKeys = [],
            };
            var modalVec = modal.ComputeEmbedding(stubDoc);
            Array.Copy(modalVec, 0, raw, EmbeddingSchema.ModalOffset, modalVec.Length);
        }

        // SYMBOLIC — technique/style tags
        if (q.Tags is { Count: > 0 })
        {
            var symbolicVec = symbolic.ComputeEmbedding(q.Tags);
            Array.Copy(symbolicVec, 0, raw, EmbeddingSchema.SymbolicOffset, symbolicVec.Length);
        }

        // MORPHOLOGY (24 dim) and CONTEXT (12 dim) remain zero — a text query carries no
        // fretboard realization or temporal-motion information. Their cosine contribution
        // is therefore zero, which is the correct behavior.

        return ExtractCompactAndNormalize(raw);
    }

    /// <summary>
    ///     Extract the 112 search-relevant dims (STRUCTURE+MORPHOLOGY+CONTEXT+SYMBOLIC+MODAL),
    ///     per-partition L2-normalize, then apply sqrt(partition weight). Mirrors
    ///     <c>OptickIndexWriter.ExtractAndNormalize</c> (v4-pp semantics).
    ///     <para>
    ///         Dot product of encoder output against on-disk v4-pp vectors = weighted
    ///         partition cosine directly. No global renormalization.
    ///     </para>
    /// </summary>
    internal static double[] ExtractCompactAndNormalize(double[] raw)
    {
        var compact = new double[OptickIndexReader.Dimension];
        var cStart = 0;
        foreach (var p in EmbeddingSchema.SimilarityPartitions)
        {
            // Per-partition L2 norm over raw slice.
            var partitionSumSq = 0.0;
            for (var j = 0; j < p.Dim; j++)
            {
                var v = raw[p.Start + j];
                partitionSumSq += v * v;
            }

            if (partitionSumSq > double.Epsilon)
            {
                var norm = Math.Sqrt(partitionSumSq);
                for (var j = 0; j < p.Dim; j++)
                {
                    compact[cStart + j] = raw[p.Start + j] / norm * p.SqrtWeight;
                }
            }
            // else: zero slice stays zero
            cStart += p.Dim;
        }
        return compact;
    }
}

/// <summary>
///     Minimal chord-symbol → pitch-class-set resolver. Covers the common qualities used
///     in chatbot queries. Not a substitute for the full <c>ChordSymbolParser</c> — this is
///     a scoped, deterministic fallback for when the LLM extractor returns a bare symbol
///     string. Returns pitch classes relative to C=0, ordered ascending.
/// </summary>
public static class ChordPitchClasses
{
    // Quality → offsets from root (semitones within one octave).
    private static readonly Dictionary<string, int[]> Qualities = new(StringComparer.OrdinalIgnoreCase)
    {
        [""]          = [0, 4, 7],                    // major triad
        ["maj"]       = [0, 4, 7],
        ["m"]         = [0, 3, 7],                    // minor triad
        ["min"]       = [0, 3, 7],
        ["dim"]       = [0, 3, 6],
        ["aug"]       = [0, 4, 8],
        ["sus2"]      = [0, 2, 7],
        ["sus4"]      = [0, 5, 7],
        ["5"]         = [0, 7],                       // power chord
        ["7"]         = [0, 4, 7, 10],                // dominant 7
        ["maj7"]      = [0, 4, 7, 11],
        ["M7"]        = [0, 4, 7, 11],
        ["m7"]        = [0, 3, 7, 10],
        ["min7"]      = [0, 3, 7, 10],
        ["m7b5"]      = [0, 3, 6, 10],                // half-diminished
        ["dim7"]      = [0, 3, 6, 9],
        ["7sus4"]     = [0, 5, 7, 10],
        ["6"]         = [0, 4, 7, 9],
        ["m6"]        = [0, 3, 7, 9],
        ["9"]         = [0, 4, 7, 10, 2],
        ["maj9"]      = [0, 4, 7, 11, 2],
        ["m9"]        = [0, 3, 7, 10, 2],
        ["11"]        = [0, 4, 7, 10, 2, 5],
        ["13"]        = [0, 4, 7, 10, 2, 5, 9],
        ["add9"]      = [0, 4, 7, 2],

        // Extended / altered qualities — jazz and fusion vocabulary
        ["mmaj7"]     = [0, 3, 7, 11],                // minor-major 7
        ["minmaj7"]   = [0, 3, 7, 11],
        ["m(maj7)"]   = [0, 3, 7, 11],
        ["maj13"]     = [0, 4, 7, 11, 2, 5, 9],
        ["m11"]       = [0, 3, 7, 10, 2, 5],
        ["m13"]       = [0, 3, 7, 10, 2, 5, 9],
        ["9#11"]      = [0, 4, 7, 10, 2, 6],          // "Lydian dominant" flavor
        ["maj9#11"]   = [0, 4, 7, 11, 2, 6],
        ["13#11"]     = [0, 4, 7, 10, 2, 6, 9],
        ["7b9"]       = [0, 4, 7, 10, 1],
        ["7#9"]       = [0, 4, 7, 10, 3],             // "Hendrix chord" intervals
        ["7b5"]       = [0, 4, 6, 10],
        ["7#5"]       = [0, 4, 8, 10],
        ["aug7"]      = [0, 4, 8, 10],
        ["+7"]        = [0, 4, 8, 10],
        ["13b9"]      = [0, 4, 7, 10, 1, 5, 9],
        ["7alt"]      = [0, 4, 10, 1, 3, 6, 8],       // altered dominant: b9 #9 b5 b13
        ["69"]        = [0, 4, 7, 9, 2],
        ["6/9"]       = [0, 4, 7, 9, 2],              // conventional notation with slash
        ["sus4add9"]  = [0, 5, 7, 2],
        ["sus2add11"] = [0, 2, 5, 7],
    };

    private static readonly Dictionary<string, int> Roots = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C"] = 0, ["C#"] = 1, ["Db"] = 1, ["D"] = 2, ["D#"] = 3, ["Eb"] = 3,
        ["E"] = 4, ["F"] = 5, ["F#"] = 6, ["Gb"] = 6, ["G"] = 7, ["G#"] = 8,
        ["Ab"] = 8, ["A"] = 9, ["A#"] = 10, ["Bb"] = 10, ["B"] = 11, ["Cb"] = 11,
    };

    /// <summary>Canonical chord-quality suffixes the parser recognizes (after the root letter).</summary>
    public static IReadOnlyCollection<string> KnownQualities => Qualities.Keys;

    /// <summary>Canonical root notes (C, C#, Db, D, ... B, Cb) the parser recognizes.</summary>
    public static IReadOnlyCollection<string> KnownRoots => Roots.Keys;

    public static bool TryParse(string symbol, out int? root, out int[] pitchClasses)
    {
        root = null;
        pitchClasses = [];
        if (string.IsNullOrWhiteSpace(symbol)) return false;
        symbol = symbol.Trim();

        // Root: first 1–2 chars covering letter + optional accidental.
        var rootLen = symbol.Length >= 2 && (symbol[1] == '#' || symbol[1] == 'b') ? 2 : 1;
        if (rootLen > symbol.Length) return false;
        var rootStr = symbol[..rootLen];
        if (!Roots.TryGetValue(rootStr, out var r)) return false;

        var qualityStr = symbol[rootLen..];
        // Drop slash-BASS suffix (e.g. "Cmaj7/G" → bass G). But keep slash-in-quality
        // like "C6/9" where the token after the slash is a digit — that's an extension,
        // not a bass note.
        var slashIdx = qualityStr.IndexOf('/');
        if (slashIdx >= 0
            && slashIdx + 1 < qualityStr.Length
            && char.IsLetter(qualityStr[slashIdx + 1]))
        {
            qualityStr = qualityStr[..slashIdx];
        }

        if (!Qualities.TryGetValue(qualityStr, out var offsets))
        {
            // Reject unknown qualities — otherwise every english word starting with a letter
            // A-G (e.g. "and", "fade", "be") parses as a chord and every query carries false
            // musical structure into retrieval.
            return false;
        }

        root = r;
        pitchClasses = offsets.Select(o => (r + o) % 12).Distinct().OrderBy(x => x).ToArray();
        return true;
    }
}
