namespace GA.Business.Core.Tests.Config;

using Business.Config;
using GA.Domain.Services;
using GA.Domain.Services.Tonal.Cadences;

/// <summary>
///     The C# config loaders used to keep their own path-probe lists. They now resolve through
///     <see cref="ConfigFileLocator" />. These tests pin the behaviour-preservation claim: every former
///     probe is still a locator candidate, in the same relative order, and each loader still falls back
///     to its built-in default when the YAML is missing.
/// </summary>
[TestFixture]
public class ConfigFileLocatorTests
{
    private const string BaseDir = @"C:\app\bin\Debug\net10.0";
    private const string CurDir = @"C:\work";

    private static IEnumerable<TestCaseData> LegacyProbeLists()
    {
        // ChordProgressions, GuitarTechniques, SpecializedTunings and IconicChords shared this list.
        foreach (var name in new[]
                 {
                     "ChordProgressions.yaml", "GuitarTechniques.yaml", "SpecializedTunings.yaml",
                     "IconicChords.yaml"
                 })
        {
            yield return new TestCaseData(name, new[]
            {
                Path.Combine(BaseDir, name),
                Path.Combine(CurDir, name),
                Path.Combine(CurDir, "Common", "GA.Business.Config", name),
                Path.Combine(BaseDir, "..", "..", "..", "..", "Common", "GA.Business.Config", name)
            }).SetName($"LegacyProbes_StillProbedInOrder({name})");
        }

        // CadenceCatalog.FindConfigFile.
        const string cadences = "Cadences.yaml";
        yield return new TestCaseData(cadences, new[]
        {
            Path.Combine(BaseDir, cadences),
            Path.Combine(BaseDir, "Configuration", cadences),
            Path.Combine(BaseDir, "..", "..", "..", "..", "..", "Common", "GA.Business.Config", cadences),
            Path.Combine(BaseDir, "..", "..", "..", "Common", "GA.Business.Config", cadences)
        }).SetName($"LegacyProbes_StillProbedInOrder({cadences})");
    }

    [TestCaseSource(nameof(LegacyProbeLists))]
    public void LegacyProbes_StillProbedInOrder(string name, string[] legacyProbes)
    {
        var candidates = ConfigFileLocator.candidatePathsFrom(BaseDir, CurDir, name).ToList();

        var positions = legacyProbes.Select(probe => candidates.IndexOf(probe)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(positions, Has.None.EqualTo(-1), "every former probe is still a candidate");
            Assert.That(positions, Is.Ordered.Ascending, "former probes keep their relative precedence");
        });
    }

    [Test]
    public void ModalEmbeddingLegacyProbes_AreAllStillCandidates()
    {
        // ModalSchemaService probed base, base/5-up/Common/GA.Business.Config, then the cwd-relative name.
        // The locator probes the cwd before the source tree, so the last two swap; they only disagree when
        // two different copies of ModalEmbedding.yaml exist, which no build or deployment produces.
        const string name = "ModalEmbedding.yaml";
        var candidates = ConfigFileLocator.candidatePathsFrom(BaseDir, CurDir, name).ToList();

        Assert.That(candidates, Is.SupersetOf(new[]
        {
            Path.Combine(BaseDir, name),
            Path.Combine(BaseDir, "..", "..", "..", "..", "..", "Common", "GA.Business.Config", name),
            Path.Combine(CurDir, name)
        }));
    }

    [Test]
    public void FindFileFrom_PrefersBaseDirectoryOverWorkingDirectory()
    {
        var root = Directory.CreateTempSubdirectory("ga-locator-");
        try
        {
            var baseDir = root.CreateSubdirectory("base").FullName;
            var curDir = root.CreateSubdirectory("cur").FullName;
            const string name = "LocatorPrecedenceProbe.yaml";
            File.WriteAllText(Path.Combine(curDir, name), "cur");

            var onlyInCur = ConfigFileLocator.findFileFrom(baseDir, curDir, name);
            File.WriteAllText(Path.Combine(baseDir, name), "base");
            var inBoth = ConfigFileLocator.findFileFrom(baseDir, curDir, name);
            var absent = ConfigFileLocator.findFileFrom(baseDir, curDir, "LocatorAbsentProbe.yaml");

            Assert.Multiple(() =>
            {
                Assert.That(onlyInCur?.Value, Is.EqualTo(Path.Combine(curDir, name)));
                Assert.That(inBoth?.Value, Is.EqualTo(Path.Combine(baseDir, name)));
                Assert.That(absent, Is.Null);
            });
        }
        finally
        {
            root.Delete(true);
        }
    }

    [TestCase("ChordProgressions.yaml")]
    [TestCase("GuitarTechniques.yaml")]
    [TestCase("SpecializedTunings.yaml")]
    [TestCase("IconicChords.yaml")]
    [TestCase("Cadences.yaml")]
    public void FindFile_ResolvesCopiedYamlInTheOutputDirectory(string name) =>
        Assert.That(ConfigFileLocator.findFile(name)?.Value,
            Is.EqualTo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name)));

    [Test]
    public void FindFile_ResolvesModalEmbeddingFromTheRepositoryRoot()
    {
        // ModalEmbedding.yaml is not copied to the output directory; TestEnvironment sets the working directory
        // to the repository root, so the locator resolves it through the cwd/Common/GA.Business.Config probe.
        var path = ConfigFileLocator.findFile("ModalEmbedding.yaml")?.Value;

        Assert.That(path, Is.Not.Null);
        Assert.That(File.Exists(path), Is.True);
    }

    [Test]
    public void Loaders_ReadTheYamlThroughTheLocator() =>
        Assert.Multiple(() =>
        {
            Assert.That(ChordProgressionsConfigLoader.LoadConfiguration(Find("ChordProgressions.yaml"))
                .ChordProgressions, Has.Count.GreaterThan(1));
            Assert.That(GuitarTechniquesConfigLoader.LoadConfiguration(Find("GuitarTechniques.yaml"))
                .GuitarTechniques, Has.Count.GreaterThan(1));
            Assert.That(SpecializedTuningsConfigLoader.LoadConfiguration(Find("SpecializedTunings.yaml"))
                .AlternativeStringConfigurations.Select(t => t.Name), Does.Contain("Nashville Tuning"));
            Assert.That(IconicChordsConfigLoader.LoadConfiguration(Find("IconicChords.yaml"))
                .IconicChords, Has.Count.GreaterThan(1));
            Assert.That(CadenceCatalog.LoadCadences(Find("Cadences.yaml")), Has.Count.GreaterThan(1));
        });

    [Test]
    public void Loaders_FallBackToBuiltInDefaults_WhenYamlIsMissing()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"ga-missing-{Guid.NewGuid():N}.yaml");

        Assert.Multiple(() =>
        {
            Assert.That(ChordProgressionsConfigLoader.LoadConfiguration(missing).ChordProgressions
                .Select(p => p.Name), Is.EqualTo(new[] { "I–V–vi–IV" }));
            Assert.That(GuitarTechniquesConfigLoader.LoadConfiguration(missing).GuitarTechniques
                .Select(t => t.Name), Is.EqualTo(new[] { "Alternate Picking" }));
            Assert.That(SpecializedTuningsConfigLoader.LoadConfiguration(missing).AlternativeStringConfigurations
                .Select(t => t.Name), Is.EqualTo(new[] { "Drop D (Guitar)" }));
            Assert.That(IconicChordsConfigLoader.LoadConfiguration(missing).IconicChords
                .Select(c => c.Name), Is.EqualTo(new[] { "C Major Triad" }));
            Assert.That(CadenceCatalog.LoadCadences(missing), Is.Empty);
            Assert.That(ChordProgressionsConfigLoader.LoadConfiguration(null).ChordProgressions, Has.Count.EqualTo(1));
        });
    }

    private static string Find(string name) =>
        ConfigFileLocator.findFile(name)?.Value ?? throw new FileNotFoundException(name);
}
