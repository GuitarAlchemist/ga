// GaDomainInvariants — exports GA's finite domain universes to JSONL so a DuckDB
// invariant sweep can check structural laws exhaustively (all 4096 pitch-class sets,
// the full Forte set-class catalog) rather than by sampling. See the companion
// build-invariants.sql. Born from the IntervalClassVector.Major base-12 bug: the same
// class of latent error is a one-line SQL check over the whole universe.

using System.Text.Json;
using GA.Domain.Core.Theory.Atonal;

var outDir = args.Length > 0 ? args[0] : "state/quality/domain-invariants";
Directory.CreateDirectory(outDir);

var jsonOpts = new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never };

// --- Pitch-class sets: the full 2^12 universe ---------------------------------------
var pcsCount = 0;
using (var w = new StreamWriter(Path.Combine(outDir, "pitchclasssets.jsonl")))
{
    foreach (var s in PitchClassSet.Items)
    {
        var icv = s.IntervalClassVector;
        var row = new
        {
            id            = s.Id.Value,
            cardinality   = (int)s.Cardinality,
            icv_id        = icv.Id.Value,
            icv           = icv.Vector.Values.ToArray(),   // [IC1..IC6] counts
            prime_form_id = s.PrimeForm?.Id.Value,
            is_prime_form = s.IsPrimeForm,
            complement_id = s.Id.Complement.Value,
        };
        w.WriteLine(JsonSerializer.Serialize(row, jsonOpts));
        pcsCount++;
    }
}

// --- Set classes: the Forte equivalence-class catalog -------------------------------
var scCount = 0;
using (var w = new StreamWriter(Path.Combine(outDir, "setclasses.jsonl")))
{
    foreach (var sc in SetClass.Items)
    {
        var icv = sc.IntervalClassVector;
        var row = new
        {
            label         = sc.ToString(),
            cardinality   = (int)sc.Cardinality,
            icv_id        = icv.Id.Value,
            icv           = icv.Vector.Values.ToArray(),
            prime_form_id = sc.PrimeForm.Id.Value,
        };
        w.WriteLine(JsonSerializer.Serialize(row, jsonOpts));
        scCount++;
    }
}

Console.WriteLine($"Exported {pcsCount} pitch-class sets + {scCount} set classes to {outDir}");
