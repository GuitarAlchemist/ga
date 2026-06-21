// GaStructureInvariance (Tier-2 lens) — measures whether OPTIC-K's STRUCTURE partition is
// actually transposition-invariant, by running the REAL TheoryVectorService over every set
// class at all 12 transpositions and exporting the STRUCTURE vectors for a DuckDB cosine
// sweep. STRUCTURE is *claimed* O+P+T+I-invariant (TheoryVectorService remark, CLAUDE.md);
// this checks it empirically instead of trusting the claim.

using System.Text.Json;
using GA.Domain.Core.Theory.Atonal;
using GA.Business.ML.Embeddings.Services;

var outDir = args.Length > 0 ? args[0] : "state/quality/domain-invariants";
Directory.CreateDirectory(outDir);

var rows = 0;
using (var w = new StreamWriter(Path.Combine(outDir, "structure-transpositions.jsonl")))
{
    foreach (var sc in SetClass.Items)
    {
        if ((int)sc.Cardinality < 2) continue;   // 0/1-note sets have no interval content

        var primeForm = sc.PrimeForm;
        var basePcs = Enumerable.Range(0, 12).Where(i => (primeForm.Id.Value & (1 << i)) != 0).ToList();
        var icvStr = sc.IntervalClassVector.ToString();   // "<2 5 4 3 6 1>" — ParseIcv accepts this

        for (var t = 0; t < 12; t++)
        {
            var pcs = basePcs.Select(p => (p + t) % 12).ToList();
            var structure = TheoryVectorService.ComputeEmbedding(pcs, rootPitchClass: pcs.Min(), intervalClassVector: icvStr);
            w.WriteLine(JsonSerializer.Serialize(new
            {
                setclass      = sc.ToString(),
                cardinality   = (int)sc.Cardinality,
                transposition = t,
                structure,   // double[24] — STRUCTURE partition contents
            }));
            rows++;
        }
    }
}

Console.WriteLine($"Exported {rows} STRUCTURE vectors (set classes x 12 transpositions) to {outDir}");
