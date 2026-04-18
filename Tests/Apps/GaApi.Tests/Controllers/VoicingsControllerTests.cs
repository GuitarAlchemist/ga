namespace GaApi.Tests.Controllers;

using GaApi.Controllers;
using GaApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

[TestFixture]
[Category("Unit")]
public class VoicingsControllerTests
{
    private sealed class StubKnowledge(IReadOnlyList<SemanticSearchResult> results) : ISemanticKnowledgeSource
    {
        public int ObservedLimit { get; private set; }
        public string? ObservedQuery { get; private set; }

        public Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
            string query, int limit, CancellationToken cancellationToken)
        {
            ObservedQuery = query;
            ObservedLimit = limit;
            return Task.FromResult(results);
        }
    }

    [Test]
    public async Task Retrieve_RejectsEmptyQuery()
    {
        var stub = new StubKnowledge([]);
        var controller = new VoicingsController(NullLogger<VoicingsController>.Instance, stub);

        var result = await controller.Retrieve(new VoicingRetrieveRequest("  ", null), CancellationToken.None);

        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        Assert.That(stub.ObservedQuery, Is.Null, "service should not be called on bad input");
    }

    [Test]
    public async Task Retrieve_ClampsLimitAndEnvelopesResults()
    {
        var stub = new StubKnowledge([
            new SemanticSearchResult("## Cmaj7\nDiagram: x-3-2-0-1-0", 0.91),
            new SemanticSearchResult("## Cmaj9\nDiagram: x-3-2-4-3-0", 0.83),
        ]);
        var controller = new VoicingsController(NullLogger<VoicingsController>.Instance, stub);

        var result = await controller.Retrieve(
            new VoicingRetrieveRequest("warm jazz major seventh", Limit: 999),
            CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);

        var response = ok!.Value as VoicingRetrieveResponse;
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.SchemaVersion, Is.EqualTo("v1"));
        Assert.That(response.TopK, Is.EqualTo(50), "limit should clamp to 50");
        Assert.That(response.Query, Is.EqualTo("warm jazz major seventh"));
        Assert.That(response.Results, Has.Count.EqualTo(2));
        Assert.That(response.Results[0].Rank, Is.EqualTo(0));
        Assert.That(response.Results[0].Score, Is.EqualTo(0.91));
        Assert.That(response.Results[0].Snippet, Does.Contain("Cmaj7"));
        Assert.That(response.LatencyMs, Is.GreaterThanOrEqualTo(0));
        Assert.That(stub.ObservedLimit, Is.EqualTo(50));
    }

    [Test]
    public async Task Retrieve_UsesDefaultLimitWhenUnspecified()
    {
        var stub = new StubKnowledge([]);
        var controller = new VoicingsController(NullLogger<VoicingsController>.Instance, stub);

        await controller.Retrieve(new VoicingRetrieveRequest("x", null), CancellationToken.None);

        Assert.That(stub.ObservedLimit, Is.EqualTo(10));
    }
}
