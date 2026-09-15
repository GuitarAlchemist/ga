namespace GaMcpServer.Tests;

using System.ComponentModel;
using System.Reflection;
using GaMcpServer.Tools;

/// <summary>
/// The ga_polychord description is what an agent reads before calling the tool: its example must be
/// what the tool returns.
/// </summary>
[TestFixture]
public sealed class PolychordDescriptionTests
{
    [OneTimeSetUp]
    public void RegisterClosures() => GA.Business.DSL.GaClosureBootstrap.init();

    [Test]
    public async Task GaPolychord_DescriptionExample_MatchesTheToolOutput()
    {
        var description = typeof(ChordAtonalTool)
            .GetMethod(nameof(ChordAtonalTool.GaPolychord))!
            .GetCustomAttribute<DescriptionAttribute>()!
            .Description;

        // D over C: C-E-G + D-F#-A, the C Lydian scale without its seventh
        var result = await ChordAtonalTool.GaPolychord("C", "D");

        Assert.That(result, Contains.Substring("{C, D, E, F#, G, A} (6 tones)"));
        Assert.That(result, Contains.Substring("Forte:      6-33"));
        Assert.That(description, Contains.Substring("D triad over C triad → {C,D,E,F#,G,A}"));
        Assert.That(description, Does.Not.Contain("B triad over C triad"));
    }
}
