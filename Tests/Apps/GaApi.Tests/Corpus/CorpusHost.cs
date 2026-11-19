namespace GaApi.Tests.Corpus;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

/// <summary>
///     The in-process GaApi host the prompt and session corpora run against. It starts
///     from <see cref="TestWebApplicationFactory"/> (Ollama chat provider, startup voicing
///     indexer off) and adds what the public deployment answers with: the routing embedder
///     and thresholds from <c>appsettings.Production.json</c>, and the OPTIC-K voicing index
///     at <c>state/voicings/optick.index</c> when the checkout has one (it is gitignored;
///     without it, voicing search is unavailable, as on a deploy without the index).
/// </summary>
internal static class CorpusHost
{
    public static WebApplicationFactory<Program> Create() =>
        new TestWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            var productionSettings = TestPaths.RepositoryPath("Apps", "ga-server", "GaApi", "appsettings.Production.json");
            builder.ConfigureAppConfiguration(config => config.AddJsonFile(productionSettings, optional: false));

            var opticIndex = TestPaths.RepositoryPath("state", "voicings", "optick.index");
            if (File.Exists(opticIndex))
                builder.UseSetting("VoicingSearch:OpticIndexPath", opticIndex);
        });
}
