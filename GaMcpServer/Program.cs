using AllProjects.ServiceDefaults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Console.SetOut(new StreamWriter(Stream.Null) { AutoFlush = true });

// Force F# closure module initializers to run before MCP tools are registered.
// F# do-bindings are lazy — without this, GaClosureRegistry.Global is empty.
GA.Business.DSL.GaClosureBootstrap.init();

var builder = Host.CreateApplicationBuilder(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
// Note: This is optional for MCP servers but provides better observability
try
{
    builder.AddServiceDefaults();
}
catch
{
    // If Aspire is not available (running standalone), continue without it
}

// Add configuration from appsettings.json
builder.Configuration.AddJsonFile("appsettings.json", true, true);

builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Register HTTP client for GaApi chatbot tool
builder.Services.AddHttpClient("gaapi", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["GaApi:BaseUrl"] ?? "https://localhost:7001";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

// Register MCP server with tools + resources. Resources (e.g. ga://voicings/vocabulary)
// are read-only data the client auto-lists via resources/list — more efficient than
// forcing a tool-call round-trip for static vocabulary lookup.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly();

await builder.Build().RunAsync();
