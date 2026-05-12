namespace GaApi.Tests;

using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override IEnumerable<Assembly> GetTestAssemblies() =>
        [typeof(TestWebApplicationFactory).Assembly];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(TestPaths.RepositoryPath("Apps", "ga-server", "GaApi"));

        // Force the Ollama IChatClient path during tests. appsettings.Development.json
        // pins AI:ChatProvider=claude, which makes AddLlmServices eagerly construct
        // an AnthropicProvider IChatClient at boot (intentional per PR #151 — fail
        // fast on misconfig). CI has no ANTHROPIC_API_KEY, so every WebApplicationFactory
        // test in this assembly blew up at host build with InvalidOperationException
        // out of AnthropicProvider.CreateChatClient. None of the GaApi.Tests fixtures
        // actually exercise the chat endpoint — they cover GraphQL, REST, and DI
        // shape. The Ollama branch registers a lazy adapter that never probes the
        // network unless something resolves IChatService, so this is safe even
        // without an Ollama instance running.
        builder.UseSetting("AI:ChatProvider", "ollama");

        base.ConfigureWebHost(builder);
    }

    /// <summary>
    /// Forces eager host construction so a startup failure surfaces with its
    /// full inner-exception chain inlined into the rethrown exception's message.
    /// Call from <c>[OneTimeSetUp]</c> in any test class that uses this factory.
    ///
    /// WHY: <see cref="WebApplicationFactory{TEntryPoint}"/> builds the host
    /// lazily on first <see cref="WebApplicationFactory{TEntryPoint}.Server"/>
    /// or <see cref="WebApplicationFactory{TEntryPoint}.CreateClient()"/>
    /// access. When startup fails (DI resolution error, missing config,
    /// unreachable content root), the exception fires from inside whichever
    /// test method first touches the factory — and on CI the published TRX
    /// often shows empty Error Message / Stack Trace fields for every test
    /// in the class, making the failure look like a phantom mass-fail with
    /// no diagnostic.
    ///
    /// Calling this in <c>[OneTimeSetUp]</c> moves the failure to a single
    /// fixture-level error with a clean exception chain that even a lossy
    /// TRX serializer reports verbatim, so the next CI failure for these
    /// tests is immediately actionable.
    /// </summary>
    /// <example>
    /// <code>
    /// public class MyGraphQLTests
    /// {
    ///     private readonly TestWebApplicationFactory _factory = new();
    ///
    ///     [OneTimeSetUp]
    ///     public void OneTimeSetUp() => _factory.EnsureStarted();
    ///
    ///     [OneTimeTearDown]
    ///     public void OneTimeTearDown() => _factory.Dispose();
    /// }
    /// </code>
    /// </example>
    public void EnsureStarted()
    {
        try
        {
            // Server is a public property on WebApplicationFactory<T>; touching it
            // forces the underlying TestServer to build, which in turn builds and
            // starts the host. No-op on subsequent calls.
            _ = Server;
        }
        catch (Exception ex)
        {
            var chain = new StringBuilder("TestWebApplicationFactory.EnsureStarted failed during host startup. Inner exception chain:\n");
            for (var e = (Exception?)ex; e is not null; e = e.InnerException)
            {
                chain.Append("  -> ").Append(e.GetType().FullName).Append(": ").AppendLine(e.Message);
            }
            chain.Append("Full stack trace:\n").Append(ex);
            throw new InvalidOperationException(chain.ToString(), ex);
        }
    }
}
