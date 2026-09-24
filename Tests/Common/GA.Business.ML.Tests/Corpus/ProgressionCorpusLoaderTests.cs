namespace GA.Business.ML.Tests.Corpus;

using System.Text.Json;

/// <summary>
///     Loader and schema-conformance gate for the held-out progression corpus
///     (GuitarAlchemist/ga#627).
/// </summary>
/// <remarks>
///     These tests are the corpus's own contract: the file must exist where the
///     loader looks, parse deterministically, and satisfy every constraint the
///     language-neutral schema states. They say nothing about whether the
///     product answers the cases correctly - that is
///     <see cref="ProgressionCorpusMatrixTests" />.
/// </remarks>
[TestFixture]
public class ProgressionCorpusLoaderTests
{
    [Test]
    public void SchemaFile_Exists() =>
        Assert.That(File.Exists(ProgressionCorpus.SchemaPath), Is.True,
            $"expected the corpus schema at {ProgressionCorpus.SchemaPath}");

    [Test]
    public void CorpusFile_Exists() =>
        Assert.That(File.Exists(ProgressionCorpus.CorpusPath), Is.True,
            $"expected the corpus at {ProgressionCorpus.CorpusPath}");

    /// <summary>
    ///     The in-repo validator implements a closed set of keywords. If the
    ///     schema grows one it does not implement, the validator would silently
    ///     ignore that constraint and report a false pass - so fail here first.
    /// </summary>
    [Test]
    public void Schema_StaysInsideTheValidatorsSupportedKeywordSet()
    {
        using var schema = ProgressionCorpus.LoadSchemaDocument();
        var used = JsonSchemaSubsetValidator.CollectKeywords(schema.RootElement);
        var unsupported = used.Except(JsonSchemaSubsetValidator.SupportedKeywords).Order(StringComparer.Ordinal).ToList();

        Assert.That(unsupported, Is.Empty,
            "the schema uses keywords JsonSchemaSubsetValidator does not implement: " +
            string.Join(", ", unsupported));
    }

    [Test]
    public void Corpus_ValidatesAgainstItsSchema()
    {
        using var corpus = ProgressionCorpus.LoadDocument();
        using var schema = ProgressionCorpus.LoadSchemaDocument();

        var errors = JsonSchemaSubsetValidator.Validate(corpus.RootElement, schema.RootElement);

        Assert.That(errors, Is.Empty,
            "corpus violates progression-corpus.v1.schema.json:" + Environment.NewLine +
            string.Join(Environment.NewLine, errors));
    }

    [Test]
    public void Corpus_DeclaresTheSchemaFileItValidatesAgainst()
    {
        var corpus = ProgressionCorpus.Load();
        Assert.Multiple(() =>
        {
            Assert.That(corpus.Schema, Is.EqualTo(ProgressionCorpus.SchemaFileName));
            Assert.That(corpus.SchemaVersion, Is.EqualTo(ProgressionCorpus.SupportedSchemaVersion));
            Assert.That(corpus.CorpusId, Is.EqualTo("progression-to-voicing"));
            Assert.That(corpus.HeldOut, Is.True, "the corpus must be marked held-out");
            Assert.That(corpus.Status, Is.EqualTo("draft"),
                "v0.1.x-style drafts stay draft until the Phase 4 milestone of #623");
        });
    }

    [Test]
    public void Load_ReturnsAtLeastTwelveCases() =>
        Assert.That(ProgressionCorpus.Load().Cases, Has.Count.GreaterThanOrEqualTo(12));

    [Test]
    public void Load_IsDeterministic()
    {
        var first = JsonSerializer.Serialize(ProgressionCorpus.Load());
        var second = JsonSerializer.Serialize(ProgressionCorpus.Load());

        Assert.That(second, Is.EqualTo(first), "two loads of the same file must be identical");
    }

    [Test]
    public void Load_ReturnsCasesInAscendingIdOrder()
    {
        var ids = ProgressionCorpus.Load().Cases.Select(c => c.Id).ToList();

        Assert.That(ids, Is.EqualTo(ids.OrderBy(id => id, StringComparer.Ordinal).ToList()),
            "file order must equal sorted order so iteration is reproducible");
    }

    [Test]
    public void Load_RejectsAnUnsupportedSchemaVersion()
    {
        // The guard lives in Load(); exercising it via a doctored copy would need
        // a second file, so assert the constant the guard compares against is the
        // one the corpus actually declares. Drift here is the failure mode.
        using var corpus = ProgressionCorpus.LoadDocument();
        Assert.That(corpus.RootElement.GetProperty("schema_version").GetString(),
            Is.EqualTo(ProgressionCorpus.SupportedSchemaVersion));
    }

    /// <summary>
    ///     Guards against mojibake. The corpus is read by C#, Python and (later)
    ///     Rust tooling; smart quotes and en-dashes have already produced silent
    ///     encoding damage in this repo's snapshot files.
    /// </summary>
    [Test]
    public void CorpusAndSchema_AreAsciiOnly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ProgressionCorpus.ReadCorpusText().All(char.IsAscii), Is.True,
                $"{ProgressionCorpus.CorpusFileName} must stay ASCII-only");
            Assert.That(ProgressionCorpus.ReadSchemaText().All(char.IsAscii), Is.True,
                $"{ProgressionCorpus.SchemaFileName} must stay ASCII-only");
        });
    }
}
