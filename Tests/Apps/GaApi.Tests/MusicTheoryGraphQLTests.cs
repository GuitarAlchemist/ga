namespace GaApi.Tests;

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

public class MusicTheoryGraphQLTests
{
    private readonly WebApplicationFactory<Program> _factory = new();
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private record GraphQlRequest(string Query, object? Variables = null, string? OperationName = null);

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
    public async Task GetKey_Returns_Data()
    {
        const string query = """
                             query {
                               key(name: "C Major") {
                                 keyMode
                                 accidentalKind
                                 root {
                                    naturalNote {
                                        pitchClass {
                                            value
                                        }
                                    }
                                 }
                                 pitchClassSet {
                                    value
                                 }
                               }
                             }
                             """;

        var doc = await PostGraphQlAsync(query);
        var root = doc.RootElement;
        
        if (root.TryGetProperty("errors", out var errors))
        {
            Assert.Fail($"GraphQL returned errors: {errors}");
        }

        var data = root.GetProperty("data").GetProperty("key");
        Assert.That(data.GetProperty("keyMode").GetString(), Is.EqualTo("MAJOR"));
    }

    [Test]
    public async Task GetNote_Returns_Data()
    {
        const string query = """
                             query {
                               note(name: "C#") {
                                 pitchClass {
                                    value
                                 }
                                 naturalNote {
                                    value
                                 }
                               }
                             }
                             """;

        var doc = await PostGraphQlAsync(query);
        var root = doc.RootElement;

        if (root.TryGetProperty("errors", out var errors))
        {
            Assert.Fail($"GraphQL returned errors: {errors}");
        }

        var data = root.GetProperty("data").GetProperty("note");
        Assert.That(data.GetProperty("pitchClass").GetProperty("value").GetInt32(), Is.EqualTo(1));
    }
}
