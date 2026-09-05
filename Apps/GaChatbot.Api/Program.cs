using GaChatbot.Api.Extensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

Environment.SetEnvironmentVariable(
    "GA_STATE_DIR",
    Path.Combine(AppContext.BaseDirectory, "state"));

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();
builder.Logging.AddDebug();

builder.Services.AddControllers();
// Data Protection backs the chat session cookie (HttpChatSessionCookie).
// Unlike GaApi — which gets IDataProtectionProvider transitively from
// AddAuthentication — this host registers no auth, so nothing else pulls
// the service in and GetOrIssue would fail at request time.
// SetApplicationName isolates the key ring from any other app sharing the
// keys directory, so a cookie minted elsewhere can never Unprotect here.
// Keys use the framework default location; if it isn't writable (some
// container images) they are ephemeral and sessions rotate on restart —
// acceptable for the anonymous demo, whose threat model already treats
// rotation as expected.
builder.Services.AddDataProtection().SetApplicationName("GaChatbot.Api");
builder.Services.AddProblemDetails();
builder.Services.AddHttpClient();
builder.Services.AddTransient(_ => new HttpClient());
builder.Services.AddMinimalChatbotApi(builder.Configuration);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ChatbotClient", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
}

var app = builder.Build();

// Trust X-Forwarded-* from the reverse proxy so Request.Scheme reflects the
// CLIENT-facing scheme. Mirrors Apps/ga-server/GaApi/Program.cs.
//
// Why this host needs it: the deployment terminates TLS at the cloudflared
// `ga-demos` tunnel and forwards to plain http://localhost:5252
// (docs/runbooks/chatbot-deploy.md). Without this, Request.IsHttps is false on
// EVERY production request, so HttpChatSessionCookie issues the session cookie
// without `Secure` on https://demos.guitaralchemist.com — the cookie that keys
// MemoryStore / ChatTranscriptStore partitions.
//
// Safety: forwarded headers are honoured only for requests that actually came
// through the tunnel (CF-Connecting-IP present) AND only when a public host is
// configured. Stray headers on direct/localhost requests are stripped, so a
// dev proxy emitting a partial set can't talk the host into an https view of
// itself — that would mint a Secure cookie the browser refuses to send back
// over http, silently rotating the session on every turn.
// Must run before UsePathBase so the whole pipeline sees the corrected scheme.
var publicHost = builder.Configuration["Proxy:PublicHost"];
app.Use(async (ctx, next) =>
{
    var isProxiedRequest = !string.IsNullOrEmpty(ctx.Request.Headers["CF-Connecting-IP"].ToString());
    if (isProxiedRequest && !string.IsNullOrWhiteSpace(publicHost))
    {
        // The tunnel sets X-Forwarded-Proto but not X-Forwarded-Host; synthesise
        // it so the forwarded host matches the configured public origin.
        ctx.Request.Headers["X-Forwarded-Host"] = publicHost;
    }
    else
    {
        ctx.Request.Headers.Remove("X-Forwarded-Proto");
        ctx.Request.Headers.Remove("X-Forwarded-Host");
        ctx.Request.Headers.Remove("X-Forwarded-For");
    }
    await next();
});
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
};
if (!string.IsNullOrWhiteSpace(publicHost))
{
    forwardedHeadersOptions.AllowedHosts.Add(publicHost);
}
// Cloudflare can connect from any IP — clear the default localhost-only allowlist.
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

// Optional path-base for hosting under a public host's sub-path
// (e.g. demos.guitaralchemist.com/chatbot via Cloudflare Tunnel ingress
// `path: ^/chatbot(/.*)?$ -> localhost:5252`). Empty by default so direct
// localhost:5252/ access continues to work; UsePathBase only strips the
// prefix when present, so BOTH access modes coexist.
var pathBase = builder.Configuration["Chatbot:PathBase"];
if (!string.IsNullOrWhiteSpace(pathBase))
{
    // Normalise: must start with '/'. A misconfigured value like "chatbot"
    // (no leading slash) would silently bypass UsePathBase. Force the
    // slash so config typos still produce correct routing. Strip any
    // trailing slash on the configured value too — the redirect logic
    // below adds it back deliberately.
    if (!pathBase.StartsWith('/')) pathBase = "/" + pathBase;
    pathBase = pathBase.TrimEnd('/');

    var pathBaseNoSlash   = pathBase;
    var pathBaseWithSlash = pathBase + "/";

    // Trailing-slash redirect: must run BEFORE UsePathBase so it sees
    // the unstripped request path. A user landing at `/chatbot` (no
    // slash) resolves wwwroot/index.html's relative URLs against the
    // parent dir, so `fetch('api/chatbot/chat')` becomes
    // `/api/chatbot/chat` at the host root — bypasses the Cloudflare
    // path-based ingress and 404s. PR #111 review flagged this as the
    // same regression class as shipped bug #2 (VexFlow not loaded).
    // 308 (permanent + preserveMethod) so POSTs redirect cleanly too.
    app.Use(async (ctx, next) =>
    {
        if (string.Equals(ctx.Request.Path.Value, pathBaseNoSlash, StringComparison.Ordinal))
        {
            var qs = ctx.Request.QueryString.HasValue ? ctx.Request.QueryString.Value : string.Empty;
            ctx.Response.Redirect(pathBaseWithSlash + qs, permanent: true, preserveMethod: true);
            return;
        }
        await next();
    });

    app.UsePathBase(pathBaseNoSlash);
}

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();

if (allowedOrigins.Length > 0)
{
    app.UseCors("ChatbotClient");
}

app.MapControllers();

app.MapGet("/api", () => Results.Ok(new
{
    service = "ga-chatbot-api",
    version = "0.1.0",
    description = "Thin Guitar Alchemist chatbot API host"
}));

app.Run();

public partial class Program;
