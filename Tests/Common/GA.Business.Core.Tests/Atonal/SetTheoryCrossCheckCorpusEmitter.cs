namespace GA.Business.Core.Tests.Atonal;

using System.Text.Json;
using GA.Domain.Core.Theory.Atonal;

/// <summary>
/// Emits GA's set-theory invariants (prime form + interval-class vector) for
/// every set class to a JSON corpus, so IX's DuckDB set-theory UDFs
/// (<c>ix_icv</c>, <c>ix_prime_form</c>, <c>ix_forte_number</c>) can be
/// cross-checked against GA's INDEPENDENT C# engine.
/// </summary>
/// <remarks>
/// Two independent implementations (GA's C# <see cref="SetClass"/> engine and
/// IX's Rust <c>ix-bracelet</c>) agreeing on the same invariants is a far
/// stronger correctness signal than either self-checking. The interval-class
/// vector is convention-FREE (it is THE canonical invariant of a set class,
/// identical under any prime-form ordering), so <c>ix_icv</c> MUST equal GA's
/// ICV for every set class — any mismatch is a real bug in one engine. Prime
/// form and Forte label are convention-DEPENDENT (Rahn vs Forte 1973 can differ
/// for a handful of set classes — the exact gap PR #414's CanonicalForteCatalog
/// addressed), so those comparisons are informational.
///
/// Run via <c>Scripts/ix-ga-settheory-crosscheck.ps1</c> (which also runs the
/// DuckDB comparison). [Explicit] because it is a tooling emitter, not a unit
/// test of GA behaviour.
/// </remarks>
[TestFixture]
public class SetTheoryCrossCheckCorpusEmitter
{
    [Test]
    [Explicit("Emits the GA set-theory cross-check corpus for the IX DuckDB comparison.")]
    public void EmitCorpus()
    {
        // The 6 interval classes in canonical ic1..ic6 order — the same order
        // ix_icv emits its "<a,b,c,d,e,f>" string.
        var intervalClasses = Enumerable.Range(1, 6).Select(IntervalClass.FromValue).ToArray();

        var items = SetClass.Items
            // Skip the empty set (0) and the aggregate (12): trivial edges whose
            // ICV/forte handling differs across engines for uninteresting reasons.
            .Where(sc => sc.Cardinality.Value is >= 1 and <= 11)
            .Select(sc => new
            {
                primeForm   = sc.PrimeForm.Select(pc => pc.Value).OrderBy(v => v).ToArray(),
                gaIcv       = intervalClasses.Select(ic => sc.IntervalClassVector[ic]).ToArray(),
                cardinality = sc.Cardinality.Value,
            })
            .OrderBy(x => x.cardinality)
            .ThenBy(x => string.Join(",", x.primeForm))
            .ToList();

        var corpus = new
        {
            schemaVersion = "0.1",
            generatedAt   = DateTime.UtcNow.ToString("o"),
            gaEngine      = "GA.Domain.Core.SetClass",
            count         = items.Count,
            items,
        };

        var dir = ResolveOutDir();
        Directory.CreateDirectory(dir);
        var outPath = Path.Combine(dir, $"ga-settheory-{DateTime.UtcNow:yyyy-MM-dd}.json");
        File.WriteAllText(outPath, JsonSerializer.Serialize(corpus));

        TestContext.WriteLine($"GA set-theory cross-check corpus written: {outPath} ({items.Count} set classes)");
        Assert.That(items, Is.Not.Empty, "No set classes emitted — GA SetClass.Items is empty?");
    }

    private static string ResolveOutDir()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            if (Directory.Exists(Path.Combine(d.FullName, ".git")) ||
                File.Exists(Path.Combine(d.FullName, "AllProjects.slnx")))
            {
                return Path.Combine(d.FullName, "state", "quality", "ga-ix-settheory");
            }
            d = d.Parent;
        }
        return Path.Combine(AppContext.BaseDirectory, "ga-ix-settheory");
    }
}
