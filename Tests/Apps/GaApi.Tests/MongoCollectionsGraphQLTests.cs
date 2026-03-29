namespace GaApi.Tests;

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

[Category("Integration")]
public class MongoCollectionsGraphQLTests
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly WebApplicationFactory<Program> _factory = new();

    private async Task<JsonDocument> PostGraphQlAsync(string query, object? variables = null)
    {
        var client = _factory.CreateClient();
        var payload = new GraphQlRequest(query, variables);
        var json = JsonSerializer.Serialize(payload, JsonOpts);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await client.PostAsync("/graphql", content);
        await using var stream = await resp.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    [Test]
    public async Task GetChords_Returns_Data()
    {
        const string query = """
                             query {
                               chords(first: 5) {
                                 nodes {
                                   name
                                   quality
                                 }
                                 totalCount
                                 pageInfo {
                                    hasNextPage
                                 }
                               }
                             }
                             """;

        var doc = await PostGraphQlAsync(query);
        var root = doc.RootElement;

        // check for errors
        if (root.TryGetProperty("errors", out var errors))
        {
            Assert.Fail($"GraphQL returned errors: {errors}");
        }

        var data = root.GetProperty("data").GetProperty("chords");
        var nodes = data.GetProperty("nodes");
        Assert.That(nodes.ValueKind, Is.EqualTo(JsonValueKind.Array));

        // We might not have data in the test DB, but the query should be valid
        // and return a structure.
        var totalCount = data.GetProperty("totalCount").GetInt32();
        Assert.That(totalCount, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public async Task GetVoicings_Returns_Data()
    {
        const string query = """
                             query {
                               voicings(first: 5) {
                                 nodes {
                                   id
                                   diagram
                                   chordName
                                 }
                                 totalCount
                               }
                             }
                             """;

        var doc = await PostGraphQlAsync(query);
        var root = doc.RootElement;

        // check for errors
        if (root.TryGetProperty("errors", out var errors))
        {
            Assert.Fail($"GraphQL returned errors: {errors}");
        }

        var data = root.GetProperty("data").GetProperty("voicings");
        var nodes = data.GetProperty("nodes");
        Assert.That(nodes.ValueKind, Is.EqualTo(JsonValueKind.Array));
    }

    private record GraphQlRequest(string Query, object? Variables = null, string? OperationName = null);
}
