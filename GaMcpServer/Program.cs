using AgentGovernance;
using AgentGovernance.Audit;
using AgentGovernance.Extensions.ModelContextProtocol;
using AgentGovernance.Security;
using AllProjects.ServiceDefaults;
using GaMcpServer;
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

// Register the algedonic emitter so the governance audit handler can publish
// deny/violation events to the cross-repo VSM inbox at state/algedonic/inbox.jsonl.
builder.Services.AddSingleton<AlgedonicEmitter>();

// Register MCP server with tools + resources. Resources (e.g. ga://voicings/vocabulary)
// are read-only data the client auto-lists via resources/list — more efficient than
// forcing a tool-call round-trip for static vocabulary lookup.
//
// .WithGovernance(...) wraps the MCP server with Microsoft's Agent Governance Toolkit:
//   - Startup tool-definition scan (prompt poisoning, typosquatting, hidden instructions)
//   - Per-call YAML policy evaluation (Policies/governance.yaml)
//   - Response sanitization (system tags, *_KEY/SECRET patterns, exfil URL shapes)
// See docs/runbooks/gamcp-governance.md for operator notes (live-reload, upgrade checklist).
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly()
    .WithGovernance(options =>
    {
        options.PolicyPaths.Add(Path.Combine(AppContext.BaseDirectory, "Policies", "governance.yaml"));
        options.ServerName = "GaMcpServer";

        // The toolkit defaults RequireAuthenticatedAgentId = true, which breaks
        // stdio MCP (no auth principal). We are running an offline desktop server
        // talked to by a local Claude Code session — fall back to a stable DID
        // so rate-limit buckets and audit entries have something to key on.
        options.RequireAuthenticatedAgentId = false;
        options.DefaultAgentId = "did:mcp:ga-server";

        // Scan tool definitions at startup but don't fail the server if the
        // scanner flags something — we want the warnings in logs to triage,
        // not a wedged MCP transport. Tighten to true after the first clean run.
        options.ScanToolsOnStartup = true;
        options.FailOnUnsafeTools = false;

        // Response sanitization is on by default — keep it on so any tool
        // whose output accidentally embeds a <system> tag or credential
        // pattern is redacted before reaching the client.
        options.SanitizeResponses = true;

        // The YAML rule language only supports tool_name / arg field equality;
        // substring matching for credential probes lives in the toolkit's
        // built-in detector. The default pattern set covers "ignore all
        // previous instructions", role-play hijacks, delimiter attacks, and
        // SQL injection — we extend it with a Blocklist for the credential /
        // env-var patterns called out in the task brief so they are denied
        // at the same severity (Critical) as the built-in jailbreak rules.
        options.EnablePromptInjectionDetection = true;
        options.PromptInjectionConfig = new DetectionConfig
        {
            Sensitivity = "balanced",
            Blocklist =
            {
                "api_key",
                "credential",
                "${env:",
                "$env:"
            }
        };
    });

var app = builder.Build();

// Hook the toolkit's audit emitter so we project denies/violations into the
// algedonic channel. Resolution is best-effort: if the kernel/emitter aren't
// available we log once and continue — the MCP server must come up either way.
try
{
    var kernel = app.Services.GetService<GovernanceKernel>();
    var algedonic = app.Services.GetRequiredService<AlgedonicEmitter>();
    var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("GaMcpServer.Governance");

    if (kernel is not null)
    {
        // PolicyViolation + ToolCallBlocked are the actionable ones (deny path).
        // PolicyCheck fires on every evaluation and would flood the inbox; skip it.
        kernel.OnEvent(GovernanceEventType.ToolCallBlocked, algedonic.Emit);
        kernel.OnEvent(GovernanceEventType.PolicyViolation, algedonic.Emit);
        startupLogger.LogInformation(
            "Algedonic emitter wired to governance audit (inbox: {Inbox})",
            algedonic.InboxPath);
    }
    else
    {
        startupLogger.LogWarning(
            "GovernanceKernel not registered in DI — algedonic projection inactive (preview API quirk).");
    }
}
catch (Exception ex)
{
    // Don't fail startup if the audit wiring throws — the server still runs,
    // we just lose the algedonic projection. The toolkit's built-in audit log
    // and metrics remain active.
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("GaMcpServer.Governance");
    logger.LogError(ex, "Failed to wire algedonic emitter to governance audit");
}

await app.RunAsync();
