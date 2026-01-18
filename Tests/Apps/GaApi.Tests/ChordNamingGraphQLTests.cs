namespace GaApi.Tests
{
    using System.Text;
    using System.Text.Json;
    using Microsoft.AspNetCore.Mvc.Testing;

    public class ChordNamingGraphQLTests
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
            // For GraphQL, even validation errors are often returned with 200 and an errors[] payload.
            // However, in our pipeline certain exceptions may bubble up as 400/500. We still want to parse the body.
            await using var stream = await resp.Content.ReadAsStreamAsync();
            return await JsonDocument.ParseAsync(stream);
        }

        [Test]
        public async Task BestName_Returns_NonEmpty_String()
        {
            const string query = @"query($name:String!,$root:Int!,$intervals:[Int!]!){
  chordBestName(formulaName:$name, root:$root, intervals:$intervals)
}";

            var doc = await PostGraphQlAsync(query, new { name = "Demo", root = 0, intervals = new[] { 4, 7, 10 } });
            var data = doc.RootElement.GetProperty("data");
            var best = data.GetProperty("chordBestName").GetString();
            Assert.That(best, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task AllNames_Contains_At_Least_One_And_Includes_Best()
        {
            const string qBest = @"query($name:String!,$root:Int!,$intervals:[Int!]!){
  chordBestName(formulaName:$name, root:$root, intervals:$intervals)
}";
            const string qAll = @"query($name:String!,$root:Int!,$intervals:[Int!]!){
  chordAllNames(formulaName:$name, root:$root, intervals:$intervals)
}";

            var vars = new { name = "Demo", root = 0, intervals = new[] { 4, 7, 10 } };

            var bestDoc = await PostGraphQlAsync(qBest, vars);
            var best = bestDoc.RootElement.GetProperty("data").GetProperty("chordBestName").GetString();

            var allDoc = await PostGraphQlAsync(qAll, vars);
            var all = allDoc.RootElement.GetProperty("data").GetProperty("chordAllNames").EnumerateArray().Select(e => e.GetString()!).ToArray();

            Assert.That(all.Length, Is.GreaterThanOrEqualTo(1));
            Assert.That(all, Does.Contain(best));
        }

        [Test]
        public async Task Comprehensive_Returns_Primary_And_Alternates()
        {
            const string q = @"query($name:String!,$root:Int!,$intervals:[Int!]!){
  chordComprehensiveNames(formulaName:$name, root:$root, intervals:$intervals){ primary alternates }
}";

            var doc = await PostGraphQlAsync(q, new { name = "Demo", root = 0, intervals = new[] { 4, 7, 10 } });
            var obj = doc.RootElement.GetProperty("data").GetProperty("chordComprehensiveNames");
            var primary = obj.GetProperty("primary").GetString();
            var alternates = obj.GetProperty("alternates").EnumerateArray().Select(e => e.GetString()).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(primary, Is.Not.Null.And.Not.Empty);
                Assert.That(alternates, Is.Not.Null);
            });
        }

        [Test]
        public async Task Validation_Error_On_Empty_Intervals()
        {
            const string q = @"query($name:String!,$root:Int!,$intervals:[Int!]!){
  chordBestName(formulaName:$name, root:$root, intervals:$intervals)
}";

            // Use a raw client to assert that the server rejects the request (either via non-200 or GraphQL errors[])
            var factory = new WebApplicationFactory<Program>();
            var client = factory.CreateClient();
            var payload = new { query = q, variables = new { name = "Demo", root = 0, intervals = Array.Empty<int>() } };
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var resp = await client.PostAsync("/graphql", content);

            // If the server returns a failure status code, that's an acceptable validation outcome
            if (!resp.IsSuccessStatusCode)
            {
                Assert.Pass($"Got HTTP {(int)resp.StatusCode} for invalid input as expected.");
            }

            // Otherwise, expect a GraphQL errors array
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var hasErrors = doc.RootElement.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array && errs.GetArrayLength() > 0;
            Assert.That(hasErrors, Is.True, "Expected errors[] when intervals are empty");
        }

        [Test]
        public async Task Validation_Error_On_OutOfRange_Root()
        {
            const string q = @"query($name:String!,$root:Int!,$intervals:[Int!]!){
  chordBestName(formulaName:$name, root:$root, intervals:$intervals)
}";

            var doc = await PostGraphQlAsync(q, new { name = "Demo", root = 12, intervals = new[] { 4, 7, 10 } });
            var errors = doc.RootElement.GetProperty("errors").EnumerateArray();
            var msg = errors.First().GetProperty("message").GetString();
            Assert.That(msg, Does.Contain("root must be a pitch class in the range 0..11"));
        }

        [Test]
        public async Task Validation_Error_On_OutOfRange_Bass()
        {
            const string q = @"query($name:String!,$root:Int!,$intervals:[Int!]!,$bass:Int){
  chordBestName(formulaName:$name, root:$root, intervals:$intervals, bass:$bass)
}";

            var doc = await PostGraphQlAsync(q, new { name = "Demo", root = 0, intervals = new[] { 4, 7, 10 }, bass = 13 });
            var errors = doc.RootElement.GetProperty("errors").EnumerateArray();
            var msg = errors.First().GetProperty("message").GetString();
            Assert.That(msg, Does.Contain("bass must be a pitch class in the range 0..11"));
        }
    }
}
