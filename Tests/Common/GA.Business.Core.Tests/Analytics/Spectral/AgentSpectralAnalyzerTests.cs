namespace GA.Business.Core.Tests.Analytics.Spectral;

using Business.Analytics.Analytics.Spectral;
using Microsoft.Extensions.Logging.Abstractions;

[TestFixture]
public class AgentSpectralAnalyzerTests
{
    [Test]
    public void Analyze_PathGraph_ComputesExpectedConnectivity()
    {
        var analyzer = new AgentSpectralAnalyzer(NullLogger<AgentSpectralAnalyzer>.Instance);
        var graph = new AgentInteractionGraph
        {
            Agents =
            [
                new() { Id = "a" },
                new() { Id = "b" },
                new() { Id = "c" }
            ],
            Edges =
            [
                new() { Source = "a", Target = "b", Weight = 1.0 },
                new() { Source = "b", Target = "c", Weight = 1.0 }
            ]
        };

        var metrics = analyzer.Analyze(graph);

        Assert.That(metrics.Eigenvalues.Length, Is.EqualTo(3));
        Assert.That(metrics.AlgebraicConnectivity, Is.GreaterThan(0));
        Assert.That(metrics.SpectralRadius, Is.GreaterThan(0));
        Assert.That(metrics.DegreeDistribution.Length, Is.EqualTo(3));
        Assert.That(metrics.Centrality.Count, Is.EqualTo(3));
        Assert.That(metrics.Centrality.Values.Sum() - 1.0, Is.LessThan(1e-6));
    }
}
