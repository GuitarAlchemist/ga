namespace GA.Business.Core.Tests.Config;

using GA.Business.Config;
using GA.Domain.Core.Theory.Tonal.Scales;

/// <summary>
///     The YAML catalogs that ScaleTool and the GraphQL music hierarchy read name the tritone scale where
///     <see cref="Scale.Tritone" /> puts it: C Db E Gb G Bb, binary scale ID 1235.
/// </summary>
[TestFixture]
public class TritoneScaleCatalogTests
{
    [Test]
    public void ScalesYaml_Tritone_HasThePitchClassesOfScaleTritone() =>
        Assert.That(ScalesConfig.TryGetScaleByName("Tritone").Value.BinaryScaleId,
            Is.EqualTo(Scale.Tritone.PitchClassSet.Id.Value));

    [Test]
    public void ExtendedScalesYaml_NamesTheTritoneScaleAtItsId() =>
        Assert.That(ExtendedScalesConfig.TryGetByBinaryId(Scale.Tritone.PitchClassSet.Id.Value).Value.WellKnownName?.Value,
            Is.EqualTo("Tritone"));
}
