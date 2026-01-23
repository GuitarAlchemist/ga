namespace GaApi.Tests;

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

public class DomainSchemaGraphQLTests
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
    public async Task GetDomainSchema_Returns_Relationships()
    {
        const string query = """
                             query {
                               domainSchema {
                                 name
                                 fullName
                                 relationships {
                                   targetTypeName
                                   relationshipType
                                 }
                                 invariants {
                                    description
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

        var data = root.GetProperty("data").GetProperty("domainSchema");
        Assert.That(data.ValueKind, Is.EqualTo(JsonValueKind.Array));

        // Find relevant type (Note)
        var types = data.EnumerateArray().ToList();
        var noteType = types.FirstOrDefault(t => t.GetProperty("name").GetString() == "Note");
        
        Assert.That(noteType.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined), "Should find 'Note' type in schema");
        
        var relationships = noteType.GetProperty("relationships");
        Assert.That(relationships.GetArrayLength(), Is.GreaterThan(0));
        
        var firstRel = relationships[0];
        Assert.That(firstRel.GetProperty("targetTypeName").GetString(), Is.EqualTo("PitchClass"));
        Assert.That(firstRel.GetProperty("relationshipType").GetString(), Is.EqualTo("IsParentOf"));
    }
}
