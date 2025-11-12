using AllProjects.ServiceDefaults;
using GA.Business.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Console.SetOut(new StreamWriter(Stream.Null) { AutoFlush = true });

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

// Register web integration services from shared library
builder.Services.AddWebIntegrationServices();

// Register MCP server with tools
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
