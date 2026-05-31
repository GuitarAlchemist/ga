namespace GA.Business.ML.Search;

using System.Text.RegularExpressions;
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
    IReadOnlyList<string>? Tags)
{
    /// <summary>
    ///     Instrument filter parsed out of the query text ("bass" / "guitar" / "ukulele").
    ///     Null when the user didn't mention one. Applied as a post-retrieval filter on the
    ///     index; does NOT affect the encoded query vector.
    /// </summary>
    public string? Instrument { get; init; }
}

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
    SymbolicVectorService symbolic,
    RootVectorService rootService)
{
    public double[] Encode(StructuredQuery q)
    {
        var raw = new double[EmbeddingSchema.TotalDimension];

        // STRUCTURE — from pitch classes (ideally derived from a parsed chord)
        var pcs = q.PitchClasses ?? (q.ChordSymbol is { } cs ? ChordPitchClasses.TryParse(cs, out var root, out var arr) ? arr : null : null);
        var root2 = q.RootPitchClass ?? (q.ChordSymbol is { } cs2 && ChordPitchClasses.TryParse(cs2, out var r2, out _) ? r2 : null);
        if (pcs is { Length: > 0 })
        {
            // ICV not yet wired into the query path. The helper ComputeIcvString +
            // dual-format ParseIcv (this PR) are forward-ready; turning the wiring on
            // requires the corpus rebuild step described in
            // docs/plans/2026-05-12-icv-format-reconciliation-plan.md §2 — without it,
            // a real query-side ICV signal cosines against the still-misparsed corpus
            // ICV slice and skews top-K away from exact PC-set matches (see acceptance
            // criterion: OptickIntegrationTests.cs:118 — "score should rise, not fall").
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

        // ROOT — 12-dim one-hot (v1.8). Query carries root signal IF the chord symbol
        // specified one; otherwise zero. Low weight (0.05) in the weighted cosine, so
        // root match adds a small discriminating boost on top of set-class-level STRUCTURE.
        if (root2.HasValue)
        {
            var rootVec = rootService.ComputeEmbedding(root2);
            var rootPartition = EmbeddingSchema.Partitions.First(p => p.Name == "ROOT");
            Array.Copy(rootVec, 0, raw, rootPartition.Start, rootVec.Length);
        }

        // MORPHOLOGY (24 dim) and CONTEXT (12 dim) remain zero — a text query carries no
        // fretboard realization or temporal-motion information. Their cosine contribution
        // is therefore zero, which is the correct behavior.

        return ExtractCompactAndNormalize(raw);
    }

    /// <summary>
    ///     Computes the interval-class vector of a pitch-class set as a digit-per-position
    ///     string (e.g. <c>"012120"</c>) where position <c>i</c> is the count of interval
    ///     class <c>i+1</c> (IC1..IC6). For each unordered PC pair, the interval class is
    ///     <c>min(d, 12 - d)</c> where <c>d = |pc_a - pc_b| mod 12</c>. Counts are clamped
    ///     to a single digit (max 9) so the lossless digit-per-position form is preserved;
    ///     in practice voicings of ≤ 7 PCs cap at 6 pairs per IC. Mirrors the math behind
    ///     <c>IntervalClassVectorId.GetVector</c> while emitting the format
    ///     <c>TheoryVectorService.ParseIcv</c> reads alongside the corpus's bracket-space
    ///     form. Exposed <c>internal</c> for symmetry tests.
    /// </summary>
    internal static string ComputeIcvString(int[] pitchClasses)
    {
        var counts = new int[6];
        var distinct = pitchClasses.Distinct().ToArray();
        for (var i = 0; i < distinct.Length; i++)
        {
            for (var j = i + 1; j < distinct.Length; j++)
            {
                var diff = Math.Abs(distinct[i] - distinct[j]) % 12;
                var ic = Math.Min(diff, 12 - diff);
                if (ic is >= 1 and <= 6) counts[ic - 1]++;
            }
        }
        // Single-digit-per-position: clamp at 9 (caller-aligned with ParseIcv).
        return string.Concat(counts.Select(c => (char)('0' + Math.Min(c, 9))));
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
    // Chord-quality resolution is COMPOSITIONAL (see TryComputeChordOffsets): the parser
    // builds the interval set algorithmically from a base triad + an optional
    // sixth/seventh/extension spine + any sequence of alterations / additions / omissions,
    // rather than enumerating named qualities in a finite lookup table. This makes it
    // exhaustive over the combinatorial space — "7b13", "maj7#5", "13#9", "m11b5",
    // "7sus4b9", "C(add#11)" all resolve — instead of silently failing (and dropping the
    // query to a hallucinating LLM) whenever a symbol is missing from a hand-maintained list.
    private static readonly Dictionary<string, int> Roots = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C"] = 0, ["C#"] = 1, ["Db"] = 1, ["D"] = 2, ["D#"] = 3, ["Eb"] = 3,
        ["E"] = 4, ["F"] = 5, ["F#"] = 6, ["Gb"] = 6, ["G"] = 7, ["G#"] = 8,
        ["Ab"] = 8, ["A"] = 9, ["A#"] = 10, ["Bb"] = 10, ["B"] = 11, ["Cb"] = 11,
    };

    // Representative suffixes — ADVISORY ONLY (vocabulary hints). The parser is
    // compositional, so the accepted set is far larger than this list; never treat it as
    // the exhaustive accepted set.
    private static readonly string[] RepresentativeQualities =
    [
        "", "maj", "m", "dim", "aug", "sus2", "sus4", "5", "6", "m6", "69", "6/9",
        "7", "maj7", "m7", "m7b5", "dim7", "mmaj7", "7sus4",
        "9", "maj9", "m9", "11", "m11", "13", "maj13", "m13", "add9", "madd9", "add11",
        "7b5", "7#5", "7b9", "7#9", "7#11", "7b13", "7alt", "9#11", "13#9", "13b9",
        "maj7#5", "maj7b5", "maj9#11", "m7#5", "aug7", "+7",
    ];

    /// <summary>
    ///     Representative chord-quality suffixes (advisory; the compositional parser accepts
    ///     far more — any root + base triad + spine + alteration/addition sequence).
    /// </summary>
    public static IReadOnlyCollection<string> KnownQualities => RepresentativeQualities;

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

        // Parens are cosmetic: "C7(#9)", "Cm(maj7)", "C(add9)" mean the paren-less forms.
        qualityStr = qualityStr.Replace("(", "").Replace(")", "");

        if (!TryComputeChordOffsets(qualityStr, out var offsets)) return false;

        root = r;
        pitchClasses = offsets.Select(o => (r + o) % 12).Distinct().OrderBy(x => x).ToArray();
        return true;
    }

    /// <summary>
    ///     Compositional chord-quality → semitone-offsets (from the root). Assembles the
    ///     interval set from a base triad (major / minor / diminished / augmented /
    ///     suspended), an optional sixth or seventh/extension spine (6, 7, maj7, 9, 11, 13 —
    ///     the 7th quality implied by maj/M/△ vs dominant vs diminished), and any sequence of
    ///     alterations (b5 #5 b9 #9 #11 b13 …), additions (add2/4/6/9/11/13) and omissions
    ///     (no3/omit5). Returns <see langword="false"/> when the suffix leaves residue it
    ///     cannot explain, so a root letter followed by ordinary words ("Fade", "Bee") does
    ///     NOT parse as a chord and inject false structure into retrieval.
    /// </summary>
    internal static bool TryComputeChordOffsets(string quality, out int[] offsets)
    {
        offsets = [];
        var w = (quality ?? string.Empty).Trim();

        // ── normalize symbol variants (case matters: 'M7' = major7, 'm' = minor) ──
        w = Regex.Replace(w, "△|Δ", "maj");
        w = Regex.Replace(w, "ø|Ø", "m7b5");
        w = Regex.Replace(w, @"M(?=7|9|11|13|6)", "maj");        // M7 / M9 / M13 → major-7th family
        w = w.Replace("Maj", "maj").Replace("MAJ", "maj").Replace("Major", "maj").Replace("major", "maj");
        w = w.Replace("Min", "min").Replace("MIN", "min").Replace("minor", "min");
        w = w.Replace("M", "maj");                                // any lone uppercase M ⇒ major
        // The major-7th marker is now canonical, so folding to lower case leaves a residual
        // 'm' meaning unambiguously minor.
        w = w.ToLowerInvariant();
        w = w.Replace("°", "dim").Replace("o7", "dim7");

        if (w.Length == 0) { offsets = [0, 4, 7]; return true; }  // bare root → major triad
        if (w == "5") { offsets = [0, 7]; return true; }          // power chord

        // Altered dominant (7alt / alt) is a fixed 7-note sonority — 1 3 b7 b9 #9 #11 b13.
        // It carries BOTH b9 and #9, which a one-value-per-degree map cannot express, so
        // resolve it directly.
        if (Regex.IsMatch(w, @"^7?alt$")) { offsets = [0, 1, 3, 4, 6, 8, 10]; return true; }

        var set = new SortedSet<int> { 0 };
        var explicitDegrees = new Dictionary<int, int>();         // degree (2/4/6/9/11/13) → semitone
        var omit = new HashSet<int>();                            // chord degrees (3, 5) to drop
        var noThird = false;
        var fifth = 7;
        int? sixth = null;
        int? seventh = null;

        // additions: add2 add4 add6 add9 add11 add13, AND accidental-additions
        // add#11 / addb9 / add#9 / addb13 etc. The sign group is optional — must
        // run BEFORE the alteration pass below, or "add#11" leaves "add" as residue
        // (the alteration pass eats "#11") and the whole symbol fails to parse.
        foreach (Match m in Regex.Matches(w, @"add(b|#)?(2|4|6|9|11|13)"))
        {
            var sign = m.Groups[1].Value.Length > 0 ? m.Groups[1].Value[0] : '\0';
            var d = int.Parse(m.Groups[2].Value);
            explicitDegrees[d] = DegreeSemitone(d, sign);
        }
        w = Regex.Replace(w, @"add(b|#)?(2|4|6|9|11|13)", "");

        // alterations: b5 #5 b6 b9 #9 #11 b13 … (sign + degree)
        foreach (Match m in Regex.Matches(w, @"(b|#)(5|6|9|11|13)"))
        {
            var sign = m.Groups[1].Value[0];
            var deg = int.Parse(m.Groups[2].Value);
            if (deg == 5) fifth = sign == 'b' ? 6 : 8;
            else if (deg == 6) sixth = sign == 'b' ? 8 : 10;
            else explicitDegrees[deg] = DegreeSemitone(deg, sign);
        }
        w = Regex.Replace(w, @"(b|#)(5|6|9|11|13)", "");

        // omissions: no3 / no5 / omit5 … and extension omissions (no11 / no9 / omit13).
        // "13no11" — the standard 13-without-11 voicing — must resolve, not fall through.
        foreach (Match m in Regex.Matches(w, @"(omit|no)(2|3|4|5|6|9|11|13)")) omit.Add(int.Parse(m.Groups[2].Value));
        w = Regex.Replace(w, @"(omit|no)(2|3|4|5|6|9|11|13)", "");

        // ── base triad ──
        bool thirdMinor;
        if (w.Contains("sus2")) { noThird = true; explicitDegrees[2] = 2; w = w.Replace("sus2", ""); thirdMinor = false; }
        else if (w.Contains("sus4") || w.Contains("sus")) { noThird = true; explicitDegrees[4] = 5; w = Regex.Replace(w, "sus4|sus", ""); thirdMinor = false; }
        else if (w.Contains("dim")) { thirdMinor = true; fifth = 6; }
        else if (w.Contains("aug") || w.StartsWith("+")) { thirdMinor = false; fifth = 8; w = w.Replace("aug", "").Replace("+", ""); }
        else if (w.StartsWith("min")) { thirdMinor = true; }
        else if (w.StartsWith("m") && !w.StartsWith("maj")) { thirdMinor = true; }
        else thirdMinor = false;

        var majSeventh = w.Contains("maj");
        var dimBase = w.Contains("dim");
        w = w.Replace("maj", "").Replace("min", "").Replace("dim", "");
        if (w.StartsWith("m")) w = w[1..];                        // leading minor 'm'

        // ── sixth / seventh / extension spine ──
        if (Regex.IsMatch(w, @"6/9|69"))
        {
            sixth ??= 9;
            explicitDegrees[9] = explicitDegrees.GetValueOrDefault(9, 2);
            w = Regex.Replace(w, @"6/9|69", "");
        }

        var topExt = 0;
        if (w.Contains("13")) { topExt = 13; w = w.Replace("13", ""); }
        else if (w.Contains("11")) { topExt = 11; w = w.Replace("11", ""); }
        else if (w.Contains("9")) { topExt = 9; w = w.Replace("9", ""); }
        else if (w.Contains("7")) { topExt = 7; w = w.Replace("7", ""); }
        else if (w.Contains("6")) { sixth ??= 9; w = w.Replace("6", ""); }

        if (dimBase && topExt >= 7) seventh = 9;                  // dim7 → bb7
        else if (topExt >= 7) seventh = majSeventh ? 11 : 10;

        // residue check: anything left (besides separators) means it was not a chord symbol.
        var residue = Regex.Replace(w, @"[\s/()+\-]", "");
        if (residue.Length > 0) return false;

        // ── assemble ──
        if (!noThird && !omit.Contains(3)) set.Add(thirdMinor ? 3 : 4);
        if (!omit.Contains(5)) set.Add(fifth);
        if (sixth is { } s6 && !omit.Contains(6)) set.Add(s6);
        if (seventh is { } s7) set.Add(s7);
        if (topExt >= 9 && !omit.Contains(9)) set.Add(explicitDegrees.GetValueOrDefault(9, 2));
        if (topExt >= 11 && !omit.Contains(11)) set.Add(explicitDegrees.GetValueOrDefault(11, 5));
        if (topExt >= 13 && !omit.Contains(13)) set.Add(explicitDegrees.GetValueOrDefault(13, 9));
        foreach (var kv in explicitDegrees)                      // adds + altered degrees, regardless of spine
            if (!omit.Contains(kv.Key)) set.Add(kv.Value);

        offsets = set.Select(x => x % 12).Distinct().OrderBy(x => x).ToArray();
        return true;
    }

    // Natural (sign='\0') or altered semitone for a chord degree, reduced to one octave.
    private static int DegreeSemitone(int degree, char sign) => degree switch
    {
        2  => 2,
        4  => 5,
        6  => sign == 'b' ? 8 : sign == '#' ? 10 : 9,
        9  => sign == 'b' ? 1 : sign == '#' ? 3 : 2,
        11 => sign == '#' ? 6 : sign == 'b' ? 4 : 5,
        13 => sign == 'b' ? 8 : sign == '#' ? 10 : 9,
        _  => 0
    };
}
